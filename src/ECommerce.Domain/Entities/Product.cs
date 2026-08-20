using ECommerce.Domain.Common;
using ECommerce.Domain.Enums;

namespace ECommerce.Domain.Entities;

public class Product : SoftDeletableEntity
{
    /// <summary>
    /// Stable identifier from the external system (1C). Used by the inventory
    /// sync to match an incoming product to an existing row (upsert key).
    /// Null for products created manually in the admin panel.
    /// </summary>
    public string? ExternalId { get; set; }

    public string NameAz { get; set; } = string.Empty;
    public string NameRu { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string? DescriptionAz { get; set; }
    public string? DescriptionRu { get; set; }
    public string? DescriptionEn { get; set; }
    public string SKU { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public decimal? DiscountPrice { get; set; }
    public Gender Gender { get; set; } = Gender.Unisex;
    public int BrandId { get; set; }
    public Brand Brand { get; set; } = null!;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();

    public decimal EffectivePrice => DiscountPrice ?? BasePrice;
}
