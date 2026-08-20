using AutoMapper;
using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Interfaces.Infrastructure;
using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Application.Services.Abstractions;
using ECommerce.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class ProductService : IProductService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IFileStorageService _files;

    public ProductService(IUnitOfWork uow, IMapper mapper, IFileStorageService files)
    {
        _uow = uow;
        _mapper = mapper;
        _files = files;
    }

    public async Task<PaginatedList<ProductListItemDto>> ListAsync(ProductQueryParameters p, CancellationToken ct = default)
    {
        var query = _uow.Products.Query()
            .Include(x => x.Brand)
            .Include(x => x.Category)
            .Include(x => x.Images)
            .Include(x => x.Variants).ThenInclude(v => v.Color)
            .Include(x => x.Variants).ThenInclude(v => v.Size)
            .Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(p.CategorySlug))
        {
            query = query.Where(x =>
                x.Category.Slug == p.CategorySlug ||
                (x.Category.ParentCategory != null && x.Category.ParentCategory.Slug == p.CategorySlug));
        }
        if (!string.IsNullOrWhiteSpace(p.BrandSlug))
            query = query.Where(x => x.Brand.Slug == p.BrandSlug);
        if (p.MinPrice.HasValue)
            query = query.Where(x => (x.DiscountPrice ?? x.BasePrice) >= p.MinPrice.Value);
        if (p.MaxPrice.HasValue)
            query = query.Where(x => (x.DiscountPrice ?? x.BasePrice) <= p.MaxPrice.Value);
        if (p.Gender.HasValue)
            query = query.Where(x => x.Gender == p.Gender.Value);
        if (!string.IsNullOrWhiteSpace(p.Color))
            query = query.Where(x => x.Variants.Any(v => v.Color.NameAz == p.Color || v.Color.NameRu == p.Color));
        if (!string.IsNullOrWhiteSpace(p.Size))
            query = query.Where(x => x.Variants.Any(v => v.Size.Name == p.Size));
        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var s = p.Search.Trim();
            query = query.Where(x =>
                x.NameAz.Contains(s) || x.NameRu.Contains(s) || x.SKU.Contains(s) || x.Brand.Name.Contains(s));
        }
        if (p.IsFeatured == true)
            query = query.Where(x => x.IsFeatured);

        query = (p.Sort?.ToLowerInvariant()) switch
        {
            "newest" => query.OrderByDescending(x => x.CreatedAt),
            "oldest" => query.OrderBy(x => x.CreatedAt),
            "price_asc" => query.OrderBy(x => x.DiscountPrice ?? x.BasePrice),
            "price_desc" => query.OrderByDescending(x => x.DiscountPrice ?? x.BasePrice),
            "name_asc" => query.OrderBy(x => x.NameAz),
            "name_desc" => query.OrderByDescending(x => x.NameAz),
            // Sort by total available stock across active variants — useful
            // for the admin "what needs restocking" view.
            "stock_asc" => query.OrderBy(x => x.Variants.Where(v => v.IsActive).Sum(v => v.StockQuantity)),
            "stock_desc" => query.OrderByDescending(x => x.Variants.Where(v => v.IsActive).Sum(v => v.StockQuantity)),
            "popular" => query.OrderByDescending(x => x.IsFeatured).ThenByDescending(x => x.CreatedAt),
            "relevant" => query.OrderByDescending(x => x.IsFeatured).ThenBy(x => x.NameAz),
            _ => query.OrderByDescending(x => x.IsFeatured).ThenByDescending(x => x.CreatedAt)
        };

        var page = Math.Max(1, p.Page);
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 12 : p.PageSize, 1, 100);
        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = _mapper.Map<List<ProductListItemDto>>(items);
        return new PaginatedList<ProductListItemDto>(dtos, total, page, pageSize);
    }

    public async Task<ProductDetailDto> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var product = await _uow.Products.Query()
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Variants).ThenInclude(v => v.Color)
            .Include(p => p.Variants).ThenInclude(v => v.Size)
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive, ct)
            ?? throw new NotFoundException("Məhsul");

        var dto = _mapper.Map<ProductDetailDto>(product);
        dto.AvailableColors = product.Variants
            .Where(v => v.IsActive)
            .Select(v => v.Color)
            .DistinctBy(c => c.Id)
            .Select(c => _mapper.Map<ColorDto>(c))
            .ToList();
        dto.AvailableSizes = product.Variants
            .Where(v => v.IsActive)
            .Select(v => v.Size)
            .DistinctBy(s => s.Id)
            .OrderBy(s => s.SortOrder)
            .Select(s => _mapper.Map<SizeDto>(s))
            .ToList();
        return dto;
    }

    public async Task<ProductDetailDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        if (!await _uow.Brands.AnyAsync(b => b.Id == request.BrandId, ct))
            throw new NotFoundException("Brend");
        if (!await _uow.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
            throw new NotFoundException("Kateqoriya");

        var slug = SlugHelper.EnsureUnique(SlugHelper.Slugify(request.NameAz),
            s => _uow.Products.Query().Any(p => p.Slug == s));

        var product = new Product
        {
            NameAz = request.NameAz.Trim(),
            NameRu = request.NameRu.Trim(),
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim(),
            Slug = slug,
            DescriptionAz = request.DescriptionAz,
            DescriptionRu = request.DescriptionRu,
            DescriptionEn = string.IsNullOrWhiteSpace(request.DescriptionEn) ? null : request.DescriptionEn,
            SKU = request.SKU.Trim(),
            BasePrice = request.BasePrice,
            DiscountPrice = request.DiscountPrice,
            Gender = request.Gender,
            BrandId = request.BrandId,
            CategoryId = request.CategoryId,
            IsActive = request.IsActive,
            IsFeatured = request.IsFeatured,
            Variants = request.Variants.Select(v => new ProductVariant
            {
                ColorId = v.ColorId,
                SizeId = v.SizeId,
                StockQuantity = v.StockQuantity,
                PriceAdjustment = v.PriceAdjustment,
                SKU = string.IsNullOrWhiteSpace(v.SKU) ? $"{request.SKU}-{v.ColorId}-{v.SizeId}" : v.SKU,
                IsActive = true
            }).ToList(),
            Images = request.Images.Select(i => new ProductImage
            {
                ImageUrl = i.ImageUrl,
                AltText = i.AltText,
                IsMain = i.IsMain,
                SortOrder = i.SortOrder
            }).ToList()
        };

        await _uow.Products.AddAsync(product, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetBySlugAsync(product.Slug, ct);
    }

    public async Task<ProductDetailDto> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await _uow.Products.Query()
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Məhsul");

        if (product.NameAz != request.NameAz.Trim())
        {
            product.NameAz = request.NameAz.Trim();
            product.Slug = SlugHelper.EnsureUnique(SlugHelper.Slugify(product.NameAz),
                s => _uow.Products.Query().Any(p => p.Slug == s && p.Id != id));
        }
        product.NameRu = request.NameRu.Trim();
        product.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim();
        product.DescriptionAz = request.DescriptionAz;
        product.DescriptionRu = request.DescriptionRu;
        product.DescriptionEn = string.IsNullOrWhiteSpace(request.DescriptionEn) ? null : request.DescriptionEn;
        product.SKU = request.SKU.Trim();
        product.BasePrice = request.BasePrice;
        product.DiscountPrice = request.DiscountPrice;
        product.Gender = request.Gender;
        product.BrandId = request.BrandId;
        product.CategoryId = request.CategoryId;
        product.IsActive = request.IsActive;
        product.IsFeatured = request.IsFeatured;
        product.UpdatedAt = DateTime.UtcNow;

        _uow.Products.Update(product);
        await _uow.SaveChangesAsync(ct);
        return await GetBySlugAsync(product.Slug, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(id, ct) ?? throw new NotFoundException("Məhsul");
        product.IsDeleted = true;
        product.DeletedAt = DateTime.UtcNow;
        product.IsActive = false;
        _uow.Products.Update(product);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<List<ProductImageDto>> AddImagesAsync(int productId, IEnumerable<IFormFile> files, CancellationToken ct = default)
    {
        var product = await _uow.Products.Query()
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new NotFoundException("Məhsul");

        var nextOrder = product.Images.Count;
        var added = new List<ProductImage>();
        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            var url = await _files.SaveAsync(file, $"products/{product.Id}", ct);
            var img = new ProductImage
            {
                ProductId = product.Id,
                ImageUrl = url,
                AltText = product.NameAz,
                IsMain = product.Images.Count == 0 && added.Count == 0,
                SortOrder = nextOrder++
            };
            added.Add(img);
            await _uow.ProductImages.AddAsync(img, ct);
        }
        await _uow.SaveChangesAsync(ct);
        return _mapper.Map<List<ProductImageDto>>(added);
    }

    public async Task DeleteImageAsync(int imageId, CancellationToken ct = default)
    {
        var image = await _uow.ProductImages.GetByIdAsync(imageId, ct)
            ?? throw new NotFoundException("Şəkil");
        await _files.DeleteAsync(image.ImageUrl, ct);
        _uow.ProductImages.Remove(image);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<ProductDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var product = await _uow.Products.Query()
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Variants).ThenInclude(v => v.Color)
            .Include(p => p.Variants).ThenInclude(v => v.Size)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Məhsul");

        var dto = _mapper.Map<ProductDetailDto>(product);
        dto.AvailableColors = product.Variants
            .Where(v => v.IsActive)
            .Select(v => v.Color)
            .DistinctBy(c => c.Id)
            .Select(c => _mapper.Map<ColorDto>(c))
            .ToList();
        dto.AvailableSizes = product.Variants
            .Where(v => v.IsActive)
            .Select(v => v.Size)
            .DistinctBy(s => s.Id)
            .OrderBy(s => s.SortOrder)
            .Select(s => _mapper.Map<SizeDto>(s))
            .ToList();
        return dto;
    }

    public async Task<ProductImageDto> SetMainImageAsync(int imageId, CancellationToken ct = default)
    {
        var image = await _uow.ProductImages.GetByIdAsync(imageId, ct)
            ?? throw new NotFoundException("Şəkil");
        var siblings = await _uow.ProductImages.Query()
            .Where(i => i.ProductId == image.ProductId)
            .ToListAsync(ct);
        foreach (var sib in siblings)
            sib.IsMain = sib.Id == imageId;
        await _uow.SaveChangesAsync(ct);
        return _mapper.Map<ProductImageDto>(image);
    }

    public async Task<ProductVariantDto> AddVariantAsync(int productId, AddVariantRequest request, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(productId, ct)
            ?? throw new NotFoundException("Məhsul");
        if (!await _uow.Colors.AnyAsync(c => c.Id == request.ColorId, ct))
            throw new NotFoundException("Rəng");
        if (!await _uow.Sizes.AnyAsync(s => s.Id == request.SizeId, ct))
            throw new NotFoundException("Ölçü");

        var exists = await _uow.ProductVariants.Query()
            .AnyAsync(v => v.ProductId == productId && v.ColorId == request.ColorId && v.SizeId == request.SizeId, ct);
        if (exists)
            throw new ConflictException("Bu rəng + ölçü kombinasiyası artıq mövcuddur.");

        var variant = new ProductVariant
        {
            ProductId = productId,
            ColorId = request.ColorId,
            SizeId = request.SizeId,
            StockQuantity = Math.Max(0, request.StockQuantity),
            PriceAdjustment = request.PriceAdjustment,
            SKU = string.IsNullOrWhiteSpace(request.SKU)
                ? $"{product.SKU}-{request.ColorId}-{request.SizeId}"
                : request.SKU.Trim(),
            IsActive = request.IsActive
        };
        await _uow.ProductVariants.AddAsync(variant, ct);
        await _uow.SaveChangesAsync(ct);

        var reloaded = await _uow.ProductVariants.Query()
            .Include(v => v.Color)
            .Include(v => v.Size)
            .FirstAsync(v => v.Id == variant.Id, ct);
        return _mapper.Map<ProductVariantDto>(reloaded);
    }

    public async Task<ProductVariantDto> UpdateVariantAsync(int variantId, UpdateVariantRequest request, CancellationToken ct = default)
    {
        var variant = await _uow.ProductVariants.Query()
            .Include(v => v.Color)
            .Include(v => v.Size)
            .FirstOrDefaultAsync(v => v.Id == variantId, ct)
            ?? throw new NotFoundException("Variant");

        variant.StockQuantity = Math.Max(0, request.StockQuantity);
        variant.PriceAdjustment = request.PriceAdjustment;
        if (!string.IsNullOrWhiteSpace(request.SKU))
            variant.SKU = request.SKU.Trim();
        variant.IsActive = request.IsActive;
        _uow.ProductVariants.Update(variant);
        await _uow.SaveChangesAsync(ct);
        return _mapper.Map<ProductVariantDto>(variant);
    }

    public async Task DeleteVariantAsync(int variantId, CancellationToken ct = default)
    {
        var variant = await _uow.ProductVariants.GetByIdAsync(variantId, ct)
            ?? throw new NotFoundException("Variant");
        _uow.ProductVariants.Remove(variant);
        await _uow.SaveChangesAsync(ct);
    }
}
