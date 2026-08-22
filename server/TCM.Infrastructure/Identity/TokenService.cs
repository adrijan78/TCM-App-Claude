using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TCM.Application.Abstractions;
using TCM.Application.Options;
using TCM.Domain.Entities;

namespace TCM.Infrastructure.Identity;

/// <summary>
/// Issues the JWT returned on a successful login (SPEC section 7). The payload carries only the
/// user id, email and roles — everything else the client needs it fetches through an
/// authorized endpoint, where the server can check who is asking.
/// </summary>
public class TokenService : ITokenService
{
    private readonly JwtSettings _settings;
    private readonly SigningCredentials _credentials;

    public TokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;

        if (string.IsNullOrWhiteSpace(_settings.Key)
            || Encoding.UTF8.GetByteCount(_settings.Key) < JwtSettings.MinimumKeyLengthBytes)
        {
            // Fail loudly at startup rather than issuing weakly-signed tokens for months.
            throw new InvalidOperationException(
                $"Jwt:Key must be configured and at least {JwtSettings.MinimumKeyLengthBytes} bytes long. " +
                "Set it with 'dotnet user-secrets set' or an environment variable.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public (string Token, DateTimeOffset ExpiresAt) CreateToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_settings.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: _credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
