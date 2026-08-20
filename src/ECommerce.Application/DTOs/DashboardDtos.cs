using ECommerce.Domain.Enums;

namespace ECommerce.Application.DTOs;

public class DashboardStatsDto
{
    public int TotalProducts { get; set; }
    public int ActiveProducts { get; set; }
    public int TotalOrders { get; set; }
    public int OrdersLast30Days { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal Revenue30Days { get; set; }
    public int UnreadMessages { get; set; }
    public int LowStockVariants { get; set; }
    public int TotalCustomers { get; set; }

    // ── KPI metrics ─────────────────────────────────────────────────────────
    public decimal AverageOrderValue { get; set; }
    public decimal InventoryValue { get; set; }
    public int NewCustomers30Days { get; set; }
    public decimal AverageItemsPerOrder { get; set; }
    public int RepeatCustomers { get; set; }
    public int ActiveCarts { get; set; }

    // ── Period-over-period (previous 30 days) ──────────────────────────────
    public decimal PreviousPeriodRevenue { get; set; }
    public int PreviousPeriodOrders { get; set; }
    public int PreviousPeriodNewCustomers { get; set; }

    // ── Stock health (active variants) ─────────────────────────────────────
    public int VariantsInStock { get; set; }   // > 5
    public int VariantsLowStock { get; set; }  // 1..5
    public int VariantsOutOfStock { get; set; } // 0

    // ── Breakdowns ──────────────────────────────────────────────────────────
    public Dictionary<OrderStatus, int> OrdersByStatus { get; set; } = new();
    public Dictionary<Gender, int> OrdersByGender { get; set; } = new();
    public Dictionary<PaymentMethod, int> OrdersByPaymentMethod { get; set; } = new();

    // ── Top lists ──────────────────────────────────────────────────────────
    public List<TopProductDto> TopProducts { get; set; } = new();
    public List<TopBrandDto> TopBrands { get; set; } = new();
    public List<TopCategoryDto> TopCategories { get; set; } = new();
    public List<TopCustomerDto> TopCustomers { get; set; } = new();
    public List<TopColorDto> TopColors { get; set; } = new();
    public List<LowStockProductDto> LowStockProducts { get; set; } = new();

    // ── Time series ────────────────────────────────────────────────────────
    public List<DailyRevenuePoint> Revenue30DaysChart { get; set; } = new();
    public List<HourlyOrderPoint> HourlyDistribution { get; set; } = new();
    public List<DayOfWeekPoint> DayOfWeekDistribution { get; set; } = new();
    /// <summary>7×24 grid: row = day of week (0=Sun), col = hour. Value = order count.</summary>
    public List<HeatmapCellDto> HourDayHeatmap { get; set; } = new();
    public List<RecentOrderDto> RecentOrders { get; set; } = new();

    /// <summary>
    /// Heuristic-generated "Smart Insights": narrated observations the dashboard
    /// derives from the raw data (color dominance, peak hours, growth drivers…).
    /// </summary>
    public List<InsightDto> Insights { get; set; } = new();

    /// <summary>
    /// Order counts grouped by detected Azerbaijani city — drives the
    /// dashboard's map heatmap.  Cities are extracted from <c>DeliveryAddress</c>
    /// via a known-city substring match (case- and diacritic-insensitive).
    /// </summary>
    public List<CityOrderDto> OrdersByCity { get; set; } = new();

    /// <summary>
    /// "Dead products": active products that haven't sold in the last 45 days.
    /// Sorted by days-since-last-sale descending (longest-cold first), then by
    /// stock value descending (so high-value dead inventory floats up).
    /// </summary>
    public List<DeadProductDto> DeadProducts { get; set; } = new();
}

/// <summary>
/// "Dead product" — active, in stock, but stagnating.  Used to surface
/// inventory the merchant should put on sale, photograph better, or retire.
/// </summary>
public class DeadProductDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSlug { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    /// <summary>Total stock across all active variants.</summary>
    public int StockRemaining { get; set; }
    /// <summary>Days since the product was created (helps gauge "fresh" vs "ancient").</summary>
    public int DaysSinceCreated { get; set; }
    /// <summary>Days since the last non-cancelled sale of this product, or <c>null</c> if it has never sold.</summary>
    public int? DaysSinceLastSale { get; set; }
    /// <summary>Total units ever sold (across the product's lifetime).</summary>
    public int TotalSold { get; set; }
}

/// <summary>One bubble on the Azerbaijan map.</summary>
public class CityOrderDto
{
    /// <summary>Azerbaijani name (e.g. "Bakı", "Gəncə").</summary>
    public string City { get; set; } = string.Empty;
    /// <summary>Latitude in decimal degrees.</summary>
    public double Lat { get; set; }
    /// <summary>Longitude in decimal degrees.</summary>
    public double Lng { get; set; }
    /// <summary>Non-cancelled orders in the last 30 days from this city.</summary>
    public int OrderCount { get; set; }
    /// <summary>Revenue contributed by orders from this city.</summary>
    public decimal Revenue { get; set; }
}

/// <summary>
/// A single piece of narrated analytics — the dashboard interprets the data
/// instead of just showing it.  Frontend chooses styling from <c>Tone</c>.
/// </summary>
public class InsightDto
{
    /// <summary>positive | warning | info | critical</summary>
    public string Tone { get; set; } = "info";
    /// <summary>Emoji glyph for the leading icon (e.g. "🔥", "⚠️", "📈").</summary>
    public string Icon { get; set; } = "💡";
    /// <summary>Bold one-line punch-line.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Explanatory body sentence.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Optional headline metric (e.g. "2.3×", "+18%", "20:00–23:00").</summary>
    public string? Metric { get; set; }
    /// <summary>Optional admin-panel link the insight points to.</summary>
    public string? ActionHref { get; set; }
    /// <summary>Optional CTA label paired with <see cref="ActionHref"/>.</summary>
    public string? ActionLabel { get; set; }
    /// <summary>Sort weight — higher floats to the top of the list.</summary>
    public int Priority { get; set; }
}

public class HeatmapCellDto
{
    public int DayOfWeek { get; set; } // 0..6
    public int Hour { get; set; }      // 0..23
    public int OrderCount { get; set; }
}

public class TopColorDto
{
    public int ColorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string HexCode { get; set; } = "#000000";
    public int UnitsSold { get; set; }
}

public class TopProductDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSlug { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
}

public class TopBrandDto
{
    public int BrandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
}

public class TopCategoryDto
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
}

public class TopCustomerDto
{
    public string CustomerFullName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
}

public class LowStockProductDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSlug { get; set; } = string.Empty;
    public int StockRemaining { get; set; }
    public int VariantsAtRisk { get; set; }
}

public class DailyRevenuePoint
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
    public int OrderCount { get; set; }
}

public class HourlyOrderPoint
{
    public int Hour { get; set; } // 0-23
    public int OrderCount { get; set; }
    public decimal Revenue { get; set; }
}

public class DayOfWeekPoint
{
    public int DayOfWeek { get; set; } // 0 = Sunday … 6 = Saturday
    public int OrderCount { get; set; }
    public decimal Revenue { get; set; }
}

public class RecentOrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerFullName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
