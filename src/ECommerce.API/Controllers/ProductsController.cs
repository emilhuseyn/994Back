using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;
    public ProductsController(IProductService service) => _service = service;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PaginatedList<ProductListItemDto>>>> List(
        [FromQuery] ProductQueryParameters parameters, CancellationToken ct)
        => Ok(ApiResponse<PaginatedList<ProductListItemDto>>.Ok(await _service.ListAsync(parameters, ct)));

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ProductDetailDto>>> GetBySlug(string slug, CancellationToken ct)
        => Ok(ApiResponse<ProductDetailDto>.Ok(await _service.GetBySlugAsync(slug, ct)));
}

[ApiController]
[Route("api/admin/products")]
[Authorize(Roles = "Admin")]
public class AdminProductsController : ControllerBase
{
    private readonly IProductService _service;
    public AdminProductsController(IProductService service) => _service = service;

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ProductDetailDto>>> GetById(int id, CancellationToken ct)
        => Ok(ApiResponse<ProductDetailDto>.Ok(await _service.GetByIdAsync(id, ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProductDetailDto>>> Create([FromBody] CreateProductRequest request, CancellationToken ct)
        => Ok(ApiResponse<ProductDetailDto>.Ok(await _service.CreateAsync(request, ct), "Məhsul yaradıldı."));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<ProductDetailDto>>> Update(int id, [FromBody] UpdateProductRequest request, CancellationToken ct)
        => Ok(ApiResponse<ProductDetailDto>.Ok(await _service.UpdateAsync(id, request, ct), "Məhsul yeniləndi."));

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse>> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Məhsul silindi."));
    }

    [HttpPost("{id:int}/images")]
    public async Task<ActionResult<ApiResponse<List<ProductImageDto>>>> AddImages(int id, [FromForm] IFormFileCollection files, CancellationToken ct)
        => Ok(ApiResponse<List<ProductImageDto>>.Ok(await _service.AddImagesAsync(id, files, ct), "Şəkillər yükləndi."));

    [HttpPost("{id:int}/variants")]
    public async Task<ActionResult<ApiResponse<ProductVariantDto>>> AddVariant(int id, [FromBody] AddVariantRequest request, CancellationToken ct)
        => Ok(ApiResponse<ProductVariantDto>.Ok(await _service.AddVariantAsync(id, request, ct), "Variant əlavə edildi."));
}

[ApiController]
[Route("api/admin/product-images")]
[Authorize(Roles = "Admin")]
public class AdminProductImagesController : ControllerBase
{
    private readonly IProductService _service;
    public AdminProductImagesController(IProductService service) => _service = service;

    [HttpDelete("{imageId:int}")]
    public async Task<ActionResult<ApiResponse>> Delete(int imageId, CancellationToken ct)
    {
        await _service.DeleteImageAsync(imageId, ct);
        return Ok(ApiResponse.Ok("Şəkil silindi."));
    }

    [HttpPut("{imageId:int}/main")]
    public async Task<ActionResult<ApiResponse<ProductImageDto>>> SetMain(int imageId, CancellationToken ct)
        => Ok(ApiResponse<ProductImageDto>.Ok(await _service.SetMainImageAsync(imageId, ct), "Əsas şəkil təyin edildi."));
}

[ApiController]
[Route("api/admin/product-variants")]
[Authorize(Roles = "Admin")]
public class AdminProductVariantsController : ControllerBase
{
    private readonly IProductService _service;
    public AdminProductVariantsController(IProductService service) => _service = service;

    [HttpPut("{variantId:int}")]
    public async Task<ActionResult<ApiResponse<ProductVariantDto>>> Update(int variantId, [FromBody] UpdateVariantRequest request, CancellationToken ct)
        => Ok(ApiResponse<ProductVariantDto>.Ok(await _service.UpdateVariantAsync(variantId, request, ct), "Variant yeniləndi."));

    [HttpDelete("{variantId:int}")]
    public async Task<ActionResult<ApiResponse>> Delete(int variantId, CancellationToken ct)
    {
        await _service.DeleteVariantAsync(variantId, ct);
        return Ok(ApiResponse.Ok("Variant silindi."));
    }
}
