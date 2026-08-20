using System.Text.Json.Serialization;

namespace ECommerce.Application.DTOs;

/// <summary>
/// Payload posted by the 1C inventory exporter.
///
/// Property names are pinned with <see cref="JsonPropertyNameAttribute"/> to
/// match the EXACT casing 1C sends ("Products", "uid", "Category", "Items",
/// "Price"…), independent of the API's global camelCase JSON policy. Keep these
/// in sync with the agreed 1C contract.
/// </summary>
public class InventorySyncRequest
{
    /// <summary>Shared secret the 1C operator must include in every request.</summary>
    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("Products")]
    public List<SyncProductDto> Products { get; set; } = new();
}

public class SyncProductDto
{
    /// <summary>Stable 1C identifier — the upsert key for the product.</summary>
    [JsonPropertyName("uid")]
    public string? Uid { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("Category")]
    public SyncRefDto? Category { get; set; }

    [JsonPropertyName("Brand")]
    public SyncRefDto? Brand { get; set; }

    [JsonPropertyName("Items")]
    public List<SyncItemDto> Items { get; set; } = new();
}

/// <summary>A referenced lookup entity (category / brand / color / size) by uid + name.</summary>
public class SyncRefDto
{
    [JsonPropertyName("uid")]
    public string? Uid { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>One concrete variant line (color + size + price + stock).</summary>
public class SyncItemDto
{
    [JsonPropertyName("Color")]
    public SyncRefDto? Color { get; set; }

    [JsonPropertyName("Size")]
    public SyncRefDto? Size { get; set; }

    /// <summary>Free-text variant label from 1C — stored as the variant SKU.</summary>
    [JsonPropertyName("Item")]
    public string? Item { get; set; }

    /// <summary>
    /// 1C's stable per-variant identifier. Note the exact key is "ItemuUid"
    /// (lower-case u) as 1C sends it — kept verbatim so the mapping doesn't
    /// silently miss. Stored on the variant and echoed back on order pull.
    /// </summary>
    [JsonPropertyName("ItemuUid")]
    public string? ItemuUid { get; set; }

    [JsonPropertyName("Price")]
    public decimal Price { get; set; }

    [JsonPropertyName("Quantity")]
    public int Quantity { get; set; }
}

/// <summary>Summary of what the sync changed — returned to the caller and logged.</summary>
public class InventorySyncResultDto
{
    public int ProductsCreated { get; set; }
    public int ProductsUpdated { get; set; }
    public int ProductsResurrected { get; set; }
    public int VariantsCreated { get; set; }
    public int VariantsUpdated { get; set; }
    public int BrandsCreated { get; set; }
    public int CategoriesCreated { get; set; }
    public int ColorsCreated { get; set; }
    public int SizesCreated { get; set; }
    public int TotalProductsReceived { get; set; }
    public int TotalItemsReceived { get; set; }

    /// <summary>Per-row notes for anything skipped (missing uid/name/brand/etc.).</summary>
    public List<string> Warnings { get; set; } = new();
}
