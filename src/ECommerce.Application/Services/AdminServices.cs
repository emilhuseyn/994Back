using AutoMapper;
using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Interfaces.Infrastructure;
using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Application.Services.Abstractions;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _uow;
    public DashboardService(IUnitOfWork uow) => _uow = uow;

    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var since = now.AddDays(-30).Date;
        const int lowStockThreshold = 5;

        var totalProducts = await _uow.Products.Query().CountAsync(ct);
        var activeProducts = await _uow.Products.Query().CountAsync(p => p.IsActive, ct);

        var orders = _uow.Orders.Query();
        var totalOrders = await orders.CountAsync(ct);
        var ordersLast30 = await orders.CountAsync(o => o.CreatedAt >= since, ct);

        // Only count revenue from non-cancelled orders
        var totalRevenue = await orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;

        var revenue30 = await orders
            .Where(o => o.Status != OrderStatus.Cancelled && o.CreatedAt >= since)
            .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;

        var statusGroups = await orders
            .GroupBy(o => o.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var ordersByStatus = statusGroups.ToDictionary(g => g.Key, g => g.Count);

        var unreadMessages = await _uow.ContactMessages.Query().CountAsync(m => !m.IsRead, ct);
        var lowStockVariants = await _uow.ProductVariants.Query()
            .CountAsync(v => v.IsActive && v.StockQuantity <= lowStockThreshold, ct);
        var totalCustomers = await _uow.Users.Query()
            .CountAsync(u => u.Role == UserRole.Customer, ct);

        // Top products by units sold in last 30 days (or all-time if no recent orders).
        var topProducts = await _uow.OrderItems.Query()
            .Where(oi => oi.Order.Status != OrderStatus.Cancelled && oi.Order.CreatedAt >= since)
            .GroupBy(oi => new
            {
                ProductId = oi.ProductVariant.ProductId,
                ProductName = oi.ProductVariant.Product.NameAz,
                ProductSlug = oi.ProductVariant.Product.Slug
            })
            .Select(g => new TopProductDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                ProductSlug = g.Key.ProductSlug,
                UnitsSold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.TotalPrice)
            })
            .OrderByDescending(p => p.UnitsSold)
            .Take(5)
            .ToListAsync(ct);

        // Attach product images
        if (topProducts.Count > 0)
        {
            var productIds = topProducts.Select(p => p.ProductId).ToList();
            var images = await _uow.ProductImages.Query()
                .Where(i => productIds.Contains(i.ProductId))
                .GroupBy(i => i.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    Url = g.OrderByDescending(i => i.IsMain).ThenBy(i => i.SortOrder)
                        .Select(i => i.ImageUrl).FirstOrDefault()
                })
                .ToListAsync(ct);
            var imageMap = images.ToDictionary(i => i.ProductId, i => i.Url);
            foreach (var p in topProducts)
                p.ImageUrl = imageMap.TryGetValue(p.ProductId, out var u) ? u : null;
        }

        // 30-day daily revenue series — fill in zeros for days without orders.
        var dailyAgg = await orders
            .Where(o => o.Status != OrderStatus.Cancelled && o.CreatedAt >= since)
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Revenue = g.Sum(o => o.TotalAmount), Count = g.Count() })
            .ToListAsync(ct);
        var dailyMap = dailyAgg.ToDictionary(d => d.Date, d => (Revenue: d.Revenue, Count: d.Count));

        var series = new List<DailyRevenuePoint>(30);
        for (var i = 29; i >= 0; i--)
        {
            var date = now.Date.AddDays(-i);
            dailyMap.TryGetValue(date, out var v);
            series.Add(new DailyRevenuePoint
            {
                Date = date,
                Revenue = v.Revenue,
                OrderCount = v.Count
            });
        }

        var recent = await orders
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .Select(o => new RecentOrderDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                CustomerFullName = o.CustomerFullName,
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync(ct);

        // ── New KPI metrics ────────────────────────────────────────────────
        var paidOrders = await orders.CountAsync(o => o.Status != OrderStatus.Cancelled, ct);
        var aov = paidOrders > 0 ? totalRevenue / paidOrders : 0m;

        var inventoryValue = await _uow.ProductVariants.Query()
            .Where(v => v.IsActive && !v.Product.IsDeleted)
            .SumAsync(
                v => (decimal?)(v.StockQuantity *
                    ((v.Product.DiscountPrice ?? v.Product.BasePrice) + v.PriceAdjustment)),
                ct) ?? 0m;

        var newCustomers30 = await _uow.Users.Query()
            .CountAsync(u => u.Role == UserRole.Customer && u.CreatedAt >= since, ct);

        var totalUnitsSold = await _uow.OrderItems.Query()
            .Where(oi => oi.Order.Status != OrderStatus.Cancelled)
            .SumAsync(oi => (int?)oi.Quantity, ct) ?? 0;
        var avgItemsPerOrder = paidOrders > 0
            ? (decimal)totalUnitsSold / paidOrders
            : 0m;

        // Repeat customer = an email that placed 2+ non-cancelled orders.
        var repeatCustomers = await orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .GroupBy(o => o.CustomerEmail)
            .Where(g => g.Count() >= 2)
            .CountAsync(ct);

        var activeCarts = await _uow.Carts.Query().CountAsync(c => c.Items.Any(), ct);

        // ── Gender + payment-method splits (non-cancelled, last 30d) ───────
        var genderGroups = await _uow.OrderItems.Query()
            .Where(oi => oi.Order.Status != OrderStatus.Cancelled && oi.Order.CreatedAt >= since)
            .GroupBy(oi => oi.ProductVariant.Product.Gender)
            .Select(g => new { g.Key, Count = g.Sum(x => x.Quantity) })
            .ToListAsync(ct);
        var ordersByGender = genderGroups.ToDictionary(g => g.Key, g => g.Count);

        var paymentGroups = await orders
            .Where(o => o.CreatedAt >= since)
            .GroupBy(o => o.PaymentMethod)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var ordersByPayment = paymentGroups.ToDictionary(g => g.Key, g => g.Count);

        // ── Top brands (last 30 days) ──────────────────────────────────────
        var topBrands = await _uow.OrderItems.Query()
            .Where(oi => oi.Order.Status != OrderStatus.Cancelled && oi.Order.CreatedAt >= since)
            .GroupBy(oi => new
            {
                Id = oi.ProductVariant.Product.BrandId,
                Name = oi.ProductVariant.Product.Brand.Name,
                Slug = oi.ProductVariant.Product.Brand.Slug,
            })
            .Select(g => new TopBrandDto
            {
                BrandId = g.Key.Id,
                Name = g.Key.Name,
                Slug = g.Key.Slug,
                UnitsSold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.TotalPrice),
            })
            .OrderByDescending(b => b.Revenue)
            .Take(5)
            .ToListAsync(ct);

        // ── Top categories (last 30 days) ──────────────────────────────────
        var topCategories = await _uow.OrderItems.Query()
            .Where(oi => oi.Order.Status != OrderStatus.Cancelled && oi.Order.CreatedAt >= since)
            .GroupBy(oi => new
            {
                Id = oi.ProductVariant.Product.CategoryId,
                Name = oi.ProductVariant.Product.Category.NameAz,
                Slug = oi.ProductVariant.Product.Category.Slug,
            })
            .Select(g => new TopCategoryDto
            {
                CategoryId = g.Key.Id,
                Name = g.Key.Name,
                Slug = g.Key.Slug,
                UnitsSold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.TotalPrice),
            })
            .OrderByDescending(c => c.Revenue)
            .Take(5)
            .ToListAsync(ct);

        // ── Top customers (all-time by spend) ──────────────────────────────
        var topCustomers = await orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .GroupBy(o => new { o.CustomerEmail, o.CustomerFullName })
            .Select(g => new TopCustomerDto
            {
                CustomerFullName = g.Key.CustomerFullName,
                CustomerEmail = g.Key.CustomerEmail,
                OrderCount = g.Count(),
                TotalSpent = g.Sum(x => x.TotalAmount),
            })
            .OrderByDescending(c => c.TotalSpent)
            .Take(5)
            .ToListAsync(ct);

        // ── Low-stock alerts: products whose total active stock ≤ threshold ─
        var lowStockProducts = await _uow.Products.Query()
            .Where(p => p.IsActive)
            .Select(p => new LowStockProductDto
            {
                ProductId = p.Id,
                ProductName = p.NameAz,
                ProductSlug = p.Slug,
                StockRemaining = p.Variants.Where(v => v.IsActive).Sum(v => v.StockQuantity),
                VariantsAtRisk = p.Variants.Count(v => v.IsActive && v.StockQuantity <= lowStockThreshold),
            })
            .Where(p => p.StockRemaining <= lowStockThreshold * 4 && p.VariantsAtRisk > 0)
            .OrderBy(p => p.StockRemaining)
            .Take(5)
            .ToListAsync(ct);

        // ── Hourly distribution (24h, last 30 days) ────────────────────────
        var hourlyAgg = await orders
            .Where(o => o.Status != OrderStatus.Cancelled && o.CreatedAt >= since)
            .GroupBy(o => o.CreatedAt.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count(), Revenue = g.Sum(o => o.TotalAmount) })
            .ToListAsync(ct);
        var hourMap = hourlyAgg.ToDictionary(h => h.Hour, h => (h.Count, h.Revenue));
        var hourlyDistribution = new List<HourlyOrderPoint>(24);
        for (var h = 0; h < 24; h++)
        {
            hourMap.TryGetValue(h, out var v);
            hourlyDistribution.Add(new HourlyOrderPoint
            {
                Hour = h,
                OrderCount = v.Count,
                Revenue = v.Revenue,
            });
        }

        // ── Day-of-week distribution (last 30 days) ────────────────────────
        var dayAgg = await orders
            .Where(o => o.Status != OrderStatus.Cancelled && o.CreatedAt >= since)
            .ToListAsync(ct);
        var dayMap = dayAgg
            .GroupBy(o => (int)o.CreatedAt.DayOfWeek)
            .ToDictionary(
                g => g.Key,
                g => (Count: g.Count(), Revenue: g.Sum(x => x.TotalAmount)));
        var dayOfWeekDistribution = new List<DayOfWeekPoint>(7);
        for (var d = 0; d < 7; d++)
        {
            dayMap.TryGetValue(d, out var v);
            dayOfWeekDistribution.Add(new DayOfWeekPoint
            {
                DayOfWeek = d,
                OrderCount = v.Count,
                Revenue = v.Revenue,
            });
        }

        // ── Previous 30-day period (for period-over-period comparison) ────
        var prevSince = since.AddDays(-30);
        var prevRevenue = await orders
            .Where(o => o.Status != OrderStatus.Cancelled
                && o.CreatedAt >= prevSince && o.CreatedAt < since)
            .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;
        var prevOrders = await orders
            .CountAsync(o => o.CreatedAt >= prevSince && o.CreatedAt < since, ct);
        var prevNewCustomers = await _uow.Users.Query()
            .CountAsync(u => u.Role == UserRole.Customer
                && u.CreatedAt >= prevSince && u.CreatedAt < since, ct);

        // ── Stock health (active variants) ────────────────────────────────
        var stockBuckets = await _uow.ProductVariants.Query()
            .Where(v => v.IsActive && v.Product.IsActive && !v.Product.IsDeleted)
            .GroupBy(v => v.StockQuantity == 0 ? "out" : v.StockQuantity <= 5 ? "low" : "in")
            .Select(g => new { Bucket = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var variantsInStock = stockBuckets.FirstOrDefault(b => b.Bucket == "in")?.Count ?? 0;
        var variantsLowStock = stockBuckets.FirstOrDefault(b => b.Bucket == "low")?.Count ?? 0;
        var variantsOutOfStock = stockBuckets.FirstOrDefault(b => b.Bucket == "out")?.Count ?? 0;

        // ── Top colors (last 30 days, by units sold) ──────────────────────
        var topColors = await _uow.OrderItems.Query()
            .Where(oi => oi.Order.Status != OrderStatus.Cancelled && oi.Order.CreatedAt >= since)
            .GroupBy(oi => new
            {
                Id = oi.ProductVariant.ColorId,
                Name = oi.ProductVariant.Color.NameAz,
                Hex = oi.ProductVariant.Color.HexCode,
            })
            .Select(g => new TopColorDto
            {
                ColorId = g.Key.Id,
                Name = g.Key.Name,
                HexCode = g.Key.Hex,
                UnitsSold = g.Sum(x => x.Quantity),
            })
            .OrderByDescending(c => c.UnitsSold)
            .Take(8)
            .ToListAsync(ct);

        // ── Hour × Day-of-week heatmap (last 30d) ─────────────────────────
        // EF Core/MySQL can't translate DayOfWeek directly, so we materialise.
        var heatmapData = await orders
            .Where(o => o.Status != OrderStatus.Cancelled && o.CreatedAt >= since)
            .Select(o => new { o.CreatedAt })
            .ToListAsync(ct);
        var heatmapMap = heatmapData
            .GroupBy(o => new { Day = (int)o.CreatedAt.DayOfWeek, Hour = o.CreatedAt.Hour })
            .ToDictionary(g => (g.Key.Day, g.Key.Hour), g => g.Count());
        var heatmap = new List<HeatmapCellDto>(7 * 24);
        for (var d = 0; d < 7; d++)
        {
            for (var h = 0; h < 24; h++)
            {
                heatmapMap.TryGetValue((d, h), out var count);
                heatmap.Add(new HeatmapCellDto { DayOfWeek = d, Hour = h, OrderCount = count });
            }
        }

        // ── "Dead products": active + in stock + no sale in last 45 days ──
        var deadCutoff = now.AddDays(-45);
        // Most recent non-cancelled order date per product (in any time range).
        var lastSaleByProduct = await _uow.OrderItems.Query()
            .Where(oi => oi.Order.Status != OrderStatus.Cancelled)
            .GroupBy(oi => oi.ProductVariant.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                LastSale = g.Max(x => x.Order.CreatedAt),
                TotalSold = g.Sum(x => x.Quantity),
            })
            .ToListAsync(ct);
        var lastSaleMap = lastSaleByProduct.ToDictionary(x => x.ProductId);

        // Eligible products: active, has stock, exists in catalog ≥ 45 days.
        // We project the data needed for the DTO directly out of the DB so
        // the in-memory join afterwards is tiny.
        var deadCandidates = await _uow.Products.Query()
            .Where(p => p.IsActive && !p.IsDeleted && p.CreatedAt <= deadCutoff)
            .Select(p => new
            {
                p.Id,
                p.NameAz,
                p.Slug,
                p.BasePrice,
                p.DiscountPrice,
                p.CreatedAt,
                BrandName = p.Brand.Name,
                StockRemaining = p.Variants.Where(v => v.IsActive).Sum(v => v.StockQuantity),
                MainImage = p.Images.OrderByDescending(i => i.IsMain).ThenBy(i => i.SortOrder)
                    .Select(i => i.ImageUrl).FirstOrDefault(),
            })
            .Where(p => p.StockRemaining > 0)
            .ToListAsync(ct);

        var deadProducts = deadCandidates
            .Select(p =>
            {
                lastSaleMap.TryGetValue(p.Id, out var sale);
                var lastSaleDate = sale?.LastSale;
                var totalSold = sale?.TotalSold ?? 0;
                // "Dead" criterion: either never sold, or last sale is older than the cutoff.
                if (lastSaleDate.HasValue && lastSaleDate.Value >= deadCutoff)
                    return null;
                return new DeadProductDto
                {
                    ProductId = p.Id,
                    ProductName = p.NameAz,
                    ProductSlug = p.Slug,
                    ImageUrl = p.MainImage,
                    BrandName = p.BrandName,
                    Price = p.DiscountPrice ?? p.BasePrice,
                    StockRemaining = p.StockRemaining,
                    DaysSinceCreated = (int)(now - p.CreatedAt).TotalDays,
                    DaysSinceLastSale = lastSaleDate.HasValue
                        ? (int)(now - lastSaleDate.Value).TotalDays
                        : null,
                    TotalSold = totalSold,
                };
            })
            .Where(x => x != null)
            .Select(x => x!)
            // Never-sold + oldest products first; high-value dead inventory floats up.
            .OrderByDescending(d => d.DaysSinceLastSale ?? d.DaysSinceCreated)
            .ThenByDescending(d => d.Price * d.StockRemaining)
            .Take(10)
            .ToList();

        // ── Orders grouped by detected Azerbaijani city (last 30 days) ────
        // We can't translate string-matching into SQL safely (diacritic
        // matching, multiple aliases per city), so we materialise just the
        // delivery addresses + monetary fields and parse in memory.  The
        // result set is tiny — at most one row per non-cancelled order in 30d.
        var addressRows = await orders
            .Where(o => o.Status != OrderStatus.Cancelled && o.CreatedAt >= since)
            .Select(o => new { o.DeliveryAddress, o.TotalAmount })
            .ToListAsync(ct);
        var ordersByCity = CityGeocoder.Aggregate(addressRows
            .Select(r => (r.DeliveryAddress, r.TotalAmount)));

        // ── Extra signals only needed for Smart Insights ──────────────────
        // Most-sold size last 14 days
        var since14 = now.AddDays(-14).Date;
        var topSize14 = await _uow.OrderItems.Query()
            .Where(oi => oi.Order.Status != OrderStatus.Cancelled
                && oi.Order.CreatedAt >= since14)
            .GroupBy(oi => oi.ProductVariant.Size.Name)
            .Select(g => new { Size = g.Key, Units = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.Units)
            .FirstOrDefaultAsync(ct);

        // Per-brand revenue: current 7d vs previous 7d (growth driver)
        var sinceWeek = now.AddDays(-7).Date;
        var prevWeek = now.AddDays(-14).Date;
        var brandWeek = await _uow.OrderItems.Query()
            .Where(oi => oi.Order.Status != OrderStatus.Cancelled
                && oi.Order.CreatedAt >= prevWeek)
            .Select(oi => new
            {
                BrandName = oi.ProductVariant.Product.Brand.Name,
                Revenue = oi.TotalPrice,
                IsCurrent = oi.Order.CreatedAt >= sinceWeek,
            })
            .ToListAsync(ct);
        var brandWeekly = brandWeek
            .GroupBy(x => x.BrandName)
            .Select(g => new
            {
                Brand = g.Key,
                Current = g.Where(x => x.IsCurrent).Sum(x => x.Revenue),
                Previous = g.Where(x => !x.IsCurrent).Sum(x => x.Revenue),
            })
            .Where(x => x.Current > 0)
            .ToList();
        var topGrowthBrand = brandWeekly
            .OrderByDescending(x => x.Current - x.Previous)
            .FirstOrDefault();

        // Previous-period AOV (compare AOV growth)
        var prevAovOrders = await orders
            .Where(o => o.Status != OrderStatus.Cancelled
                && o.CreatedAt >= prevSince && o.CreatedAt < since)
            .Select(o => o.TotalAmount)
            .ToListAsync(ct);
        var prevAov = prevAovOrders.Count > 0 ? prevAovOrders.Average() : 0m;
        var currentAovOrders = await orders
            .Where(o => o.Status != OrderStatus.Cancelled && o.CreatedAt >= since)
            .Select(o => o.TotalAmount)
            .ToListAsync(ct);
        var currentAov = currentAovOrders.Count > 0 ? currentAovOrders.Average() : 0m;

        // Cross-reference: top-selling product that's also low on stock = urgent
        var topSeller = topProducts.FirstOrDefault();
        var topSellerLowStock = topSeller != null
            ? lowStockProducts.FirstOrDefault(lp => lp.ProductId == topSeller.ProductId)
            : null;

        // ── Build the narrated insights ───────────────────────────────────
        var insights = BuildInsights(
            topColors,
            hourlyDistribution,
            dayOfWeekDistribution,
            topBrands,
            topCategories,
            revenue30,
            prevRevenue,
            currentAov,
            prevAov,
            repeatCustomers,
            totalCustomers,
            newCustomers30,
            prevNewCustomers,
            ordersLast30,
            prevOrders,
            variantsOutOfStock,
            variantsLowStock,
            topSize14?.Size,
            topSize14?.Units ?? 0,
            topGrowthBrand?.Brand,
            topGrowthBrand?.Current ?? 0m,
            topGrowthBrand?.Previous ?? 0m,
            topSeller,
            topSellerLowStock);

        return new DashboardStatsDto
        {
            TotalProducts = totalProducts,
            ActiveProducts = activeProducts,
            TotalOrders = totalOrders,
            OrdersLast30Days = ordersLast30,
            TotalRevenue = totalRevenue,
            Revenue30Days = revenue30,
            UnreadMessages = unreadMessages,
            LowStockVariants = lowStockVariants,
            TotalCustomers = totalCustomers,

            AverageOrderValue = aov,
            InventoryValue = inventoryValue,
            NewCustomers30Days = newCustomers30,
            AverageItemsPerOrder = avgItemsPerOrder,
            RepeatCustomers = repeatCustomers,
            ActiveCarts = activeCarts,

            PreviousPeriodRevenue = prevRevenue,
            PreviousPeriodOrders = prevOrders,
            PreviousPeriodNewCustomers = prevNewCustomers,

            VariantsInStock = variantsInStock,
            VariantsLowStock = variantsLowStock,
            VariantsOutOfStock = variantsOutOfStock,

            OrdersByStatus = ordersByStatus,
            OrdersByGender = ordersByGender,
            OrdersByPaymentMethod = ordersByPayment,

            TopProducts = topProducts,
            TopBrands = topBrands,
            TopCategories = topCategories,
            TopCustomers = topCustomers,
            TopColors = topColors,
            LowStockProducts = lowStockProducts,

            Revenue30DaysChart = series,
            HourlyDistribution = hourlyDistribution,
            DayOfWeekDistribution = dayOfWeekDistribution,
            HourDayHeatmap = heatmap,
            RecentOrders = recent,

            Insights = insights,
            OrdersByCity = ordersByCity,
            DeadProducts = deadProducts,
        };
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Smart Insights builder
    // ═════════════════════════════════════════════════════════════════════════

    private static readonly string[] DayNamesAz =
    {
        "Bazar", "Bazar ertəsi", "Çərşənbə axşamı", "Çərşənbə",
        "Cümə axşamı", "Cümə", "Şənbə",
    };

    /// <summary>
    /// Generates the list of narrated observations from already-computed signals.
    /// Each heuristic only fires when its data is meaningful (e.g. enough orders,
    /// a real winner-vs-rest gap, a non-zero comparison baseline).  Result is
    /// sorted by descending priority and capped at 8 cards.
    /// </summary>
    private static List<InsightDto> BuildInsights(
        List<TopColorDto> topColors,
        List<HourlyOrderPoint> hourly,
        List<DayOfWeekPoint> dayOfWeek,
        List<TopBrandDto> topBrands,
        List<TopCategoryDto> topCategories,
        decimal revenue30,
        decimal prevRevenue,
        decimal currentAov,
        decimal prevAov,
        int repeatCustomers,
        int totalCustomers,
        int newCustomers30,
        int prevNewCustomers,
        int ordersLast30,
        int prevOrders,
        int variantsOutOfStock,
        int variantsLowStock,
        string? topSize,
        int topSizeUnits,
        string? growthBrand,
        decimal growthBrandCurrent,
        decimal growthBrandPrevious,
        TopProductDto? topSeller,
        LowStockProductDto? topSellerLowStock)
    {
        var list = new List<InsightDto>();

        // ── 1. Stock urgency: top-seller is running out ───────────────────
        if (topSellerLowStock != null && topSeller != null)
        {
            list.Add(new InsightDto
            {
                Tone = topSellerLowStock.StockRemaining == 0 ? "critical" : "warning",
                Icon = topSellerLowStock.StockRemaining == 0 ? "🚨" : "⚠️",
                Title = topSellerLowStock.StockRemaining == 0
                    ? $"{topSeller.ProductName} stoku bitib"
                    : $"{topSeller.ProductName} stokda azalır",
                Description = topSellerLowStock.StockRemaining == 0
                    ? "Son 30 günün ən çox satılan məhsuludur. Təcili yenilənməsi tövsiyə olunur."
                    : $"Son 30 günün ən çox satılan məhsulu — yalnız {topSellerLowStock.StockRemaining} ədəd qalıb.",
                Metric = topSellerLowStock.StockRemaining == 0
                    ? "0 ədəd"
                    : $"{topSellerLowStock.StockRemaining} ədəd",
                ActionHref = $"/admin/products/{topSeller.ProductId}/variants",
                ActionLabel = "Variantlara bax",
                Priority = 100,
            });
        }
        else if (variantsOutOfStock > 0)
        {
            list.Add(new InsightDto
            {
                Tone = "critical",
                Icon = "🚨",
                Title = $"{variantsOutOfStock} variant stoku bitib",
                Description = "Müştərilər bu variantları sifariş edə bilmir. Yenilənmə tələb olunur.",
                Metric = $"{variantsOutOfStock}",
                ActionHref = "/admin/products",
                ActionLabel = "Məhsullara bax",
                Priority = 90,
            });
        }
        else if (variantsLowStock >= 3)
        {
            list.Add(new InsightDto
            {
                Tone = "warning",
                Icon = "⚠️",
                Title = $"{variantsLowStock} variantda az stok qalıb",
                Description = "5 və ya daha az ədəd qalan variantlar — yaxın günlərdə bitə bilər.",
                Metric = $"{variantsLowStock}",
                ActionHref = "/admin/products",
                ActionLabel = "Sifariş ver",
                Priority = 70,
            });
        }

        // ── 2. Revenue trend (vs previous 30d) ────────────────────────────
        if (prevRevenue > 0)
        {
            var deltaPct = (double)((revenue30 - prevRevenue) / prevRevenue) * 100.0;
            if (Math.Abs(deltaPct) >= 5)
            {
                list.Add(new InsightDto
                {
                    Tone = deltaPct > 0 ? "positive" : "warning",
                    Icon = deltaPct > 0 ? "📈" : "📉",
                    Title = deltaPct > 0
                        ? $"Gəlir əvvəlki 30 günə nisbətən {deltaPct:F0}% artıb"
                        : $"Gəlir əvvəlki 30 günə nisbətən {Math.Abs(deltaPct):F0}% azalıb",
                    Description = deltaPct > 0
                        ? "Trayektoriya yuxarıdır — eyni marketinq strategiyasını davam etdirməyə dəyər."
                        : "Bu dövrdə nələrin dəyişdiyini araşdırmaq tövsiyə olunur (kampaniyalar, qiymət, stok).",
                    Metric = $"{(deltaPct > 0 ? "+" : "")}{deltaPct:F0}%",
                    Priority = 85,
                });
            }
        }
        else if (revenue30 > 0)
        {
            list.Add(new InsightDto
            {
                Tone = "positive",
                Icon = "🎉",
                Title = "İlk 30 günün gəliri qeydə alındı",
                Description = "Əvvəlki dövrdə satış olmadığı üçün müqayisə yoxdur. Yaxşı başlanğıcdır!",
                Metric = revenue30.ToString("0") + " ₼",
                Priority = 50,
            });
        }

        // ── 3. Peak ordering window (3-hour block with most orders) ───────
        var totalOrders30 = hourly.Sum(h => h.OrderCount);
        if (totalOrders30 >= 5 && hourly.Count == 24)
        {
            // Find best 3-hour block (e.g., 20:00–23:00).
            var bestStart = 0;
            var bestSum = 0;
            for (var i = 0; i < 24; i++)
            {
                var sum = hourly[i].OrderCount
                    + hourly[(i + 1) % 24].OrderCount
                    + hourly[(i + 2) % 24].OrderCount;
                if (sum > bestSum) { bestSum = sum; bestStart = i; }
            }
            if (bestSum > 0)
            {
                var pct = (double)bestSum / totalOrders30 * 100.0;
                var endHour = (bestStart + 3) % 24;
                if (pct >= 20)
                {
                    list.Add(new InsightDto
                    {
                        Tone = "info",
                        Icon = "🕐",
                        Title = $"Sifariş piki: saat {bestStart:00}:00–{endHour:00}:00",
                        Description = $"Son 30 günün bütün sifarişlərinin {pct:F0}%-i bu üç saatlıq pəncərədə verilib.",
                        Metric = $"{bestStart:00}–{endHour:00}",
                        Priority = 60,
                    });
                }
            }
        }

        // ── 4. Best day of week ───────────────────────────────────────────
        if (dayOfWeek.Sum(d => d.OrderCount) >= 7)
        {
            var bestDay = dayOfWeek.OrderByDescending(d => d.OrderCount).First();
            var worstDay = dayOfWeek.Where(d => d.OrderCount > 0)
                .OrderBy(d => d.OrderCount).FirstOrDefault();
            if (bestDay.OrderCount > 0 && worstDay != null
                && bestDay.OrderCount >= worstDay.OrderCount * 2)
            {
                list.Add(new InsightDto
                {
                    Tone = "info",
                    Icon = "📅",
                    Title = $"Ən aktiv gün: {DayNamesAz[bestDay.DayOfWeek]}",
                    Description = $"Bu gün orta hesabla digər günlərdən {(double)bestDay.OrderCount / worstDay.OrderCount:F1}× çox sifariş gəlir.",
                    Metric = DayNamesAz[bestDay.DayOfWeek],
                    Priority = 55,
                });
            }
        }

        // ── 5. Color dominance ────────────────────────────────────────────
        if (topColors.Count >= 2)
        {
            var leader = topColors[0];
            var rest = topColors.Skip(1).Take(4).ToList();
            if (rest.Count > 0 && rest.Average(r => r.UnitsSold) > 0)
            {
                var ratio = leader.UnitsSold / rest.Average(r => r.UnitsSold);
                if (leader.UnitsSold >= 3 && ratio >= 1.5)
                {
                    list.Add(new InsightDto
                    {
                        Tone = "info",
                        Icon = "🎨",
                        Title = $"{leader.Name} rəngi digərlərindən {ratio:F1}× çox satılır",
                        Description = $"Son 30 gündə {leader.UnitsSold} ədəd — digər top rənglərin ortalamasından xeyli çox.",
                        Metric = $"{ratio:F1}×",
                        Priority = 65,
                    });
                }
            }
        }

        // ── 6. Top size of the last 14 days ───────────────────────────────
        if (!string.IsNullOrEmpty(topSize) && topSizeUnits >= 3)
        {
            list.Add(new InsightDto
            {
                Tone = "info",
                Icon = "👕",
                Title = $"{topSize} ölçüsü son 14 gündə ən populyardır",
                Description = $"Bu ölçüdə {topSizeUnits} ədəd satılıb. Stokda saxlamaq vacibdir.",
                Metric = topSize,
                Priority = 58,
            });
        }

        // ── 7. AOV trend ──────────────────────────────────────────────────
        if (prevAov > 0)
        {
            var aovDelta = (double)((currentAov - prevAov) / prevAov) * 100.0;
            if (Math.Abs(aovDelta) >= 10)
            {
                list.Add(new InsightDto
                {
                    Tone = aovDelta > 0 ? "positive" : "warning",
                    Icon = aovDelta > 0 ? "💰" : "🪙",
                    Title = aovDelta > 0
                        ? $"Orta sifariş dəyəri {aovDelta:F0}% artıb"
                        : $"Orta sifariş dəyəri {Math.Abs(aovDelta):F0}% azalıb",
                    Description = aovDelta > 0
                        ? $"Müştərilər sifariş başına daha çox xərcləyir — indi {currentAov:F2} ₼."
                        : $"Sifariş başına xərc azalıb — indi {currentAov:F2} ₼. Çarpaz satış kampaniyalarına dəyər.",
                    Metric = $"{(aovDelta > 0 ? "+" : "")}{aovDelta:F0}%",
                    Priority = 75,
                });
            }
        }

        // ── 8. Brand-level growth driver (last 7d vs prior 7d) ────────────
        if (!string.IsNullOrEmpty(growthBrand))
        {
            var brandDelta = growthBrandCurrent - growthBrandPrevious;
            if (brandDelta > 0 && growthBrandCurrent >= 50m)
            {
                list.Add(new InsightDto
                {
                    Tone = "positive",
                    Icon = "🚀",
                    Title = $"Bu həftə artım əsasən {growthBrand}-dən gəlib",
                    Description = growthBrandPrevious > 0
                        ? $"{growthBrand} brendi son 7 gündə {growthBrandCurrent:F0} ₼ gətirib — əvvəlki həftədən {brandDelta:F0} ₼ artıq."
                        : $"{growthBrand} brendi son 7 gündə {growthBrandCurrent:F0} ₼ gətirib — əvvəlki həftə satışı yox idi.",
                    Metric = $"+{brandDelta:F0} ₼",
                    ActionHref = $"/admin/products?search={Uri.EscapeDataString(growthBrand)}",
                    ActionLabel = "Brendə bax",
                    Priority = 80,
                });
            }
        }

        // ── 9. Top-brand concentration risk / star ────────────────────────
        if (topBrands.Count >= 2)
        {
            var leader = topBrands[0];
            var totalBrandRev = topBrands.Sum(b => b.Revenue);
            if (totalBrandRev > 0)
            {
                var share = (double)(leader.Revenue / totalBrandRev) * 100.0;
                if (share >= 50)
                {
                    list.Add(new InsightDto
                    {
                        Tone = share >= 70 ? "warning" : "info",
                        Icon = share >= 70 ? "⚖️" : "🏆",
                        Title = share >= 70
                            ? $"Gəlirin {share:F0}%-i tək {leader.Name} brendindən gəlir"
                            : $"{leader.Name} brendi gəlirin {share:F0}%-ini təşkil edir",
                        Description = share >= 70
                            ? "Tək brendə bağlılıq risklidir — çeşid genişləndirməyə dəyər."
                            : "Top brend liderliyini saxlayır — promosiyalarda ön planda saxlamağa dəyər.",
                        Metric = $"{share:F0}%",
                        Priority = share >= 70 ? 78 : 52,
                    });
                }
            }
        }

        // ── 10. Repeat customer rate ──────────────────────────────────────
        if (totalCustomers >= 10)
        {
            var rate = (double)repeatCustomers / totalCustomers * 100.0;
            if (rate >= 25)
            {
                list.Add(new InsightDto
                {
                    Tone = "positive",
                    Icon = "💚",
                    Title = $"Müştərilərin {rate:F0}%-i təkrar sifariş verir",
                    Description = $"{repeatCustomers} müştəri 2+ sifariş verib — sadiq müştəri bazası formalaşıb.",
                    Metric = $"{rate:F0}%",
                    Priority = 62,
                });
            }
            else if (rate < 10 && ordersLast30 >= 10)
            {
                list.Add(new InsightDto
                {
                    Tone = "warning",
                    Icon = "🔁",
                    Title = $"Təkrar müştəri nisbəti aşağıdır ({rate:F0}%)",
                    Description = "Yenidən qayıtma kampaniyaları (endirim, email) faydalı ola bilər.",
                    Metric = $"{rate:F0}%",
                    Priority = 48,
                });
            }
        }

        // ── 11. New customer growth ───────────────────────────────────────
        if (prevNewCustomers > 0)
        {
            var custDelta = (double)(newCustomers30 - prevNewCustomers) / prevNewCustomers * 100.0;
            if (custDelta >= 25)
            {
                list.Add(new InsightDto
                {
                    Tone = "positive",
                    Icon = "🌟",
                    Title = $"Yeni müştəri axını {custDelta:F0}% artıb",
                    Description = $"Son 30 gündə {newCustomers30} yeni müştəri qoşulub. Marketinq işləyir.",
                    Metric = $"+{custDelta:F0}%",
                    ActionHref = "/admin/users",
                    ActionLabel = "İstifadəçilərə bax",
                    Priority = 68,
                });
            }
        }

        // ── 12. Top category ──────────────────────────────────────────────
        if (topCategories.Count >= 2)
        {
            var leader = topCategories[0];
            var totalCatRev = topCategories.Sum(c => c.Revenue);
            if (totalCatRev > 0)
            {
                var share = (double)(leader.Revenue / totalCatRev) * 100.0;
                if (share >= 45)
                {
                    list.Add(new InsightDto
                    {
                        Tone = "info",
                        Icon = "🛍️",
                        Title = $"{leader.Name} kateqoriyası mağazanı çəkir",
                        Description = $"Top kateqoriyalar arasında gəlirin {share:F0}%-i bu kateqoriyadan gəlir.",
                        Metric = $"{share:F0}%",
                        Priority = 45,
                    });
                }
            }
        }

        // ── 13. Order velocity (volume trend) ─────────────────────────────
        if (prevOrders > 0)
        {
            var orderDelta = (double)(ordersLast30 - prevOrders) / prevOrders * 100.0;
            if (orderDelta >= 30)
            {
                list.Add(new InsightDto
                {
                    Tone = "positive",
                    Icon = "🔥",
                    Title = $"Sifariş sayı {orderDelta:F0}% artıb",
                    Description = $"Son 30 gündə {ordersLast30} sifariş, əvvəlki dövrdən {ordersLast30 - prevOrders} çox.",
                    Metric = $"+{orderDelta:F0}%",
                    Priority = 72,
                });
            }
        }

        // No data yet → friendly empty-state insight
        if (list.Count == 0)
        {
            list.Add(new InsightDto
            {
                Tone = "info",
                Icon = "💡",
                Title = "Hələ kifayət qədər məlumat yoxdur",
                Description = "İlk sifarişlər gəldikcə dashboard avtomatik məsləhət verəcək — rənglər, ölçülər, pik saatlar və daha çoxu.",
                Priority = 0,
            });
        }

        return list.OrderByDescending(i => i.Priority).Take(8).ToList();
    }
}

public class UserAdminService : IUserAdminService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IPasswordHasher _hasher;
    private readonly ICurrentUserService _currentUser;

    public UserAdminService(IUnitOfWork uow, IMapper mapper, IPasswordHasher hasher, ICurrentUserService currentUser)
    {
        _uow = uow;
        _mapper = mapper;
        _hasher = hasher;
        _currentUser = currentUser;
    }

    public async Task<PaginatedList<UserDto>> ListAsync(int page, int pageSize, string? search = null, int? role = null, CancellationToken ct = default)
    {
        var q = _uow.Users.Query().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(u => u.FullName.Contains(s) || u.Email.Contains(s)
                || (u.PhoneNumber != null && u.PhoneNumber.Contains(s)));
        }
        if (role.HasValue)
            q = q.Where(u => (int)u.Role == role.Value);

        q = q.OrderByDescending(u => u.CreatedAt);

        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100);
        var total = await q.CountAsync(ct);
        var items = await q.Skip((safePage - 1) * safeSize).Take(safeSize).ToListAsync(ct);
        return new PaginatedList<UserDto>(_mapper.Map<List<UserDto>>(items), total, safePage, safeSize);
    }

    public async Task<UserDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(id, ct) ?? throw new NotFoundException("İstifadəçi");
        return _mapper.Map<UserDto>(user);
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(id, ct) ?? throw new NotFoundException("İstifadəçi");

        // Prevent the only admin from demoting themselves.
        if (user.Role == UserRole.Admin && request.Role != UserRole.Admin)
        {
            var otherAdmins = await _uow.Users.Query()
                .CountAsync(u => u.Id != id && u.Role == UserRole.Admin && u.IsActive, ct);
            if (otherAdmins == 0)
                throw new ConflictException("Son aktiv admin hesabını dəyişdirmək olmaz.");
        }
        // Same protection for IsActive
        if (user.Role == UserRole.Admin && !request.IsActive)
        {
            var otherActive = await _uow.Users.Query()
                .CountAsync(u => u.Id != id && u.Role == UserRole.Admin && u.IsActive, ct);
            if (otherActive == 0)
                throw new ConflictException("Son aktiv admini deaktiv etmək olmaz.");
        }

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? null
            : request.PhoneNumber.Trim();
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);
        return _mapper.Map<UserDto>(user);
    }

    public async Task<UserDto> CreateAdminAsync(CreateAdminRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _uow.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("Bu e-poçt artıq qeydiyyatdadır.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
                ? null
                : request.PhoneNumber.Trim(),
            PasswordHash = _hasher.Hash(request.Password),
            Role = UserRole.Admin,
            IsActive = true
        };
        await _uow.Users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);
        return _mapper.Map<UserDto>(user);
    }

    public async Task DeactivateAsync(int id, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(id, ct) ?? throw new NotFoundException("İstifadəçi");
        if (_currentUser.UserId == id)
            throw new ConflictException("Öz hesabınızı deaktiv edə bilməzsiniz.");
        if (user.Role == UserRole.Admin)
        {
            var otherActive = await _uow.Users.Query()
                .CountAsync(u => u.Id != id && u.Role == UserRole.Admin && u.IsActive, ct);
            if (otherActive == 0)
                throw new ConflictException("Son aktiv admini deaktiv etmək olmaz.");
        }
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);
    }
}
