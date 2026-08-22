namespace TCM.Application.Options;

/// <summary>Bound from the "Photos" configuration section.</summary>
public class PhotoSettings
{
    public const string SectionName = "Photos";

    /// <summary>
    /// Hard cap on a stored image. Images live in the database, so this is what keeps the
    /// table from growing without bound. Two megabytes is ample for a profile photo.
    /// </summary>
    public int MaxSizeBytes { get; set; } = 2 * 1024 * 1024;
}
