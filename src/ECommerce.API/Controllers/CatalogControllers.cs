using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _service;
    public CategoriesController(ICategoryService service) => _service = service;

    [HttpGet("tree")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<CategoryTreeDto>>>> Tree(CancellationToken ct)
        => Ok(ApiResponse<List<CategoryTreeDto>>.Ok(await _service.GetTreeAsync(ct)));

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> GetBySlug(string slug, CancellationToken ct)
        => Ok(ApiResponse<CategoryDto>.Ok(await _service.GetBySlugAsync(slug, ct)));
}

[ApiController]
[Route("api/admin/categories")]
[Authorize(Roles = "Admin")]
public class AdminCategoriesController : ControllerBase
{
    private readonly ICategoryService _service;
    public AdminCategoriesController(ICategoryService service) => _service = service;

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> Create([FromBody] CreateCategoryRequest request, CancellationToken ct)
        => Ok(ApiResponse<CategoryDto>.Ok(await _service.CreateAsync(request, ct), "Kateqoriya yaradıldı."));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> Update(int id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
        => Ok(ApiResponse<CategoryDto>.Ok(await _service.UpdateAsync(id, request, ct), "Kateqoriya yeniləndi."));

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse>> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Kateqoriya silindi."));
    }
}

[ApiController]
[Route("api/brands")]
public class BrandsController : ControllerBase
{
    private readonly IBrandService _service;
    public BrandsController(IBrandService service) => _service = service;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<BrandDto>>>> List(CancellationToken ct)
        => Ok(ApiResponse<List<BrandDto>>.Ok(await _service.ListAsync(ct)));
}

[ApiController]
[Route("api/admin/brands")]
[Authorize(Roles = "Admin")]
public class AdminBrandsController : ControllerBase
{
    private readonly IBrandService _service;
    public AdminBrandsController(IBrandService service) => _service = service;

    [HttpPost]
    public async Task<ActionResult<ApiResponse<BrandDto>>> Create([FromBody] CreateBrandRequest request, CancellationToken ct)
        => Ok(ApiResponse<BrandDto>.Ok(await _service.CreateAsync(request, ct), "Brend yaradıldı."));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<BrandDto>>> Update(int id, [FromBody] UpdateBrandRequest request, CancellationToken ct)
        => Ok(ApiResponse<BrandDto>.Ok(await _service.UpdateAsync(id, request, ct), "Brend yeniləndi."));

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse>> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Brend silindi."));
    }
}

[ApiController]
[Route("api/filters")]
public class FiltersController : ControllerBase
{
    private readonly IFilterService _service;
    public FiltersController(IFilterService service) => _service = service;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<FiltersDto>>> Get(CancellationToken ct)
        => Ok(ApiResponse<FiltersDto>.Ok(await _service.GetAsync(ct)));
}
