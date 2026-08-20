using ECommerce.Domain.Enums;

namespace ECommerce.Application.DTOs;

public class ProductListItemDto
{
    public int Id { get; set; }
    public string NameAz { get; set; } = string.Empty;
    public string NameRu { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public decimal? DiscountPrice { get; set; }
    public decimal EffectivePrice { get; set; }
    public Gender Gender { get; set; }
    public int BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public string BrandSlug { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryNameAz { get; set; } = string.Empty;
    public string CategoryNameRu { get; set; } = string.Empty;
    public string? CategoryNameEn { get; set; }
    public string CategorySlug { get; set; } = string.Empty;
    public string? MainImageUrl { get; set; }
    public string? HoverImageUrl { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>AZ color names from active variants — used for client-side filtering.</summary>
    public List<string> Colors { get; set; } = new();
    /// <summary>Size names from active variants — used for client-side filtering.</summary>
    public List<string> Sizes { get; set; } = new();
}

public class ProductDetailDto : ProductListItemDto
{
    public string? DescriptionAz { get; set; }
    public string? DescriptionRu { get; set; }
    public string? DescriptionEn { get; set; }
    public List<ProductImageDto> Images { get; set; } = new();
    public List<ProductVariantDto> Variants { get; set; } = new();
    public List<ColorDto> AvailableColors { get; set; } = new();
    public List<SizeDto> AvailableSizes { get; set; } = new();
    public int TotalStock { get; set; }
}

public class ProductImageDto
{
    public int Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public bool IsMain { get; set; }
    public int SortOrder { get; set; }
}

public class ProductVariantDto
{
    public int Id { get; set; }
    public int ColorId { get; set; }
    public string ColorNameAz { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#000000";
    public int SizeId { get; set; }
    public string SizeName { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public decimal PriceAdjustment { get; set; }
    public string SKU { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class ColorDto
{
    public int Id { get; set; }
    public string NameAz { get; set; } = string.Empty;
    public string NameRu { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string HexCode { get; set; } = "#000000";
}

public class CreateColorRequest
{
    public string NameAz { get; set; } = string.Empty;
    public string NameRu { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string HexCode { get; set; } = "#000000";
}

public class UpdateColorRequest : CreateColorRequest { }

public class SizeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class CreateSizeRequest
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class UpdateSizeRequest : CreateSizeRequest { }

public class ProductQueryParameters
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public string? CategorySlug { get; set; }
    public string? BrandSlug { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public Gender? Gender { get; set; }
    public string? Color { get; set; }
    public string? Size { get; set; }
    public string? Sort { get; set; }
    public string? Search { get; set; }
    public bool? IsFeatured { get; set; }
}

public class CreateProductRequest
{
    public string NameAz { get; set; } = string.Empty;
    public string NameRu { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? DescriptionAz { get; set; }
    public string? DescriptionRu { get; set; }
    public string? DescriptionEn { get; set; }
    public string SKU { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public decimal? DiscountPrice { get; set; }
    public Gender Gender { get; set; } = Gender.Unisex;
    public int BrandId { get; set; }
    public int CategoryId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    public List<CreateVariantRequest> Variants { get; set; } = new();
    public List<CreateImageRequest> Images { get; set; } = new();
}

public class CreateVariantRequest
{
    public int ColorId { get; set; }
    public int SizeId { get; set; }
    public int StockQuantity { get; set; }
    public decimal PriceAdjustment { get; set; }
    public string? SKU { get; set; }
}

public class CreateImageRequest
{
    public string ImageUrl { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public bool IsMain { get; set; }
    public int SortOrder { get; set; }
}

public class UpdateProductRequest : CreateProductRequest { }

public class AddVariantRequest
{
    public int ColorId { get; set; }
    public int SizeId { get; set; }
    public int StockQuantity { get; set; }
    public decimal PriceAdjustment { get; set; }
    public string? SKU { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateVariantRequest
{
    public int StockQuantity { get; set; }
    public decimal PriceAdjustment { get; set; }
    public string? SKU { get; set; }
    public bool IsActive { get; set; } = true;
}
