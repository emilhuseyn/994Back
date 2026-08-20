namespace ECommerce.Application.DTOs;

/// <summary>
/// Incoming "complete-the-outfit" request from the storefront.  The product
/// id anchors the outfit; everything else is optional.
/// </summary>
public class StylistRequestDto
{
    /// <summary>Currently-viewed product (the outfit is built around this).</summary>
    public int ProductId { get; set; }

    /// <summary>
    /// Style preset chosen by the user.  Pass <c>"auto"</c> (or leave null)
    /// to let the AI choose a style that fits the anchor product.
    /// Recognised: <c>auto, streetwear, minimal, oldmoney, techwear, y2k,
    /// sporty, classic, boho, casual</c>.
    /// </summary>
    public string? Style { get; set; }

    /// <summary>Locale of the user — drives reason text language.  AZ | RUS | ENG.</summary>
    public string Locale { get; set; } = "AZ";
}

/// <summary>The four-item outfit suggested by the AI stylist.</summary>
public class StylistSuggestionDto
{
    /// <summary>
    /// Short outfit description ("Casual streetwear with edge", "Office-ready
    /// minimal"…).  Localised via the request's <c>Locale</c>.
    /// </summary>
    public string OutfitName { get; set; } = string.Empty;

    /// <summary>
    /// Up to four products, one per role (<c>top</c>, <c>bottom</c>,
    /// <c>shoes</c>, <c>accessory</c>).  Empty when the catalog is too small
    /// to cover the requested style.
    /// </summary>
    public List<StylistItemDto> Items { get; set; } = new();

    /// <summary>
    /// True when Gemini provided the suggestion, false when we fell back to
    /// a deterministic catalog walk (no AI / quota exceeded / parse error).
    /// </summary>
    public bool AiPowered { get; set; }
}

public class StylistItemDto
{
    /// <summary><c>top</c> | <c>bottom</c> | <c>shoes</c> | <c>accessory</c></summary>
    public string Role { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string ProductSlug { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    /// <summary>
    /// One-line "why this works" written by the AI in the requested locale.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
    /// <summary>True when this item is the user's anchor product.</summary>
    public bool IsAnchor { get; set; }
}
