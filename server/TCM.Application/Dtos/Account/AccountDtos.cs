namespace TCM.Application.Dtos.Account;

/// <summary>SPEC section 6.1 — login form.</summary>
public record LoginMemberDto(string Email, string Password);

/// <summary>What a successful login returns. Carries no secrets beyond the token itself.</summary>
public record MemberTokenDto(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    bool IsCoach,
    IReadOnlyList<string> Roles,
    string Token,
    DateTimeOffset ExpiresAt,
    string? PhotoUrl);

/// <summary>
/// What registering a member returns to the coach who did it.
/// </summary>
/// <remarks>
/// Deliberately carries no token. Registration authenticates the coach, not the member being
/// created, so returning a signed JWT for the new account would hand the caller a working
/// credential for a principal that is not them — and for a second coach, a full admin one. The
/// new member signs in themselves. SPEC section 6.1 never asks registration to issue a token.
/// </remarks>
public record RegisteredMemberDto(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    bool IsCoach,
    IReadOnlyList<string> Roles);

/// <summary>
/// SPEC section 6.1 — the coach-only registration form. There is no public sign-up, so this is
/// only ever submitted by an authenticated coach.
/// </summary>
public record MemberRegisterDto(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    decimal? Height,
    decimal? Weight,
    DateOnly DateOfBirth,
    int BeltId,
    string Role);

/// <summary>SPEC section 6.1 — "Forgot Password", available to everyone.</summary>
public record ForgotPasswordDto(string Email);

/// <summary>SPEC section 6.1 — the reset form reached from the emailed link.</summary>
public record ResetPasswordDto(string Email, string Token, string NewPassword, string ConfirmPassword);

/// <summary>SPEC section 3.1 — RoleDto.</summary>
public record RoleDto(string Id, string Name);

/// <summary>SPEC section 3.1 — MemberRoleDto.</summary>
public record MemberRoleDto(string MemberId, IReadOnlyList<string> Roles);
