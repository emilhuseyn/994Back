using AutoMapper;
using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Application.Services.Abstractions;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public CategoryService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<List<CategoryTreeDto>> GetTreeAsync(CancellationToken ct = default)
    {
        var all = await _uow.Categories.Query()
            .Include(c => c.Products)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.NameAz)
            .ToListAsync(ct);

        var dtos = all.Select(c => _mapper.Map<CategoryTreeDto>(c)).ToList();
        var byId = dtos.ToDictionary(c => c.Id);
        var roots = new List<CategoryTreeDto>();
        foreach (var d in dtos)
        {
            if (d.ParentCategoryId is int pid && byId.TryGetValue(pid, out var parent))
                parent.Children.Add(d);
            else
                roots.Add(d);
        }
        return roots;
    }

    public async Task<CategoryDto> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var cat = await _uow.Categories.Query()
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Slug == slug, ct)
            ?? throw new NotFoundException("Kateqoriya");
        return _mapper.Map<CategoryDto>(cat);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        var slug = SlugHelper.EnsureUnique(SlugHelper.Slugify(request.NameAz),
            s => _uow.Categories.Query().Any(c => c.Slug == s));

        var cat = new Category
        {
            NameAz = request.NameAz.Trim(),
            NameRu = request.NameRu.Trim(),
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim(),
            Slug = slug,
            ParentCategoryId = request.ParentCategoryId,
            ImageUrl = request.ImageUrl,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };
        await _uow.Categories.AddAsync(cat, ct);
        await _uow.SaveChangesAsync(ct);
        return _mapper.Map<CategoryDto>(cat);
    }

    public async Task<CategoryDto> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var cat = await _uow.Categories.GetByIdAsync(id, ct) ?? throw new NotFoundException("Kateqoriya");
        if (cat.NameAz != request.NameAz.Trim())
        {
            cat.NameAz = request.NameAz.Trim();
            cat.Slug = SlugHelper.EnsureUnique(SlugHelper.Slugify(cat.NameAz),
                s => _uow.Categories.Query().Any(c => c.Slug == s && c.Id != id));
        }
        cat.NameRu = request.NameRu.Trim();
        cat.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim();
        cat.ParentCategoryId = request.ParentCategoryId;
        cat.ImageUrl = request.ImageUrl;
        cat.SortOrder = request.SortOrder;
        cat.IsActive = request.IsActive;
        cat.UpdatedAt = DateTime.UtcNow;
        _uow.Categories.Update(cat);
        await _uow.SaveChangesAsync(ct);
        return _mapper.Map<CategoryDto>(cat);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var cat = await _uow.Categories.GetByIdAsync(id, ct) ?? throw new NotFoundException("Kateqoriya");
        cat.IsDeleted = true;
        cat.DeletedAt = DateTime.UtcNow;
        _uow.Categories.Update(cat);
        await _uow.SaveChangesAsync(ct);
    }
}
