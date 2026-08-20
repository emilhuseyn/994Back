using AutoMapper;
using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Application.Services.Abstractions;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class BrandService : IBrandService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public BrandService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<List<BrandDto>> ListAsync(CancellationToken ct = default)
    {
        var items = await _uow.Brands.Query()
            .Include(b => b.Products)
            .OrderBy(b => b.Name)
            .ToListAsync(ct);
        return _mapper.Map<List<BrandDto>>(items);
    }

    public async Task<BrandDto> CreateAsync(CreateBrandRequest request, CancellationToken ct = default)
    {
        var slug = SlugHelper.EnsureUnique(SlugHelper.Slugify(request.Name),
            s => _uow.Brands.Query().Any(b => b.Slug == s));

        var brand = new Brand
        {
            Name = request.Name.Trim(),
            Slug = slug,
            LogoUrl = request.LogoUrl,
            IsActive = request.IsActive
        };
        await _uow.Brands.AddAsync(brand, ct);
        await _uow.SaveChangesAsync(ct);
        return _mapper.Map<BrandDto>(brand);
    }

    public async Task<BrandDto> UpdateAsync(int id, UpdateBrandRequest request, CancellationToken ct = default)
    {
        var brand = await _uow.Brands.GetByIdAsync(id, ct) ?? throw new NotFoundException("Brend");
        if (brand.Name != request.Name.Trim())
        {
            brand.Name = request.Name.Trim();
            brand.Slug = SlugHelper.EnsureUnique(SlugHelper.Slugify(brand.Name),
                s => _uow.Brands.Query().Any(b => b.Slug == s && b.Id != id));
        }
        brand.LogoUrl = request.LogoUrl;
        brand.IsActive = request.IsActive;
        brand.UpdatedAt = DateTime.UtcNow;
        _uow.Brands.Update(brand);
        await _uow.SaveChangesAsync(ct);
        return _mapper.Map<BrandDto>(brand);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var brand = await _uow.Brands.GetByIdAsync(id, ct) ?? throw new NotFoundException("Brend");
        brand.IsDeleted = true;
        brand.DeletedAt = DateTime.UtcNow;
        _uow.Brands.Update(brand);
        await _uow.SaveChangesAsync(ct);
    }
}
