using ECommerce.Domain.Common;

namespace ECommerce.Domain.Entities;

public class Cart : BaseEntity
{
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string? SessionId { get; set; }

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}

public class CartItem : BaseEntity
{
    public int CartId { get; set; }
    public Cart Cart { get; set; } = null!;
    public int ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
