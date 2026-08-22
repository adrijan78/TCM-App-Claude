using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TCM.Application.Common;
using TCM.Application.Dtos.Common;
using TCM.Application.Options;
using TCM.Application.Services;

namespace TCM.Api.Controllers;

/// <summary>
/// Member and club photos, stored in the database (decided 2026-08-22, superseding SPEC
/// section 2's Firebase Storage choice).
/// </summary>
[Authorize]
public class PhotosController(IPhotoService photoService, IOptions<PhotoSettings> photoSettings) : BaseController
{
    [HttpPost("member/{memberId}")]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<PhotoDto>>> Upload(
        string memberId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return HandleResult(ApiResponse<PhotoDto>.Fail("No file was uploaded."));
        }

        // Checked before buffering: reading an oversized upload into memory to then reject it
        // is exactly the denial-of-service the limit exists to prevent.
        if (file.Length > photoSettings.Value.MaxSizeBytes)
        {
            return HandleResult(ApiResponse<PhotoDto>.Fail(
                $"Images must be {photoSettings.Value.MaxSizeBytes / 1024 / 1024} MB or smaller."));
        }

        using var memory = new MemoryStream();
        await file.CopyToAsync(memory, ct);

        var upload = new PhotoUploadDto(file.FileName, memory.ToArray());
        return HandleResult(await photoService.UploadForMemberAsync(memberId, upload, CallerId, IsCoach, ct));
    }

    /// <summary>
    /// Authenticated on purpose. An <c>img src</c> cannot carry a bearer token, so the client
    /// fetches this through the authenticated HTTP client and renders an object URL.
    /// </summary>
    [HttpGet("{publicId:guid}")]
    public async Task<IActionResult> Get(Guid publicId, CancellationToken ct)
    {
        var result = await photoService.GetContentAsync(publicId, CallerId, IsCoach, ct);

        if (!result.Success || result.Data is null)
        {
            return HandleResult(result).Result ?? StatusCode(StatusCodes.Status403Forbidden, result);
        }

        var photo = result.Data;

        // Private: this must not be held by a shared cache between different signed-in users.
        Response.Headers.CacheControl = "private, max-age=3600";
        Response.Headers.ETag = photo.ETag;

        if (Request.Headers.IfNoneMatch.Any(tag => tag == photo.ETag))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return File(photo.Content, photo.ContentType);
    }

    [HttpDelete("{publicId:guid}")]
    public async Task<ActionResult<ApiResponse<Unit>>> Delete(Guid publicId, CancellationToken ct)
        => HandleResult(await photoService.DeleteAsync(publicId, CallerId, IsCoach, ct));
}
