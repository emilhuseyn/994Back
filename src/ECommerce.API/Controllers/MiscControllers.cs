using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Interfaces.Infrastructure;
using ECommerce.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/wishlist")]
[Authorize]
public class WishlistController : ControllerBase
{
    private readonly IWishlistService _service;
    public WishlistController(IWishlistService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<WishlistItemDto>>>> List(CancellationToken ct)
        => Ok(ApiResponse<List<WishlistItemDto>>.Ok(await _service.ListAsync(ct)));

    [HttpPost("{productId:int}")]
    public async Task<ActionResult<ApiResponse>> Add(int productId, CancellationToken ct)
    {
        await _service.AddAsync(productId, ct);
        return Ok(ApiResponse.Ok("İstək siyahısına əlavə edildi."));
    }

    [HttpDelete("{productId:int}")]
    public async Task<ActionResult<ApiResponse>> Remove(int productId, CancellationToken ct)
    {
        await _service.RemoveAsync(productId, ct);
        return Ok(ApiResponse.Ok("Siyahıdan silindi."));
    }
}

[ApiController]
[Route("api/contact")]
public class ContactController : ControllerBase
{
    private readonly IContactService _service;
    public ContactController(IContactService service) => _service = service;

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse>> Submit([FromBody] CreateContactMessageRequest request, CancellationToken ct)
    {
        await _service.SubmitAsync(request, ct);
        return Ok(ApiResponse.Ok("Mesaj göndərildi."));
    }
}

[ApiController]
[Route("api/admin/contact-messages")]
[Authorize(Roles = "Admin")]
public class AdminContactController : ControllerBase
{
    private readonly IContactService _service;
    public AdminContactController(IContactService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedList<ContactMessageDto>>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] bool? isRead = null, CancellationToken ct = default)
        => Ok(ApiResponse<PaginatedList<ContactMessageDto>>.Ok(await _service.ListAsync(page, pageSize, isRead, ct)));

    [HttpPut("{id:int}/read")]
    public async Task<ActionResult<ApiResponse>> MarkRead(int id, CancellationToken ct)
    {
        await _service.MarkReadAsync(id, ct);
        return Ok(ApiResponse.Ok("İşarələndi."));
    }
}

[ApiController]
[Route("api/sliders")]
public class SlidersController : ControllerBase
{
    private readonly ISliderService _service;
    public SlidersController(ISliderService service) => _service = service;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<SliderDto>>>> List(CancellationToken ct)
        => Ok(ApiResponse<List<SliderDto>>.Ok(await _service.ListAsync(onlyActive: true, ct)));
}

[ApiController]
[Route("api/admin/sliders")]
[Authorize(Roles = "Admin")]
public class AdminSlidersController : ControllerBase
{
    private readonly ISliderService _service;
    public AdminSlidersController(ISliderService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<SliderDto>>>> ListAll(CancellationToken ct)
        => Ok(ApiResponse<List<SliderDto>>.Ok(await _service.ListAsync(onlyActive: false, ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SliderDto>>> Create([FromBody] CreateSliderRequest request, CancellationToken ct)
        => Ok(ApiResponse<SliderDto>.Ok(await _service.CreateAsync(request, ct), "Slider yaradıldı."));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<SliderDto>>> Update(int id, [FromBody] UpdateSliderRequest request, CancellationToken ct)
        => Ok(ApiResponse<SliderDto>.Ok(await _service.UpdateAsync(id, request, ct), "Slider yeniləndi."));

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse>> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Slider silindi."));
    }
}

/// <summary>
/// Generic file upload for admin use — sliders, brand logos, category images.
/// Returns the public URL of the saved file so the caller can set it on a
/// resource via the normal update flow.
/// </summary>
[ApiController]
[Route("api/admin/uploads")]
[Authorize(Roles = "Admin")]
public class AdminUploadsController : ControllerBase
{
    private readonly IFileStorageService _files;
    public AdminUploadsController(IFileStorageService files) => _files = files;

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<UploadResponse>>> Upload(
        [FromForm] UploadRequest request,
        CancellationToken ct)
    {
        if (request.File is null || request.File.Length == 0)
            throw new ConflictException("Boş fayl.");
        var safeFolder = string.IsNullOrWhiteSpace(request.Folder)
            ? "misc"
            : request.Folder.Trim('/');
        var url = await _files.SaveAsync(request.File, safeFolder, ct);
        return Ok(ApiResponse<UploadResponse>.Ok(new UploadResponse(url), "Fayl yükləndi."));
    }
}

public class UploadRequest
{
    public IFormFile File { get; set; } = default!;
    public string? Folder { get; set; }
}

public record UploadResponse(string Url);

[ApiController]
[Route("api/site-settings")]
public class SiteSettingsController : ControllerBase
{
    private readonly ISiteSettingService _service;
    public SiteSettingsController(ISiteSettingService service) => _service = service;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<SiteSettingDto>>>> List(CancellationToken ct)
        => Ok(ApiResponse<List<SiteSettingDto>>.Ok(await _service.ListAsync(ct)));
}

[ApiController]
[Route("api/admin/site-settings")]
[Authorize(Roles = "Admin")]
public class AdminSiteSettingsController : ControllerBase
{
    private readonly ISiteSettingService _service;
    public AdminSiteSettingsController(ISiteSettingService service) => _service = service;

    [HttpPut("{key}")]
    public async Task<ActionResult<ApiResponse<SiteSettingDto>>> Update(string key, [FromBody] UpdateSiteSettingRequest request, CancellationToken ct)
        => Ok(ApiResponse<SiteSettingDto>.Ok(await _service.UpdateAsync(key, request, ct), "Parametr yeniləndi."));
}
