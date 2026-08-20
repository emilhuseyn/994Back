using ECommerce.Domain.Common;

namespace ECommerce.Domain.Entities;

public class Color : BaseEntity
{
    /// <summary>External system (1C) identifier — upsert key for inventory sync.</summary>
    public string? ExternalId { get; set; }

    public string NameAz { get; set; } = string.Empty;
    public string NameRu { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string HexCode { get; set; } = "#000000";

    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
}
