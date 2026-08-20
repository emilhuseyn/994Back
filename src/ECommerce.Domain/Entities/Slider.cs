using ECommerce.Domain.Common;

namespace ECommerce.Domain.Entities;

public class Slider : BaseEntity
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
