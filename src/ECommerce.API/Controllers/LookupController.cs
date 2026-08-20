using AutoMapper;
using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/colors")]
public class ColorsController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public ColorsController(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<ColorDto>>>> List(CancellationToken ct)
    {
        var items = await _uow.Colors.Query().OrderBy(c => c.NameAz).ToListAsync(ct);
        return Ok(ApiResponse<List<ColorDto>>.Ok(_mapper.Map<List<ColorDto>>(items)));
    }
}

[ApiController]
[Route("api/admin/colors")]
[Authorize(Roles = "Admin")]
public class AdminColorsController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public AdminColorsController(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ColorDto>>> Create([FromBody] CreateColorRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NameAz))
            throw new ConflictException("Ad boş ola bilməz.");
        var nameAz = request.NameAz.Trim();
        if (await _uow.Colors.AnyAsync(c => c.NameAz == nameAz, ct))
            throw new ConflictException("Bu rəng artıq mövcuddur.");

        var color = new Color
        {
            NameAz = nameAz,
            NameRu = string.IsNullOrWhiteSpace(request.NameRu) ? nameAz : request.NameRu.Trim(),
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim(),
            HexCode = NormalizeHex(request.HexCode),
        };
        await _uow.Colors.AddAsync(color, ct);
        await _uow.SaveChangesAsync(ct);
        return Ok(ApiResponse<ColorDto>.Ok(_mapper.Map<ColorDto>(color), "Rəng əlavə edildi."));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<ColorDto>>> Update(int id, [FromBody] UpdateColorRequest request, CancellationToken ct)
    {
        var color = await _uow.Colors.GetByIdAsync(id, ct) ?? throw new NotFoundException("Rəng");
        var nameAz = request.NameAz.Trim();
        if (await _uow.Colors.AnyAsync(c => c.NameAz == nameAz && c.Id != id, ct))
            throw new ConflictException("Bu adda başqa rəng var.");

        color.NameAz = nameAz;
        color.NameRu = string.IsNullOrWhiteSpace(request.NameRu) ? nameAz : request.NameRu.Trim();
        color.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim();
        color.HexCode = NormalizeHex(request.HexCode);
        _uow.Colors.Update(color);
        await _uow.SaveChangesAsync(ct);
        return Ok(ApiResponse<ColorDto>.Ok(_mapper.Map<ColorDto>(color), "Rəng yeniləndi."));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse>> Delete(int id, CancellationToken ct)
    {
        var color = await _uow.Colors.GetByIdAsync(id, ct) ?? throw new NotFoundException("Rəng");
        var inUse = await _uow.ProductVariants.AnyAsync(v => v.ColorId == id, ct);
        if (inUse)
            throw new ConflictException("Bu rəng məhsul variantlarında istifadə olunur — əvvəlcə həmin variantları silin.");
        _uow.Colors.Remove(color);
        await _uow.SaveChangesAsync(ct);
        return Ok(ApiResponse.Ok("Rəng silindi."));
    }

    private static string NormalizeHex(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "#000000";
        var s = raw.Trim();
        if (!s.StartsWith('#')) s = "#" + s;
        if (s.Length != 4 && s.Length != 7) throw new ConflictException("HEX rəng formatı düzgün deyil.");
        return s.ToLowerInvariant();
    }
}

[ApiController]
[Route("api/sizes")]
public class SizesController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public SizesController(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<SizeDto>>>> List(CancellationToken ct)
    {
        var items = await _uow.Sizes.Query().OrderBy(s => s.SortOrder).ToListAsync(ct);
        return Ok(ApiResponse<List<SizeDto>>.Ok(_mapper.Map<List<SizeDto>>(items)));
    }
}

[ApiController]
[Route("api/admin/sizes")]
[Authorize(Roles = "Admin")]
public class AdminSizesController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public AdminSizesController(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SizeDto>>> Create([FromBody] CreateSizeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ConflictException("Ölçü adı boş ola bilməz.");
        var name = request.Name.Trim();
        if (await _uow.Sizes.AnyAsync(s => s.Name == name, ct))
            throw new ConflictException("Bu ölçü artıq mövcuddur.");

        var size = new Size { Name = name, SortOrder = request.SortOrder };
        await _uow.Sizes.AddAsync(size, ct);
        await _uow.SaveChangesAsync(ct);
        return Ok(ApiResponse<SizeDto>.Ok(_mapper.Map<SizeDto>(size), "Ölçü əlavə edildi."));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<SizeDto>>> Update(int id, [FromBody] UpdateSizeRequest request, CancellationToken ct)
    {
        var size = await _uow.Sizes.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ölçü");
        var name = request.Name.Trim();
        if (await _uow.Sizes.AnyAsync(s => s.Name == name && s.Id != id, ct))
            throw new ConflictException("Bu adda başqa ölçü var.");

        size.Name = name;
        size.SortOrder = request.SortOrder;
        _uow.Sizes.Update(size);
        await _uow.SaveChangesAsync(ct);
        return Ok(ApiResponse<SizeDto>.Ok(_mapper.Map<SizeDto>(size), "Ölçü yeniləndi."));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse>> Delete(int id, CancellationToken ct)
    {
        var size = await _uow.Sizes.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ölçü");
        var inUse = await _uow.ProductVariants.AnyAsync(v => v.SizeId == id, ct);
        if (inUse)
            throw new ConflictException("Bu ölçü məhsul variantlarında istifadə olunur — əvvəlcə həmin variantları silin.");
        _uow.Sizes.Remove(size);
        await _uow.SaveChangesAsync(ct);
        return Ok(ApiResponse.Ok("Ölçü silindi."));
    }
}
