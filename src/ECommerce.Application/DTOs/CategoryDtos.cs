namespace ECommerce.Application.DTOs;

public class CategoryDto
{
    public int Id { get; set; }
    public string NameAz { get; set; } = string.Empty;
    public string NameRu { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string Slug { get; set; } = string.Empty;
    public int? ParentCategoryId { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public int ProductCount { get; set; }
}

public class CategoryTreeDto : CategoryDto
{
    public List<CategoryTreeDto> Children { get; set; } = new();
}

public class CreateCategoryRequest
{
    public string NameAz { get; set; } = string.Empty;
    public string NameRu { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public int? ParentCategoryId { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateCategoryRequest : CreateCategoryRequest { }
