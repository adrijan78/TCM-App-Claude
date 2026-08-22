namespace TCM.Application.Options;

/// <summary>
/// Where the Angular client lives. Used to build the links inside outgoing emails. Configured
/// per environment — never a hardcoded host (SPEC section 9).
/// </summary>
public class ClientSettings
{
    public const string SectionName = "Client";

    public string BaseUrl { get; set; } = string.Empty;
}
