using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Interfaces.Infrastructure;
using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Application.Services.Abstractions;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ECommerce.Application.Services;

/// <summary>
/// AI stylist — builds a 4-card outfit (top + bottom + shoes + accessory)
/// around a product the user is viewing.
///
/// Flow per request:
///   1. Resolve anchor product + detect its role (top/bottom/shoes/accessory).
///   2. Pull candidate products per role (≤ 15 each, active, in-stock, same
///      gender or unisex).  This caps prompt size at ~5K tokens.
///   3. Build a localised prompt asking Gemini to pick exactly one product
///      per *other* role (anchor fills its own slot) plus a short reason.
///   4. Call Gemini.  Parse JSON.  Validate slugs against DB.
///   5. Cache the response by hash(productId, style, locale) for 24h so the
///      free Gemini tier (1500 req/day) easily covers a small shop.
///
/// If Gemini is not configured or returns junk, we fall back to a
/// deterministic catalog walk (pick the highest-priced in-stock product per
/// missing role) so the UI never breaks.
/// </summary>
public class StylistService : IStylistService
{
    private readonly IUnitOfWork _uow;
    private readonly IGeminiClient _gemini;
    private readonly IMemoryCache _cache;
    private readonly ILogger<StylistService> _logger;

    // Subcategory slugs we classify as bottoms.  Everything else under
    // `geyimler` is treated as a top.  Dresses (paltar) are a separate role
    // — when the anchor is a dress we skip the bottom slot and double up on
    // accessories instead.
    private static readonly HashSet<string> BottomSubSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "jinsler", "sortlar", "leggins", "yubka",
    };
    private const string DressSubSlug = "paltar";

    // Role enum as strings to keep JSON contract obvious.
    private const string RoleTop = "top";
    private const string RoleBottom = "bottom";
    private const string RoleShoes = "shoes";
    private const string RoleAccessory = "accessory";

    private static readonly HashSet<string> KnownStyles = new(StringComparer.OrdinalIgnoreCase)
    {
        "auto", "streetwear", "minimal", "oldmoney", "techwear",
        "y2k", "sporty", "classic", "boho", "casual",
    };

    public StylistService(
        IUnitOfWork uow,
        IGeminiClient gemini,
        IMemoryCache cache,
        ILogger<StylistService> logger)
    {
        _uow = uow;
        _gemini = gemini;
        _cache = cache;
        _logger = logger;
    }

    public async Task<StylistSuggestionDto> SuggestAsync(
        StylistRequestDto request,
        CancellationToken ct = default)
    {
        var style = NormaliseStyle(request.Style);
        var locale = NormaliseLocale(request.Locale);
        var cacheKey = $"stylist:{request.ProductId}:{style}:{locale}";

        if (_cache.TryGetValue<StylistSuggestionDto>(cacheKey, out var cached) && cached is not null)
            return cached;

        var anchor = await LoadProductAsync(request.ProductId, ct);
        if (anchor is null) throw new NotFoundException("Məhsul");

        var anchorRole = DetectRole(anchor);
        var candidates = await LoadCandidatesAsync(anchor, ct);

        // Build the suggestion via AI when possible, otherwise fall back to
        // the deterministic catalog walk.
        var suggestion = await TryGenerateWithAiAsync(
            anchor, anchorRole, candidates, style, locale, ct);

        if (suggestion is null || suggestion.Items.Count == 0)
        {
            suggestion = BuildFallback(anchor, anchorRole, candidates, locale);
        }

        // Aggressive 24h cache — same product+style+locale will reuse this
        // exact suggestion until something changes.  Memory pressure is
        // bounded by (# products × 10 styles × 3 locales).
        _cache.Set(cacheKey, suggestion, TimeSpan.FromHours(24));
        return suggestion;
    }

    // ─── Anchor + candidate loading ─────────────────────────────────────

    private async Task<Product?> LoadProductAsync(int id, CancellationToken ct)
    {
        return await _uow.Products.Query()
            .Include(p => p.Brand)
            .Include(p => p.Category)
                .ThenInclude(c => c.ParentCategory)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .Where(p => p.Id == id && !p.IsDeleted && p.IsActive)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Candidate pool: 15 products per role (top / bottom / shoes /
    /// accessory), excluding the anchor itself.  We bias toward popular
    /// items (featured first, then newest) so the LLM has good options.
    /// </summary>
    private async Task<List<CandidateProduct>> LoadCandidatesAsync(
        Product anchor, CancellationToken ct)
    {
        // We fetch a wider net (all active in-stock products in the matching
        // gender) and bucket them in memory — categorisation needs the
        // parent-category slug walk which is hairy in SQL.
        var raw = await _uow.Products.Query()
            .Include(p => p.Brand)
            .Include(p => p.Category)
                .ThenInclude(c => c.ParentCategory)
            .Include(p => p.Images)
            .Include(p => p.Variants)
                .ThenInclude(v => v.Color)
            .Where(p => p.Id != anchor.Id
                && p.IsActive
                && !p.IsDeleted
                && p.Variants.Any(v => v.IsActive && v.StockQuantity > 0)
                && (p.Gender == anchor.Gender || p.Gender == Domain.Enums.Gender.Unisex))
            .OrderByDescending(p => p.IsFeatured)
            .ThenByDescending(p => p.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        var buckets = new Dictionary<string, List<CandidateProduct>>(StringComparer.OrdinalIgnoreCase)
        {
            [RoleTop] = new(),
            [RoleBottom] = new(),
            [RoleShoes] = new(),
            [RoleAccessory] = new(),
        };
        foreach (var p in raw)
        {
            var role = DetectRole(p);
            if (role is null) continue;
            if (buckets[role].Count >= 15) continue;
            buckets[role].Add(new CandidateProduct(
                p.Id, p.Slug, p.NameAz, p.Brand.Name,
                p.DiscountPrice ?? p.BasePrice,
                ColourSummary(p),
                MainImage(p),
                role));
        }
        return buckets.Values.SelectMany(b => b).ToList();
    }

    // ─── Role classification ────────────────────────────────────────────

    private static string? DetectRole(Product p)
    {
        var parent = p.Category.ParentCategory?.Slug ?? p.Category.Slug;
        return parent switch
        {
            "ayaqqabilar" => RoleShoes,
            "aksesuarlar" => RoleAccessory,
            "geyimler" => BottomSubSlugs.Contains(p.Category.Slug) ? RoleBottom : RoleTop,
            _ => null,
        };
    }

    private static string? MainImage(Product p) =>
        p.Images.OrderByDescending(i => i.IsMain).ThenBy(i => i.SortOrder)
            .Select(i => i.ImageUrl).FirstOrDefault();

    /// <summary>Names of colours stocked for this product — feeds the LLM prompt.</summary>
    private static string ColourSummary(Product p)
    {
        var colours = p.Variants
            .Where(v => v.IsActive && v.StockQuantity > 0)
            .Select(v => v.Color?.NameAz)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .Take(3);
        return string.Join(", ", colours!);
    }

    // ─── Gemini path ────────────────────────────────────────────────────

    private async Task<StylistSuggestionDto?> TryGenerateWithAiAsync(
        Product anchor,
        string? anchorRole,
        List<CandidateProduct> candidates,
        string style,
        string locale,
        CancellationToken ct)
    {
        if (!_gemini.IsConfigured) return null;
        var prompt = BuildPrompt(anchor, anchorRole, candidates, style, locale);
        var json = await _gemini.GenerateJsonAsync(prompt, ct);
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var outfitName = root.TryGetProperty("outfitName", out var n) ? n.GetString() ?? "" : "";
            if (!root.TryGetProperty("items", out var itemsEl) || itemsEl.ValueKind != JsonValueKind.Array)
                return null;

            var bySlug = candidates.ToDictionary(c => c.Slug, c => c, StringComparer.OrdinalIgnoreCase);
            var result = new StylistSuggestionDto
            {
                OutfitName = outfitName,
                AiPowered = true,
            };

            // Anchor always wins its own slot — the LLM is allowed to point
            // at it, but we trust our own data over the model's transcription.
            if (anchorRole is not null)
            {
                result.Items.Add(MakeAnchorItem(anchor, anchorRole));
            }

            foreach (var el in itemsEl.EnumerateArray())
            {
                var role = el.TryGetProperty("role", out var r) ? r.GetString() : null;
                var slug = el.TryGetProperty("slug", out var s) ? s.GetString() : null;
                var reason = el.TryGetProperty("reason", out var rs) ? rs.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(slug)) continue;
                if (role == anchorRole) continue; // anchor already added
                if (result.Items.Any(x => x.Role == role)) continue; // role already filled
                if (!bySlug.TryGetValue(slug, out var cand)) continue; // hallucination — skip
                result.Items.Add(new StylistItemDto
                {
                    Role = role,
                    ProductId = cand.Id,
                    ProductSlug = cand.Slug,
                    ProductName = cand.Name,
                    BrandName = cand.Brand,
                    Price = cand.Price,
                    ImageUrl = cand.ImageUrl,
                    Reason = reason.Trim(),
                });
            }

            // If the model returned < 3 valid additions, we treat it as a
            // failure and let the deterministic fallback have a go.
            if (result.Items.Count < 3) return null;
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Gemini returned unparseable JSON: {Json}", Truncate(json, 400));
            return null;
        }
    }

    private static string BuildPrompt(
        Product anchor, string? anchorRole,
        IReadOnlyList<CandidateProduct> candidates,
        string style, string locale)
    {
        var reasonLang = locale switch
        {
            "RUS" => "Russian",
            "ENG" => "English",
            _ => "Azerbaijani",
        };

        var styleHint = style == "auto"
            ? "Pick the style that best fits the anchor product (streetwear / minimal / oldmoney / techwear / y2k / sporty / classic / boho / casual)."
            : $"Style preset: \"{style}\". The whole outfit should align with this aesthetic.";

        // Bucket the candidate list by role so the prompt is scannable.
        var sb = new StringBuilder();
        sb.AppendLine("You are a fashion stylist for an Azerbaijani streetwear / fashion shop.");
        sb.AppendLine($"The user is viewing product slug \"{anchor.Slug}\" — \"{anchor.NameAz}\" by {anchor.Brand.Name}.");
        if (anchorRole is not null)
            sb.AppendLine($"This product is a {anchorRole}.");
        var anchorPrice = anchor.DiscountPrice ?? anchor.BasePrice;
        sb.AppendLine($"Anchor price: {anchorPrice:F2} AZN. Gender bias: {anchor.Gender}.");
        sb.AppendLine();
        sb.AppendLine(styleHint);
        sb.AppendLine();
        sb.AppendLine("Available products in the catalog (you MUST pick from these — DO NOT invent slugs):");
        foreach (var role in new[] { RoleTop, RoleBottom, RoleShoes, RoleAccessory })
        {
            var bucket = candidates.Where(c => c.Role == role).ToList();
            if (bucket.Count == 0) continue;
            sb.AppendLine($"[{role}]");
            foreach (var c in bucket)
            {
                sb.AppendLine(
                    $"  - slug:{c.Slug} | {c.Name} ({c.Brand}) | {c.Price:F2} AZN | colours: {c.Colours}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("Build a complete 4-piece outfit centred on the anchor product.");
        sb.AppendLine($"Fill these roles (excluding the anchor's own role \"{anchorRole}\"): top, bottom, shoes, accessory.");
        sb.AppendLine("If the anchor is a dress (paltar), skip the bottom and pick a second accessory instead.");
        sb.AppendLine();
        sb.AppendLine("Return STRICT JSON of the shape:");
        sb.AppendLine("{");
        sb.AppendLine("  \"outfitName\": \"short 3-5 word name in " + reasonLang + "\",");
        sb.AppendLine("  \"items\": [");
        sb.AppendLine("    { \"role\": \"top\", \"slug\": \"…\", \"reason\": \"one sentence in " + reasonLang + " explaining why this works\" },");
        sb.AppendLine("    { \"role\": \"bottom\", \"slug\": \"…\", \"reason\": \"…\" },");
        sb.AppendLine("    { \"role\": \"shoes\", \"slug\": \"…\", \"reason\": \"…\" },");
        sb.AppendLine("    { \"role\": \"accessory\", \"slug\": \"…\", \"reason\": \"…\" }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("Each slug MUST come from the list above. Reasons must be in " + reasonLang + ".");
        return sb.ToString();
    }

    private static StylistItemDto MakeAnchorItem(Product anchor, string anchorRole) =>
        new()
        {
            Role = anchorRole,
            ProductId = anchor.Id,
            ProductSlug = anchor.Slug,
            ProductName = anchor.NameAz,
            BrandName = anchor.Brand.Name,
            Price = anchor.DiscountPrice ?? anchor.BasePrice,
            ImageUrl = MainImage(anchor),
            Reason = string.Empty,
            IsAnchor = true,
        };

    // ─── Deterministic fallback ─────────────────────────────────────────

    /// <summary>
    /// When AI is unavailable, we pick the top-ranked candidate per missing
    /// role.  Reasons are left blank so the frontend can hide the section
    /// rather than fake a stylist voice.
    /// </summary>
    private static StylistSuggestionDto BuildFallback(
        Product anchor, string? anchorRole,
        List<CandidateProduct> candidates, string locale)
    {
        var result = new StylistSuggestionDto
        {
            OutfitName = locale switch
            {
                "RUS" => "Готовый комплект",
                "ENG" => "Complete the look",
                _ => "Tam komplekt",
            },
            AiPowered = false,
        };

        if (anchorRole is not null)
            result.Items.Add(MakeAnchorItem(anchor, anchorRole));

        foreach (var role in new[] { RoleTop, RoleBottom, RoleShoes, RoleAccessory })
        {
            if (role == anchorRole) continue;
            var pick = candidates.FirstOrDefault(c => c.Role == role);
            if (pick is null) continue;
            result.Items.Add(new StylistItemDto
            {
                Role = role,
                ProductId = pick.Id,
                ProductSlug = pick.Slug,
                ProductName = pick.Name,
                BrandName = pick.Brand,
                Price = pick.Price,
                ImageUrl = pick.ImageUrl,
                Reason = string.Empty,
            });
        }
        return result;
    }

    // ─── Normalisation helpers ──────────────────────────────────────────

    private static string NormaliseStyle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "auto";
        var s = raw.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "");
        return KnownStyles.Contains(s) ? s : "auto";
    }

    private static string NormaliseLocale(string? raw)
    {
        var s = (raw ?? "AZ").Trim().ToUpperInvariant();
        return s switch { "RUS" or "RU" => "RUS", "ENG" or "EN" => "ENG", _ => "AZ" };
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private static string Hash(string input)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..12];
    }

    /// <summary>Slim record holding only what the prompt + result need.</summary>
    private sealed record CandidateProduct(
        int Id,
        string Slug,
        string Name,
        string Brand,
        decimal Price,
        string Colours,
        string? ImageUrl,
        string Role);
}
