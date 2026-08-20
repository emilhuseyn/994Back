using ECommerce.Domain.Common;

namespace ECommerce.Domain.Entities;

public class ProductVariant : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int ColorId { get; set; }
    public Color Color { get; set; } = null!;
    public int SizeId { get; set; }
    public Size Size { get; set; } = null!;
    public int StockQuantity { get; set; }
    public decimal PriceAdjustment { get; set; }
    public string SKU { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 1C's per-variant identifier ("ItemuUid" in the sync payload). Stored so a
    /// placed order can be reported back to 1C with the exact line uid it knows.
    /// Null for variants created manually in admin rather than via sync.
    /// </summary>
    public string? ExternalId { get; set; }

    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
