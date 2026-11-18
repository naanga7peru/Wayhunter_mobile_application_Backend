
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts.HomeAdsAnalytics;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;

namespace Wayhunter.Api.Controllers.Admin.HomeAdsAnalytics;

[ApiController]
[Authorize]
[Route(AdvertisementAdminEndpointContract.AdminBaseRoute)]
public sealed class AdvertisementsController : ControllerBase
{
    private readonly IAdvertisementRepository _reader;
    private readonly IAdvertisementWriteRepository _writer;
    private readonly IConfiguration _configuration;

    public AdvertisementsController(
        IAdvertisementRepository reader,
        IAdvertisementWriteRepository writer,
        IConfiguration configuration)
    {
        _reader = reader ??
            throw new ArgumentNullException(nameof(reader));

        _writer = writer ??
            throw new ArgumentNullException(nameof(writer));

        _configuration = configuration ??
            throw new ArgumentNullException(nameof(configuration));
    }

    private bool IsAdministrator()
    {
        var adminRole = _configuration[
            "Identity:AdministratorRole"];

        return AdvertisementAdminPrincipalReadiness
            .HasAuthenticatedAdminRole(User, adminRole);
    }

    // ==========================================
    // GET - All advertisements
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        if (!IsAdministrator())
            return Forbid();

        var advertisements = await _reader.GetAllAsync(
            cancellationToken);

        return Ok(advertisements);
    }

    // ==========================================
    // GET - Advertisement by ID
    // ==========================================

    [HttpGet("{advertisementId:guid}")]
    public async Task<IActionResult> GetById(
        Guid advertisementId,
        CancellationToken cancellationToken)
    {
        if (!IsAdministrator())
            return Forbid();

        if (advertisementId == Guid.Empty)
            return BadRequest("Invalid advertisement ID.");

        var advertisement = await _reader.GetByIdAsync(
            advertisementId,
            cancellationToken);

        if (advertisement is null)
            return NotFound();

        return Ok(advertisement);
    }

    // ==========================================
    // POST - Create draft advertisement
    // ==========================================

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] AdvertisementCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsAdministrator())
            return Forbid();

        if (!AdvertisementCreateRequestValidator.IsValid(request))
        {
            return BadRequest(
                "Invalid advertisement create request.");
        }

        var created = await _writer.CreateAsync(
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { advertisementId = created.Id },
            created);
    }

    // ==========================================
    // PUT - Update advertisement
    // ==========================================

    [HttpPut("{advertisementId:guid}")]
    public async Task<IActionResult> Update(
        Guid advertisementId,
        [FromBody] AdvertisementUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsAdministrator())
            return Forbid();

        if (advertisementId == Guid.Empty ||
            !AdvertisementUpdateRequestValidator.IsValid(request))
        {
            return BadRequest(
                "Invalid advertisement update request.");
        }

        try
        {
            var updated = await _writer.UpdateAsync(
                advertisementId,
                request,
                cancellationToken);

            if (updated is null)
                return NotFound();

            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    // ==========================================
    // PATCH - Change advertisement status
    // ==========================================

    [HttpPatch("{advertisementId:guid}/status")]
    public async Task<IActionResult> ChangeStatus(
        Guid advertisementId,
        [FromBody] AdvertisementStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsAdministrator())
            return Forbid();

        if (advertisementId == Guid.Empty ||
            !AdvertisementStatusUpdateRequestValidator.IsValid(request))
        {
            return BadRequest(
                "Invalid advertisement status request.");
        }

        try
        {
            var updated = await _writer.ChangeStatusAsync(
                advertisementId,
                request,
                cancellationToken);

            if (updated is null)
                return NotFound();

            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }
}
