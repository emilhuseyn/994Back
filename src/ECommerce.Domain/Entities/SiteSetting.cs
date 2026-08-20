using ECommerce.Domain.Common;

namespace ECommerce.Domain.Entities;

public class SiteSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string? ValueAz { get; set; }
    public string? ValueRu { get; set; }
    public string? ValueEn { get; set; }
}
