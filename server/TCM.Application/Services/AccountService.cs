using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TCM.Application.Abstractions;
using TCM.Application.Common;
using TCM.Application.Dtos.Account;
using TCM.Application.Options;
using TCM.Domain.Constants;
using TCM.Domain.Entities;

namespace TCM.Application.Services;

/// <summary>
/// Login, coach-driven registration and password reset (SPEC sections 6.1 and 7).
/// </summary>
public class AccountService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ITokenService tokenService,
    IEmailService emailService,
    IStripeCustomerService stripeCustomerService,
    IRepository<Belt> belts,
    IRepository<MemberBelt> memberBelts,
    IValidator<MemberRegisterDto> registerValidator,
    IValidator<ResetPasswordDto> resetValidator,
    IOptions<ClientSettings> clientSettings,
    ILogger<AccountService> logger) : IAccountService
{
    /// <summary>
    /// One message for every login failure. Distinguishing "no such account" from "wrong
    /// password" would let anyone enumerate the club's member emails.
    /// </summary>
    private const string InvalidCredentials = "Invalid email or password.";

    public async Task<ApiResponse<MemberTokenDto>> LoginAsync(LoginMemberDto dto, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);

        if (user is null)
        {
            return ApiResponse<MemberTokenDto>.Fail(InvalidCredentials, ErrorKind.Unauthorized);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return ApiResponse<MemberTokenDto>.Fail(
                "This account is temporarily locked after too many failed attempts. Try again later.",
                ErrorKind.Unauthorized);
        }

        if (!await userManager.CheckPasswordAsync(user, dto.Password))
        {
            await userManager.AccessFailedAsync(user);
            return ApiResponse<MemberTokenDto>.Fail(InvalidCredentials, ErrorKind.Unauthorized);
        }

        // A deactivated member keeps their history but must not be able to sign in (section 6.3).
        // Checked after the password so an attacker cannot use the message to probe account state.
        if (!user.IsActive)
        {
            return ApiResponse<MemberTokenDto>.Fail(
                "This account is inactive. Please contact your coach.", ErrorKind.Unauthorized);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        return ApiResponse<MemberTokenDto>.Ok(await BuildTokenAsync(user));
    }

    public async Task<ApiResponse<MemberTokenDto>> RegisterAsync(
        MemberRegisterDto dto, string callerId, CancellationToken ct = default)
    {
        var validation = await registerValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
        {
            return validation.ToFailure<MemberTokenDto>();
        }

        if (!await roleManager.RoleExistsAsync(dto.Role))
        {
            return ApiResponse<MemberTokenDto>.Fail($"Unknown role '{dto.Role}'.");
        }

        if (await userManager.FindByEmailAsync(dto.Email) is not null)
        {
            return ApiResponse<MemberTokenDto>.Conflict("A member with that email already exists.");
        }

        var coach = await userManager.FindByIdAsync(callerId);
        if (coach is null)
        {
            return ApiResponse<MemberTokenDto>.Forbidden();
        }

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            EmailConfirmed = true,
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            DateOfBirth = dto.DateOfBirth,
            StartedOn = DateOnly.FromDateTime(DateTime.UtcNow),
            Height = dto.Height,
            Weight = dto.Weight,
            IsActive = true,
            IsCoach = dto.Role == Roles.Coach,
            // 1 coach : 1 club, so a new member joins the registering coach's club (SPEC section 9).
            ClubId = coach.ClubId
        };

        var created = await userManager.CreateAsync(user, dto.Password);
        if (!created.Succeeded)
        {
            return ApiResponse<MemberTokenDto>.Fail(
                "Could not create the member.",
                ErrorKind.Validation,
                created.Errors.Select(e => e.Description).ToList());
        }

        var roled = await userManager.AddToRoleAsync(user, dto.Role);
        if (!roled.Succeeded)
        {
            // Leaving a user with no role would strand them: they could log in but reach nothing.
            await userManager.DeleteAsync(user);
            return ApiResponse<MemberTokenDto>.Fail(
                "Could not assign the role.",
                ErrorKind.Validation,
                roled.Errors.Select(e => e.Description).ToList());
        }

        // Stripe and email are both best-effort: neither may fail the registration.
        var stripeCustomerId = await stripeCustomerService.CreateCustomerAsync(user, ct);
        if (!string.IsNullOrWhiteSpace(stripeCustomerId))
        {
            user.StripeCustomerId = stripeCustomerId;
            await userManager.UpdateAsync(user);
        }

        // The registration form asks for a belt (SPEC 6.1), so record it as the member's current
        // one. Without this the belt is validated and then thrown away, leaving every new member
        // with an empty belt history until a coach adds one by hand.
        await AssignStartingBeltAsync(user, dto.BeltId, ct);

        await SendWelcomeEmailAsync(user, ct);

        logger.LogInformation("Coach {CoachId} registered member {MemberId}.", callerId, user.Id);
        return ApiResponse<MemberTokenDto>.Ok(await BuildTokenAsync(user));
    }

    public async Task<ApiResponse<Unit>> ForgotPasswordAsync(ForgotPasswordDto dto, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);

        // Only send when the account exists and is usable, but always answer the same way. The
        // response must not reveal whether an email is registered.
        if (user is { IsActive: true } && !string.IsNullOrWhiteSpace(user.Email))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var link = BuildResetLink(user.Email, token);

            await emailService.SendAsync(new SendEmailRequest(
                user.Email,
                $"{user.FirstName} {user.LastName}",
                "Reset your password",
                $"""
                 <p>Hello {WebUtility.HtmlEncode(user.FirstName)},</p>
                 <p>Use the link below to choose a new password. It can only be used once.</p>
                 <p><a href="{WebUtility.HtmlEncode(link)}">Reset my password</a></p>
                 <p>If you did not request this, you can ignore this email.</p>
                 """,
                $"Hello {user.FirstName},\n\nReset your password: {link}\n\nIf you did not request this, ignore this email."),
                ct);
        }

        return ApiResponse.Ok("If that email is registered, a reset link has been sent to it.");
    }

    public async Task<ApiResponse<Unit>> ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default)
    {
        var validation = await resetValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
        {
            return validation.ToFailure<Unit>();
        }

        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user is null)
        {
            // Same generic answer as an invalid token, for the same enumeration reason.
            return ApiResponse.Fail("That reset link is invalid or has expired.");
        }

        var result = await userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
        if (!result.Succeeded)
        {
            logger.LogInformation("Password reset rejected for user {UserId}.", user.Id);
            return ApiResponse<Unit>.Fail(
                "That reset link is invalid or has expired.",
                ErrorKind.Validation,
                result.Errors.Select(e => e.Description).ToList());
        }

        // A successful reset clears any lockout from the failed attempts that led here.
        await userManager.ResetAccessFailedCountAsync(user);

        logger.LogInformation("Password reset for user {UserId}.", user.Id);
        return ApiResponse.Ok("Your password has been changed. You can now sign in.");
    }

    private async Task<MemberTokenDto> BuildTokenAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var (token, expiresAt) = tokenService.CreateToken(user, roles);

        return new MemberTokenDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email ?? string.Empty,
            user.IsCoach,
            roles.ToList(),
            token,
            expiresAt,
            null);
    }

    private string BuildResetLink(string email, string token)
    {
        // Client origin is configuration, never a hardcoded host (SPEC section 9).
        // EscapeDataString percent-encodes the '+' and '/' that Identity tokens contain, so the
        // token survives the round trip through the query string intact.
        var baseUrl = clientSettings.Value.BaseUrl.TrimEnd('/');
        var encodedToken = Uri.EscapeDataString(token);
        var encodedEmail = Uri.EscapeDataString(email);

        return $"{baseUrl}/reset-password?email={encodedEmail}&token={encodedToken}";
    }

    /// <summary>
    /// Gives a newly registered member their starting belt, flagged current. A missing or unknown
    /// belt id is logged and skipped rather than failing the registration — the coach can add the
    /// belt afterwards, but they should not lose the member they just created.
    /// </summary>
    private async Task AssignStartingBeltAsync(ApplicationUser user, int beltId, CancellationToken ct)
    {
        var belt = await belts.GetByIdAsync(beltId, ct);
        if (belt is null)
        {
            logger.LogWarning(
                "Member {MemberId} was registered with unknown belt id {BeltId}; no belt recorded.",
                user.Id, beltId);
            return;
        }

        await memberBelts.AddAsync(new MemberBelt
        {
            MemberId = user.Id,
            BeltId = belt.Id,
            DateReceived = user.StartedOn,
            IsCurrentBelt = true
        }, ct);

        await memberBelts.SaveChangesAsync(ct);
    }

    private async Task SendWelcomeEmailAsync(ApplicationUser user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(user.Email)) return;

        await emailService.SendAsync(new SendEmailRequest(
            user.Email,
            $"{user.FirstName} {user.LastName}",
            "Welcome to the club",
            $"""
             <p>Hello {WebUtility.HtmlEncode(user.FirstName)},</p>
             <p>Your coach has registered you. Sign in with this email address and the password you were given.</p>
             """,
            $"Hello {user.FirstName},\n\nYour coach has registered you. Sign in with this email address and the password you were given."),
            ct);
    }
}
