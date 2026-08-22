namespace TCM.Application.Options;

/// <summary>
/// Bound from the "Jwt" configuration section. Every value is environment-supplied — none of it
/// belongs in source (SPEC section 9).
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 120;

    /// <summary>
    /// HMAC-SHA256 needs at least 256 bits of key. A shorter key throws at startup rather than
    /// silently weakening every token the app issues.
    /// </summary>
    public const int MinimumKeyLengthBytes = 32;
}
