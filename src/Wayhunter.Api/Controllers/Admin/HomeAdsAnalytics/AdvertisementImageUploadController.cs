using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Authorization;
using Wayhunter.Api.Contracts.HomeAdsAnalytics;
using Wayhunter.Api.Services.HomeAdsAnalytics;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

namespace Wayhunter.Api.Controllers.Admin.HomeAdsAnalytics;

[ApiController]
[Authorize(Policy = AdvertisementAdministratorPolicy.Name)]
[Route(AdvertisementAdminEndpointContract.AdminBaseRoute)]
public sealed class AdvertisementImageUploadController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IAdvertisementRepository _repository;

    public AdvertisementImageUploadController(
        IConfiguration configuration,
        IAdvertisementRepository repository)
    {
        _configuration = configuration
            ?? throw new ArgumentNullException(nameof(configuration));

        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));

    }

    [HttpPost("{advertisementId:guid}/image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(AdvertisementImageValidator.MaxBytes + 65536)]
    [RequestFormLimits(
        MultipartBodyLengthLimit = AdvertisementImageValidator.MaxBytes)]
    public async Task<IActionResult> UploadImage(
        Guid advertisementId,
        CancellationToken cancellationToken)
    {
        var adminRole = _configuration["Identity:AdministratorRole"];

        if (!AdvertisementAdminPrincipalReadiness
            .HasAuthenticatedAdminRole(User, adminRole))
        {
            return Forbid();
        }

        // Never parse request body or query the repository
        // while the feature is disabled.
        if (!_configuration.GetValue<bool>(
            "Advertisements:ImageUpload:Enabled"))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                "Advertisement image upload is disabled.");
        }


        if (advertisementId == Guid.Empty)
            return BadRequest("Invalid advertisement ID.");

        var advertisement = await _repository.GetByIdAsync(
            advertisementId, cancellationToken);

        if (advertisement is null)
            return NotFound("Advertisement not found.");

        if (!Request.HasFormContentType ||
            !Request.ContentType!.StartsWith(
                "multipart/form-data",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Multipart image request is required.");
        }

        AdvertisementValidatedImage validated;

        try
        {
            // Form parsing happens only after all feature gates.
            var form = await Request.ReadFormAsync(cancellationToken);

            validated = await AdvertisementMultipartImageValidator.ValidateAsync(
                form, cancellationToken);
        }
        catch (InvalidDataException)
        {
            return BadRequest("Invalid multipart image data.");
        }
        catch (ArgumentException)
        {
            return BadRequest("Invalid or unsupported advertisement image.");
        }
        catch (BadHttpRequestException)
        {
            return BadRequest("Invalid multipart request.");
        }

        // Errors from the provider or SQL must never be
        // misclassified as invalid multipart input.
        // Do not delete assets after an ambiguous SQL failure.
        // Feature remains disabled until reconciliation readiness.
        // Resolve optional dependencies only after validation, so
        // disabled M7 SQL does not break API startup or HTTP tests.
        if (!_configuration.GetValue<bool>(
                "Advertisements:ImageUpload:LifecycleEnabled"))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                "Advertisement image storage is not available.");
        }

        var storage = HttpContext.RequestServices.GetService(
            typeof(IAdvertisementImageStorage))
            as IAdvertisementImageStorage;

        if (storage is null ||
            storage is UnavailableAdvertisementImageStorage)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                "Advertisement image storage is not available.");
        }

        var lifecycle = HttpContext.RequestServices.GetService(
            typeof(AdvertisementImageLifecycleService))
            as AdvertisementImageLifecycleService;

        if (lifecycle is null)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                "Advertisement image lifecycle is not available.");
        }

        var updated = await lifecycle.ReplaceAsync(
            advertisementId,
            advertisement.ImageUrl,
            validated,
            cancellationToken);

        if (updated is null)
        {
            return Conflict(
                "Advertisement image was changed by another request.");
        }

        return Ok(updated);
    }
}
