using System.Globalization;
using ECommerce.Domain.Enums;

namespace ECommerce.Application.Common;

/// <summary>
/// Best-effort enrichment for raw 1C data that lacks structured metadata:
/// guesses a hex code for a colour name, a sort weight for a size, and gender
/// from the product name. None of these are authoritative — the admin can
/// always override the result afterwards.
/// </summary>
public static class InventoryHeuristics
{
    // Common colour name → hex. Keys are matched case-insensitively.
    private static readonly Dictionary<string, string> ColorHex = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BLACK"] = "#0C0C0C", ["WHITE"] = "#FFFFFF",
        ["GREY"] = "#8B8B8B", ["GRAY"] = "#8B8B8B", ["SILVER"] = "#C0C0C0",
        ["LEAD"] = "#5B6770", ["CHARCOAL"] = "#36454F", ["ANTHRACITE"] = "#3B3B3B", ["SMOKE"] = "#738276",
        ["RED"] = "#D63031", ["BORDO"] = "#7C1F2F", ["BURGUNDY"] = "#7C1F2F", ["WINE"] = "#7C1F2F", ["MAROON"] = "#7C1F2F",
        ["BLUE"] = "#1F3A93", ["NAVY"] = "#1B2A4A", ["DENIM"] = "#3B5A86", ["SKY"] = "#3AA6E3", ["TEAL"] = "#1F7A7A", ["COBALT"] = "#1F49C9",
        ["GREEN"] = "#2F6B3A", ["OLIVE"] = "#5A6B2F", ["KHAKI"] = "#B6A66A", ["LIME"] = "#7AC142", ["FOREST"] = "#274E2B", ["MOSS"] = "#5A6B3A",
        ["BROWN"] = "#5A3A1A", ["TAN"] = "#D2B48C", ["BEIGE"] = "#E6D4B1", ["SAND"] = "#D8C3A0", ["CAMEL"] = "#C19A6B", ["RUST"] = "#9C4722",
        ["PINK"] = "#F1A4B8", ["ROSE"] = "#E0809A", ["PURPLE"] = "#6B3FA0", ["VIOLET"] = "#7A4FB0", ["LILAC"] = "#B695D0",
        ["ORANGE"] = "#F57E20", ["YELLOW"] = "#F4C20D", ["GOLD"] = "#D4AF37", ["MUSTARD"] = "#D4A017",
        ["CREAM"] = "#F5F0E1", ["IVORY"] = "#FFFFF0", ["NATURAL"] = "#EDE6D6", ["STONE"] = "#C9C0B0",
    };

    /// <summary>
    /// Best-effort hex for a raw colour name. Tries the whole string, then a
    /// space-stripped form, then each word token (so "VELVET BLUE HEAVY STONE"
    /// resolves via "BLUE"). Falls back to a neutral grey.
    /// </summary>
    public static string GuessHex(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "#808080";
        var trimmed = name.Trim();
        if (ColorHex.TryGetValue(trimmed, out var exact)) return exact;
        if (ColorHex.TryGetValue(trimmed.Replace(" ", "").Replace("-", ""), out var collapsed)) return collapsed;
        foreach (var token in trimmed.Split(new[] { ' ', '-', '/', '_' }, StringSplitOptions.RemoveEmptyEntries))
            if (ColorHex.TryGetValue(token, out var t)) return t;
        return "#808080";
    }

    /// <summary>
    /// Sort weight so sizes order sensibly: XXS &lt; XS &lt; … &lt; XL &lt; XXL
    /// &lt; numeric (28, 30, …) &lt; one-size.
    /// </summary>
    public static int GuessSizeOrder(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return 500;
        var n = name.Trim().ToUpperInvariant();
        switch (n)
        {
            case "XXS": return 5;
            case "XS": return 10;
            case "S": return 20;
            case "M": return 30;
            case "L": return 40;
            case "XL": return 50;
            case "XXL": case "2XL": return 60;
            case "XXXL": case "3XL": return 70;
            case "4XL": return 80;
            case "OS": case "ONE SIZE": case "ONESIZE": case "UNI": case "U": return 9999;
        }
        if (int.TryParse(n, NumberStyles.Integer, CultureInfo.InvariantCulture, out var num))
            return 1000 + num; // numeric sizes ordered naturally, after the letter sizes
        return 500;
    }

    /// <summary>
    /// Carhartt-style names prefix women's items with "W " (e.g. "CARH W Kelly
    /// Shirt Jac"). Everything else defaults to unisex — we never try to guess
    /// "men", since that can't be told apart from a stray initial reliably.
    /// </summary>
    public static Gender GuessGender(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Gender.Unisex;
        var n = $" {name.Trim().ToUpperInvariant()} ";
        if (n.Contains(" W ") || n.Contains("WOMEN") || n.Contains("WOMAN") || n.Contains(" WMN") || n.Contains("FEMME") || n.Contains(" LADIES"))
            return Gender.Women;
        return Gender.Unisex;
    }

    /// <summary>
    /// Infer an Azerbaijani category name from the product name when 1C doesn't
    /// supply one (the raw 1C Excel has no category column). Returns names that
    /// match the seeded category slugs so existing categories are reused; falls
    /// back to "Digər" (Other). Order matters — specific tokens are tested before
    /// generic ones (e.g. "T-SHIRT" before "SHIRT", "JAC" before "SHIRT").
    /// </summary>
    public static string GuessCategoryName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Digər";
        var n = name.ToUpperInvariant();

        if (Has(n, "SOCK")) return "Corablar";
        if (Has(n, "BELT")) return "Kəmərlər";
        if (Has(n, "CAP") || Has(n, "HAT") || Has(n, "BEANIE")) return "Papaqlar";
        if (Has(n, "BACKPACK") || Has(n, "BAG") || Has(n, "TOTE") || Has(n, "RUCKSACK")) return "Çantalar";
        if (Has(n, "BOOT")) return "Çəkmələr";
        if (Has(n, "SANDAL") || Has(n, "SLIDE")) return "Səndəllər";
        if (Has(n, "SNEAKER") || Has(n, "SHOE") || Has(n, "TRAINER")) return "Krossovkalar";
        if (Has(n, "HOOD")) return "Hudilər";
        if (Has(n, "SWEAT") || Has(n, "CREW") || Has(n, "PULLOVER") || Has(n, "KNIT") || Has(n, "JUMPER") || Has(n, "FLEECE")) return "Sviterlər";
        if (Has(n, "POLO")) return "Pololar";
        if (Has(n, "JACKET") || Has(n, "SHIRT JAC") || Has(n, " JAC") || Has(n, "COAT") || Has(n, "PARKA") || Has(n, "ANORAK")) return "Gödəkçələr";
        if (Has(n, "VEST") || Has(n, "GILET")) return "Jiletlər";
        if (Has(n, "TEE") || Has(n, "T-SHIRT") || Has(n, "TSHIRT")) return "T-shirtlər";
        if (Has(n, "SHIRT")) return "Köynəklər";
        if (Has(n, "SHORT")) return "Şortlar";
        if (Has(n, "JEAN")) return "Cinslər";
        if (Has(n, "PANT") || Has(n, "TROUSER") || Has(n, "CHINO") || Has(n, "DOUBLE KNEE")) return "Şalvarlar";
        return "Digər";
    }

    private static bool Has(string haystack, string token) => haystack.Contains(token, StringComparison.Ordinal);
}
