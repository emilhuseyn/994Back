using ECommerce.Domain.Enums;

namespace ECommerce.Application.DTOs;

public class WishlistItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSlug { get; set; } = string.Empty;
    public decimal EffectivePrice { get; set; }
    public string? MainImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ContactMessageDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateContactMessageRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class SliderDto
{
    public int Id { get; set; }
    public string TitleAz { get; set; } = string.Empty;
    public string TitleRu { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string? SubtitleAz { get; set; }
    public string? SubtitleRu { get; set; }
    public string? SubtitleEn { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? ButtonTextAz { get; set; }
    public string? ButtonTextRu { get; set; }
    public string? ButtonTextEn { get; set; }
    public string? ButtonUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public class CreateSliderRequest
{
    public string TitleAz { get; set; } = string.Empty;
    public string TitleRu { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string? SubtitleAz { get; set; }
    public string? SubtitleRu { get; set; }
    public string? SubtitleEn { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? ButtonTextAz { get; set; }
    public string? ButtonTextRu { get; set; }
    public string? ButtonTextEn { get; set; }
    public string? ButtonUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateSliderRequest : CreateSliderRequest { }

public class SiteSettingDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string? ValueAz { get; set; }
    public string? ValueRu { get; set; }
    public string? ValueEn { get; set; }
}

public class UpdateSiteSettingRequest
{
    public string? ValueAz { get; set; }
    public string? ValueRu { get; set; }
    public string? ValueEn { get; set; }
}

public class FiltersDto
{
    public List<CategoryDto> Categories { get; set; } = new();
    public List<BrandDto> Brands { get; set; } = new();
    public List<ColorDto> Colors { get; set; } = new();
    public List<SizeDto> Sizes { get; set; } = new();
    public List<GenderDto> Genders { get; set; } = new();
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
}

public class GenderDto
{
    public Gender Value { get; set; }
    public string NameAz { get; set; } = string.Empty;
    public string NameRu { get; set; } = string.Empty;
}
