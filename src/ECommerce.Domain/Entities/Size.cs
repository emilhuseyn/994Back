using ECommerce.Domain.Common;

namespace ECommerce.Domain.Entities;

public class Size : BaseEntity
{
    /// <summary>External system (1C) identifier — upsert key for inventory sync.</summary>
    public string? ExternalId { get; set; }

    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
}
