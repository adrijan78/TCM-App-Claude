using TCM.Domain.Entities;

namespace TCM.Application.Abstractions;

/// <summary>
/// Issues the JWT that a successful login returns (SPEC section 7). Implemented in the
/// infrastructure layer so the application layer stays free of the signing libraries.
/// </summary>
public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateToken(ApplicationUser user, IEnumerable<string> roles);
}
