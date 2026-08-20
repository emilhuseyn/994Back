using ECommerce.Domain.Common;

namespace ECommerce.Domain.Entities;

public class Brand : SoftDeletableEntity
{
    /// <summary>External system (1C) identifier — upsert key for inventory sync.</summary>
    public string? ExternalId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
