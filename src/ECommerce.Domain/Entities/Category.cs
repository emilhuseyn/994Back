using ECommerce.Domain.Common;

namespace ECommerce.Domain.Entities;

public class Category : SoftDeletableEntity
{
    /// <summary>External system (1C) identifier — upsert key for inventory sync.</summary>
    public string? ExternalId { get; set; }

    public string NameAz { get; set; } = string.Empty;
    public string NameRu { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string Slug { get; set; } = string.Empty;
    public int? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Category> Children { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
