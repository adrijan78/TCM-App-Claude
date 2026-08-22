namespace TCM.Application.Dtos.Common;

/// <summary>
/// SPEC section 3.1 — PhotoDto. Metadata only: the bytes are fetched separately from
/// <c>GET /api/photos/{publicId}</c> so a list of members never carries a list of images.
/// </summary>
public record PhotoDto(Guid PublicId, string FileName, string ContentType, int SizeBytes, DateTime CreatedAt);

/// <summary>The bytes themselves, on their way out to the client.</summary>
public record PhotoContentDto(byte[] Content, string ContentType, string FileName, string ETag);

/// <summary>An incoming upload, already read off the request.</summary>
public record PhotoUploadDto(string FileName, byte[] Content);
