using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Application.Services.Abstractions;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ECommerce.Application.Services;

/// <summary>
/// Upserts the product catalogue from a 1C export. Matching is by external uid:
/// an incoming product/brand/category/colour/size that already exists (by uid,
/// or by a natural key like slug/name on first contact) is updated in place;
/// otherwise it's created. Nothing is ever deleted — a partial payload only
/// touches the rows it references, so dropping an item from 1C does not wipe it
/// here. The whole batch runs in one transaction.
/// </summary>
public class InventorySyncService : IInventorySyncService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<InventorySyncService> _logger;

    public InventorySyncService(IUnitOfWork uow, ILogger<InventorySyncService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<InventorySyncResultDto> SyncAsync(InventorySyncRequest request, CancellationToken ct = default)
    {
        var result = new InventorySyncResultDto
        {
            TotalProductsReceived = request.Products?.Count ?? 0,
        };
        if (request.Products is null || request.Products.Count == 0)
            return result;

        // ---- Preload lookup tables (tracked; query filters ignored so even
        // soft-deleted rows are matched and can be resurrected) ----
        var brands = await _uow.Brands.Query(tracking: true).IgnoreQueryFilters().ToListAsync(ct);
        var categories = await _uow.Categories.Query(tracking: true).IgnoreQueryFilters().ToListAsync(ct);
        var colors = await _uow.Colors.Query(tracking: true).ToListAsync(ct);
        var sizes = await _uow.Sizes.Query(tracking: true).ToListAsync(ct);

        var brandByExt = BuildExtIndex(brands, b => b.ExternalId);
        var brandBySlug = ByKey(brands, b => b.Slug);
        var catByExt = BuildExtIndex(categories, c => c.ExternalId);
        var catBySlug = ByKey(categories, c => c.Slug);
        var colorByExt = BuildExtIndex(colors, c => c.ExternalId);
        var colorByName = ByKey(colors, c => Norm(c.NameAz));
        var sizeByExt = BuildExtIndex(sizes, s => s.ExternalId);
        var sizeByName = ByKey(sizes, s => Norm(s.Name));

        var usedSlugs = new HashSet<string>(
            await _uow.Products.Query().IgnoreQueryFilters().Select(p => p.Slug).ToListAsync(ct));
        var usedSkus = new HashSet<string>(
            await _uow.Products.Query().IgnoreQueryFilters().Select(p => p.SKU).ToListAsync(ct));
        var usedBrandSlugs = new HashSet<string>(brandBySlug.Keys);
        var usedCatSlugs = new HashSet<string>(catBySlug.Keys);

        // Preload products whose uid is in the payload (with their variants).
        var incomingUids = request.Products
            .Select(p => p.Uid?.Trim())
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u!)
            .Distinct()
            .ToList();
        var existingProducts = incomingUids.Count == 0
            ? new List<Product>()
            : await _uow.Products.Query(tracking: true).IgnoreQueryFilters()
                .Include(p => p.Variants)
                .Where(p => p.ExternalId != null && incomingUids.Contains(p.ExternalId))
                .ToListAsync(ct);
        var productByExt = ByKey(existingProducts, p => p.ExternalId!);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            // ----- local resolvers (capture the lookup dictionaries above) -----
            async Task<Brand?> ResolveBrandAsync(SyncRefDto? r)
            {
                if (r is null) return null;
                var ext = r.Uid?.Trim();
                var nm = string.IsNullOrWhiteSpace(r.Name) ? ext : r.Name!.Trim();
                if (!string.IsNullOrWhiteSpace(ext) && brandByExt.TryGetValue(ext!, out var byExt))
                    return Revive(byExt);
                if (string.IsNullOrWhiteSpace(nm)) return null;

                var slug = SlugHelper.Slugify(nm!);
                if (string.IsNullOrWhiteSpace(slug)) slug = SlugHelper.Slugify(ext ?? "brand");
                if (!string.IsNullOrWhiteSpace(slug) && brandBySlug.TryGetValue(slug, out var bySlug))
                {
                    if (!string.IsNullOrWhiteSpace(ext) && string.IsNullOrWhiteSpace(bySlug.ExternalId))
                    {
                        bySlug.ExternalId = ext;
                        brandByExt[ext!] = bySlug;
                    }
                    return Revive(bySlug);
                }

                var brand = new Brand
                {
                    Name = Truncate(nm!, 150),
                    Slug = UniqueSlug(slug, usedBrandSlugs, 170),
                    ExternalId = ext,
                    IsActive = true,
                };
                await _uow.Brands.AddAsync(brand, ct);
                brandBySlug[brand.Slug] = brand;
                if (!string.IsNullOrWhiteSpace(ext)) brandByExt[ext!] = brand;
                result.BrandsCreated++;
                return brand;
            }

            async Task<Category?> ResolveCategoryAsync(SyncRefDto? r)
            {
                if (r is null) return null;
                var ext = r.Uid?.Trim();
                var nm = string.IsNullOrWhiteSpace(r.Name) ? ext : r.Name!.Trim();
                if (!string.IsNullOrWhiteSpace(ext) && catByExt.TryGetValue(ext!, out var byExt))
                    return Revive(byExt);
                if (string.IsNullOrWhiteSpace(nm)) return null;

                var slug = SlugHelper.Slugify(nm!);
                if (string.IsNullOrWhiteSpace(slug)) slug = SlugHelper.Slugify(ext ?? "category");
                if (!string.IsNullOrWhiteSpace(slug) && catBySlug.TryGetValue(slug, out var bySlug))
                {
                    if (!string.IsNullOrWhiteSpace(ext) && string.IsNullOrWhiteSpace(bySlug.ExternalId))
                    {
                        bySlug.ExternalId = ext;
                        catByExt[ext!] = bySlug;
                    }
                    return Revive(bySlug);
                }

                var name = Truncate(nm!, 150);
                var category = new Category
                {
                    NameAz = name,
                    NameRu = name,
                    NameEn = name,
                    Slug = UniqueSlug(slug, usedCatSlugs, 170),
                    ExternalId = ext,
                    IsActive = true,
                };
                await _uow.Categories.AddAsync(category, ct);
                catBySlug[category.Slug] = category;
                if (!string.IsNullOrWhiteSpace(ext)) catByExt[ext!] = category;
                result.CategoriesCreated++;
                return category;
            }

            async Task<Color?> ResolveColorAsync(SyncRefDto? r)
            {
                if (r is null) return null;
                var ext = r.Uid?.Trim();
                var nm = string.IsNullOrWhiteSpace(r.Name) ? ext : r.Name!.Trim();
                if (!string.IsNullOrWhiteSpace(ext) && colorByExt.TryGetValue(ext!, out var byExt))
                    return byExt;
                if (string.IsNullOrWhiteSpace(nm)) return null;

                var key = Norm(nm);
                if (colorByName.TryGetValue(key, out var byName))
                {
                    if (!string.IsNullOrWhiteSpace(ext) && string.IsNullOrWhiteSpace(byName.ExternalId))
                    {
                        byName.ExternalId = ext;
                        colorByExt[ext!] = byName;
                    }
                    return byName;
                }

                var name = Truncate(nm!, 80);
                var color = new Color
                {
                    NameAz = name,
                    NameRu = name,
                    NameEn = name,
                    HexCode = InventoryHeuristics.GuessHex(nm),
                    ExternalId = ext,
                };
                await _uow.Colors.AddAsync(color, ct);
                colorByName[key] = color;
                if (!string.IsNullOrWhiteSpace(ext)) colorByExt[ext!] = color;
                result.ColorsCreated++;
                return color;
            }

            async Task<Size?> ResolveSizeAsync(SyncRefDto? r)
            {
                if (r is null) return null;
                var ext = r.Uid?.Trim();
                var nm = string.IsNullOrWhiteSpace(r.Name) ? ext : r.Name!.Trim();
                if (!string.IsNullOrWhiteSpace(ext) && sizeByExt.TryGetValue(ext!, out var byExt))
                    return byExt;
                if (string.IsNullOrWhiteSpace(nm)) return null;

                var key = Norm(nm);
                if (sizeByName.TryGetValue(key, out var byName))
                {
                    if (!string.IsNullOrWhiteSpace(ext) && string.IsNullOrWhiteSpace(byName.ExternalId))
                    {
                        byName.ExternalId = ext;
                        sizeByExt[ext!] = byName;
                    }
                    return byName;
                }

                var size = new Size
                {
                    Name = Truncate(nm!, 20),
                    SortOrder = InventoryHeuristics.GuessSizeOrder(nm),
                    ExternalId = ext,
                };
                await _uow.Sizes.AddAsync(size, ct);
                sizeByName[key] = size;
                if (!string.IsNullOrWhiteSpace(ext)) sizeByExt[ext!] = size;
                result.SizesCreated++;
                return size;
            }

            // ----------------------------- main loop -----------------------------
            foreach (var pin in request.Products)
            {
                result.TotalItemsReceived += pin.Items?.Count ?? 0;

                var uid = pin.Uid?.Trim();
                if (string.IsNullOrWhiteSpace(uid))
                {
                    result.Warnings.Add($"Atlandı: uid olmayan məhsul (\"{pin.Name}\").");
                    continue;
                }
                var name = pin.Name?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    result.Warnings.Add($"Atlandı: adı olmayan məhsul (uid {uid}).");
                    continue;
                }
                if (pin.Items is null || pin.Items.Count == 0)
                {
                    result.Warnings.Add($"Atlandı: variantı olmayan məhsul (uid {uid}).");
                    continue;
                }

                var brand = await ResolveBrandAsync(pin.Brand);
                if (brand is null)
                {
                    result.Warnings.Add($"Atlandı: brend tapılmadı/boşdur (uid {uid}).");
                    continue;
                }
                var category = await ResolveCategoryAsync(pin.Category);
                if (category is null)
                {
                    // 1C didn't supply a category (the raw Excel has no such
                    // column) — infer one from the product name so the product
                    // isn't dropped. Resolved by name → reuses a seeded category.
                    var inferred = InventoryHeuristics.GuessCategoryName(name);
                    category = await ResolveCategoryAsync(new SyncRefDto { Name = inferred });
                }
                if (category is null)
                {
                    result.Warnings.Add($"Atlandı: kateqoriya təyin olunmadı (uid {uid}).");
                    continue;
                }

                // Base price = the cheapest variant price; per-variant deltas keep
                // each line's exact price (EffectivePrice = BasePrice + adjustment).
                var prices = pin.Items.Where(i => i.Price > 0m).Select(i => i.Price).ToList();
                var basePrice = prices.Count > 0 ? prices.Min() : 0m;
                var truncatedName = Truncate(name!, 250);

                if (!productByExt.TryGetValue(uid!, out var product))
                {
                    product = new Product
                    {
                        ExternalId = uid,
                        NameAz = truncatedName,
                        NameRu = truncatedName,
                        NameEn = truncatedName,
                        Slug = UniqueSlug(SlugHelper.Slugify(name!), usedSlugs, 260),
                        SKU = UniqueSku(uid!, usedSkus),
                        BasePrice = basePrice,
                        Gender = InventoryHeuristics.GuessGender(name),
                        Brand = brand,
                        Category = category,
                        IsActive = true,
                    };
                    if (brand.Id != 0) product.BrandId = brand.Id;
                    if (category.Id != 0) product.CategoryId = category.Id;
                    await _uow.Products.AddAsync(product, ct);
                    productByExt[uid!] = product;
                    result.ProductsCreated++;
                }
                else
                {
                    if (product.IsDeleted)
                    {
                        product.IsDeleted = false;
                        product.DeletedAt = null;
                        result.ProductsResurrected++;
                    }
                    // 1C is authoritative for the raw name; keep an admin-entered
                    // English translation if one already exists.
                    product.NameAz = truncatedName;
                    product.NameRu = truncatedName;
                    if (string.IsNullOrWhiteSpace(product.NameEn)) product.NameEn = truncatedName;
                    product.BasePrice = basePrice;
                    product.Brand = brand;
                    if (brand.Id != 0) product.BrandId = brand.Id;
                    product.Category = category;
                    if (category.Id != 0) product.CategoryId = category.Id;
                    // Don't touch IsActive on update — respect a visibility choice
                    // the admin made after the product was first imported. (New
                    // products are created active, so they still go live on sync.)
                    result.ProductsUpdated++;
                }

                var seenCombos = new HashSet<(Color, Size)>();
                foreach (var item in pin.Items)
                {
                    var color = await ResolveColorAsync(item.Color);
                    var size = await ResolveSizeAsync(item.Size);
                    if (color is null || size is null)
                    {
                        result.Warnings.Add($"Atlandı: rəng/ölçü yoxdur (uid {uid}, sətir \"{item.Item}\").");
                        continue;
                    }
                    if (!seenCombos.Add((color, size)))
                        continue; // same colour+size twice in one product → keep first

                    var qty = Math.Max(0, item.Quantity);
                    var priceAdj = item.Price > 0m ? Math.Max(0m, item.Price - basePrice) : 0m;
                    var sku = !string.IsNullOrWhiteSpace(item.Item)
                        ? Truncate(item.Item!.Trim(), 120)
                        : Truncate($"{uid}-{color.Id}-{size.Id}", 120);
                    var itemExt = string.IsNullOrWhiteSpace(item.ItemuUid)
                        ? null
                        : Truncate(item.ItemuUid!.Trim(), 100);

                    // Match an existing variant only when both lookup rows are
                    // already persisted (a brand-new colour/size can't be on one).
                    ProductVariant? variant = null;
                    if (product.Id != 0 && color.Id != 0 && size.Id != 0)
                        variant = product.Variants.FirstOrDefault(v => v.ColorId == color.Id && v.SizeId == size.Id);

                    if (variant is null)
                    {
                        variant = new ProductVariant
                        {
                            Product = product,
                            Color = color,
                            Size = size,
                            StockQuantity = qty,
                            PriceAdjustment = priceAdj,
                            SKU = sku,
                            ExternalId = itemExt,
                            IsActive = true,
                        };
                        if (color.Id != 0) variant.ColorId = color.Id;
                        if (size.Id != 0) variant.SizeId = size.Id;
                        product.Variants.Add(variant); // tracked parent → EF inserts it
                        result.VariantsCreated++;
                    }
                    else
                    {
                        variant.StockQuantity = qty;
                        variant.PriceAdjustment = priceAdj;
                        if (!string.IsNullOrWhiteSpace(item.Item))
                            variant.SKU = Truncate(item.Item!.Trim(), 120);
                        // Refresh the uid only when 1C sends one — a later partial
                        // payload that omits it must not wipe a known uid.
                        if (itemExt is not null)
                            variant.ExternalId = itemExt;
                        variant.IsActive = true;
                        result.VariantsUpdated++;
                    }
                }
            }

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            _logger.LogError(ex, "Inventory sync failed; transaction rolled back.");
            throw;
        }

        _logger.LogInformation(
            "Inventory sync OK. Products +{Created} ~{Updated} (resurrected {Res}); variants +{VC} ~{VU}; " +
            "new lookups — brands {B}, categories {C}, colours {Col}, sizes {S}; warnings {W}.",
            result.ProductsCreated, result.ProductsUpdated, result.ProductsResurrected,
            result.VariantsCreated, result.VariantsUpdated,
            result.BrandsCreated, result.CategoriesCreated, result.ColorsCreated, result.SizesCreated,
            result.Warnings.Count);

        return result;
    }

    public async Task<SyncPagedResult<SyncOrderDto>> GetOrdersAsync(
        int page, int pageSize, DateTime? since, CancellationToken ct = default)
    {
        var p = Math.Max(1, page);
        var size = Math.Clamp(pageSize <= 0 ? 100 : pageSize, 1, 500);

        // IgnoreQueryFilters so an order line still resolves its product's
        // ExternalId/SKU even if that product was later soft-deleted.
        var query = _uow.Orders.Query()
            .IgnoreQueryFilters()
            .Include(o => o.Items).ThenInclude(i => i.ProductVariant).ThenInclude(v => v.Product)
            .AsQueryable();

        if (since.HasValue)
        {
            var s = since.Value;
            query = query.Where(o => o.CreatedAt >= s || (o.UpdatedAt != null && o.UpdatedAt >= s));
        }

        query = query.OrderByDescending(o => o.CreatedAt);

        var total = await query.CountAsync(ct);
        var orders = await query.Skip((p - 1) * size).Take(size).ToListAsync(ct);

        var items = orders.Select(o => new SyncOrderDto
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            Status = o.Status.ToString(),
            PaymentStatus = o.PaymentStatus.ToString(),
            PaymentMethod = o.PaymentMethod.ToString(),
            TotalAmount = o.TotalAmount,
            UserId = o.UserId,
            CustomerFullName = o.CustomerFullName,
            CustomerEmail = o.CustomerEmail,
            CustomerPhone = o.CustomerPhone,
            DeliveryAddress = o.DeliveryAddress,
            Notes = o.Notes,
            CreatedAt = o.CreatedAt,
            UpdatedAt = o.UpdatedAt,
            Items = o.Items.Select(i => new SyncOrderItemDto
            {
                ProductExternalId = i.ProductVariant?.Product?.ExternalId,
                ItemExternalId = i.ProductVariant?.ExternalId,
                ProductName = i.ProductName,
                ProductSku = i.ProductVariant?.Product?.SKU,
                VariantSku = i.ProductVariant?.SKU,
                ColorName = i.ColorName,
                SizeName = i.SizeName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice,
            }).ToList(),
        }).ToList();

        return new SyncPagedResult<SyncOrderDto>
        {
            Page = p,
            PageSize = size,
            Total = total,
            TotalPages = (int)Math.Ceiling(total / (double)size),
            Items = items,
        };
    }

    public async Task<SyncPagedResult<SyncUserDto>> GetUsersAsync(
        int page, int pageSize, DateTime? since, CancellationToken ct = default)
    {
        var p = Math.Max(1, page);
        var size = Math.Clamp(pageSize <= 0 ? 100 : pageSize, 1, 500);

        var query = _uow.Users.Query();
        if (since.HasValue)
        {
            var s = since.Value;
            query = query.Where(u => u.CreatedAt >= s || (u.UpdatedAt != null && u.UpdatedAt >= s));
        }
        query = query.OrderByDescending(u => u.CreatedAt);

        var total = await query.CountAsync(ct);
        var users = await query.Skip((p - 1) * size).Take(size).ToListAsync(ct);

        // Order counts for this page in a single grouped query.
        var ids = users.Select(u => u.Id).ToList();
        var countMap = (await _uow.Orders.Query()
                .Where(o => o.UserId != null && ids.Contains(o.UserId.Value))
                .GroupBy(o => o.UserId!.Value)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToListAsync(ct))
            .ToDictionary(x => x.UserId, x => x.Count);

        var items = users.Select(u => new SyncUserDto
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email,
            PhoneNumber = u.PhoneNumber,
            Role = u.Role.ToString(),
            IsActive = u.IsActive,
            IsEmailVerified = u.IsEmailVerified,
            OrderCount = countMap.TryGetValue(u.Id, out var c) ? c : 0,
            CreatedAt = u.CreatedAt,
        }).ToList();

        return new SyncPagedResult<SyncUserDto>
        {
            Page = p,
            PageSize = size,
            Total = total,
            TotalPages = (int)Math.Ceiling(total / (double)size),
            Items = items,
        };
    }

    // ----------------------------- helpers -----------------------------

    private static T Revive<T>(T entity) where T : Domain.Common.SoftDeletableEntity
    {
        if (entity.IsDeleted)
        {
            entity.IsDeleted = false;
            entity.DeletedAt = null;
        }
        return entity;
    }

    private static Dictionary<string, T> BuildExtIndex<T>(IEnumerable<T> items, Func<T, string?> ext)
    {
        var d = new Dictionary<string, T>();
        foreach (var it in items)
        {
            var k = ext(it);
            if (!string.IsNullOrWhiteSpace(k)) d[k!] = it; // last wins on duplicate uid
        }
        return d;
    }

    private static Dictionary<string, T> ByKey<T>(IEnumerable<T> items, Func<T, string> key)
    {
        var d = new Dictionary<string, T>();
        foreach (var it in items)
        {
            var k = key(it);
            if (!string.IsNullOrWhiteSpace(k)) d[k] = it; // first wins; collisions are pre-existing data
        }
        return d;
    }

    private static string Norm(string? s) => (s ?? string.Empty).Trim().ToUpperInvariant();

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max);

    private static string UniqueSlug(string baseSlug, HashSet<string> used, int maxBase)
    {
        var b = string.IsNullOrWhiteSpace(baseSlug) ? "item" : baseSlug;
        if (b.Length > maxBase) b = b.Substring(0, maxBase);
        var slug = b;
        var i = 2;
        while (used.Contains(slug)) slug = $"{b}-{i++}";
        used.Add(slug);
        return slug;
    }

    private static string UniqueSku(string baseSku, HashSet<string> used)
    {
        var b = string.IsNullOrWhiteSpace(baseSku) ? "SKU" : baseSku;
        if (b.Length > 95) b = b.Substring(0, 95);
        var sku = b;
        var i = 2;
        while (used.Contains(sku)) sku = $"{b}-{i++}";
        used.Add(sku);
        return sku;
    }
}
