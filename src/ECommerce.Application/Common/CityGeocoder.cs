using System.Globalization;
using System.Text;
using ECommerce.Application.DTOs;

namespace ECommerce.Application.Common;

/// <summary>
/// Detects Azerbaijani cities mentioned in free-text delivery addresses and
/// aggregates orders into <see cref="CityOrderDto"/> rows the dashboard can
/// plot on the country map.
///
/// Matching is case- and diacritic-insensitive and accepts AZ / RU / EN
/// aliases (e.g. "Bakı", "Baku", "Баку" all map to Baku).  Each address is
/// scanned once and the first matching city wins, with the dictionary ordered
/// roughly longest-name-first so "Sumqayıt" wins over "Sum" if both were
/// present (defensive — they aren't).
/// </summary>
public static class CityGeocoder
{
    /// <summary>One entry in the city dictionary.</summary>
    private sealed record CityDef(
        string Name,
        double Lat,
        double Lng,
        string[] Aliases);

    /// <summary>
    /// Known Azerbaijani cities + their canonical coordinates.  Aliases cover
    /// transliterated AZ (without diacritics), Russian spellings and English
    /// spellings.  Order is biggest-cities-first so they're tried before
    /// smaller, less-likely matches.
    /// </summary>
    private static readonly CityDef[] Cities =
    {
        new("Bakı",          40.4093, 49.8671, new[]{ "bakı", "baki", "baku", "баку", "bakou" }),
        new("Sumqayıt",      40.5897, 49.6686, new[]{ "sumqayıt", "sumqayit", "sumqayyit", "sumgait", "сумгаит", "сумгайыт" }),
        new("Gəncə",         40.6828, 46.3606, new[]{ "gəncə", "gence", "gencə", "ganja", "гянджа", "ganca" }),
        new("Mingəçevir",    40.7700, 47.0500, new[]{ "mingəçevir", "mingacevir", "mingechevir", "mingachevir", "мингячевир" }),
        new("Şirvan",        39.9300, 48.9197, new[]{ "şirvan", "sirvan", "shirvan", "ширван" }),
        new("Naxçıvan",      39.2089, 45.4122, new[]{ "naxçıvan", "naxcivan", "naxchivan", "nakhchivan", "нахчыван", "нахичевань" }),
        new("Lənkəran",      38.7547, 48.8475, new[]{ "lənkəran", "lenkeran", "lankaran", "lankoran", "ленкорань" }),
        new("Şəki",          41.1975, 47.1706, new[]{ "şəki", "seki", "sheki", "şeki", "шеки" }),
        new("Yevlax",        40.6175, 47.1500, new[]{ "yevlax", "yevlakh", "евлах" }),
        new("Quba",          41.3614, 48.5128, new[]{ "quba", "губа", "guba" }),
        new("Xırdalan",      40.4528, 49.7556, new[]{ "xırdalan", "xirdalan", "хырдалан", "khirdalan" }),
        new("Şamaxı",        40.6322, 48.6411, new[]{ "şamaxı", "samaxi", "şamaxi", "shamakhi", "шемаха", "шамахы" }),
        new("Bərdə",         40.3722, 47.1297, new[]{ "bərdə", "berde", "barda", "берда" }),
        new("Salyan",        39.5783, 48.9706, new[]{ "salyan", "сальян" }),
        new("Şabran",        41.2167, 48.9667, new[]{ "şabran", "sabran", "shabran", "шабран" }),
        new("Tovuz",         40.9928, 45.6203, new[]{ "tovuz", "товуз" }),
        new("İmişli",        39.8694, 48.0639, new[]{ "imişli", "imisli", "imishli", "имишли" }),
        new("Qazax",         41.0928, 45.3653, new[]{ "qazax", "kazakh", "газах", "казах" }),
        new("Göyçay",        40.6517, 47.7406, new[]{ "göyçay", "goycay", "goychay", "гёйчай" }),
        new("Beyləqan",      39.7728, 47.6133, new[]{ "beyləqan", "beyleqan", "beylagan", "бейлаган" }),
        new("Cəlilabad",     39.2050, 48.4972, new[]{ "cəlilabad", "celilabad", "jalilabad", "джалилабад" }),
        new("Saatlı",        39.9094, 48.3597, new[]{ "saatlı", "saatli", "саатлы" }),
        new("Sabirabad",     40.0083, 48.4789, new[]{ "sabirabad", "сабирабад" }),
        new("Astara",        38.4561, 48.8728, new[]{ "astara", "астара" }),
        new("Yardımlı",      38.9075, 48.2497, new[]{ "yardımlı", "yardimli", "ярдымлы" }),
        new("Gədəbəy",       40.5697, 45.8136, new[]{ "gədəbəy", "gedebey", "gadabay", "кедабек" }),
        new("Daşkəsən",      40.5197, 46.0739, new[]{ "daşkəsən", "daskesen", "dashkasan", "дашкесан" }),
        new("Şəmkir",        40.8294, 46.0156, new[]{ "şəmkir", "semkir", "shamkir", "шамкир" }),
        new("Naftalan",      40.5078, 46.8200, new[]{ "naftalan", "нафталан" }),
        new("Goranboy",      40.6094, 46.7894, new[]{ "goranboy", "горанбой" }),
        new("Tərtər",        40.3439, 46.9347, new[]{ "tərtər", "terter", "tartar", "тертер" }),
        new("Ağcabədi",      40.0508, 47.4592, new[]{ "ağcabədi", "agcabedi", "agjabadi", "агджабеди" }),
        new("Ağdam",         39.9919, 46.9303, new[]{ "ağdam", "agdam", "агдам" }),
        new("Füzuli",        39.6047, 47.1422, new[]{ "füzuli", "fuzuli", "физули" }),
        new("Cəbrayıl",      39.4078, 47.0228, new[]{ "cəbrayıl", "cebrayil", "jabrayil", "джебраил" }),
        new("Zəngilan",      39.0867, 46.6519, new[]{ "zəngilan", "zengilan", "zangilan", "зангилан" }),
        new("Qubadlı",       39.3475, 46.5781, new[]{ "qubadlı", "qubadli", "губадлы" }),
        new("Laçın",         39.6347, 46.5494, new[]{ "laçın", "lacin", "lachin", "лачин" }),
        new("Kəlbəcər",      40.1064, 46.0353, new[]{ "kəlbəcər", "kelbecer", "kalbajar", "кельбаджар" }),
        new("Xocavənd",      39.7944, 47.0944, new[]{ "xocavənd", "xocavend", "khojavend", "ходжавенд" }),
        new("Şuşa",          39.7544, 46.7522, new[]{ "şuşa", "susa", "shusha", "шуша" }),
        new("Xocalı",        39.9133, 46.7975, new[]{ "xocalı", "xocali", "khojaly", "ходжалы" }),
        new("Xankəndi",      39.8267, 46.7681, new[]{ "xankəndi", "xankendi", "khankendi", "ханкенди", "степанакерт" }),
        new("Ağdaş",         40.6336, 47.4683, new[]{ "ağdaş", "agdas", "agdash", "агдаш" }),
        new("Ağsu",          40.5697, 48.4022, new[]{ "ağsu", "agsu", "агсу" }),
        new("Balakən",       41.7036, 46.4044, new[]{ "balakən", "balaken", "balakan", "белоканы" }),
        new("Biləsuvar",     39.4592, 48.5511, new[]{ "biləsuvar", "bilesuvar", "bilasuvar", "билясувар" }),
        new("Dəvəçi",        41.2167, 48.9667, new[]{ "dəvəçi", "deveci", "davachi" }),
        new("Hacıqabul",     40.0392, 48.9203, new[]{ "hacıqabul", "haciqabul", "hajigabul", "гаджикабул" }),
        new("İsmayıllı",     40.7872, 48.1525, new[]{ "ismayıllı", "ismayilli", "isma'illi", "исмаиллы" }),
        new("Qax",           41.4189, 46.9281, new[]{ "qax", "gakh", "qakh", "гах" }),
        new("Qəbələ",        40.9956, 47.8400, new[]{ "qəbələ", "qebele", "gabala", "габала" }),
        new("Qobustan",      40.5333, 48.9333, new[]{ "qobustan", "gobustan", "qubustan", "гобустан" }),
        new("Qusar",         41.4256, 48.4283, new[]{ "qusar", "kusary", "гусар" }),
        new("Lerik",         38.7733, 48.4153, new[]{ "lerik", "лерик" }),
        new("Masallı",       39.0331, 48.6589, new[]{ "masallı", "masalli", "масаллы" }),
        new("Neftçala",      39.3878, 49.2422, new[]{ "neftçala", "neftcala", "neftchala", "нефтечала" }),
        new("Oğuz",          41.0731, 47.4669, new[]{ "oğuz", "oguz", "огуз" }),
        new("Siyəzən",       41.0789, 49.1131, new[]{ "siyəzən", "siyezen", "siyazan", "сиязань" }),
        new("Ucar",          40.5078, 47.6492, new[]{ "ucar", "ujar", "уджары" }),
        new("Xaçmaz",        41.4583, 48.8019, new[]{ "xaçmaz", "xacmaz", "khachmaz", "хачмас" }),
        new("Zaqatala",      41.6308, 46.6447, new[]{ "zaqatala", "zakatala", "загатала" }),
        new("Zərdab",        40.2125, 47.7158, new[]{ "zərdab", "zerdab", "zardab", "зардоб" }),
        new("Sumqayit",      40.5897, 49.6686, new[]{ "sumqayit" }), // extra duplicate guard
    };

    /// <summary>
    /// Aggregate (address, totalAmount) tuples by detected city.  Addresses
    /// that match no city are dropped.  Result is sorted descending by order
    /// count (most popular city first) — that's how the map renders bubbles.
    /// </summary>
    public static List<CityOrderDto> Aggregate(IEnumerable<(string Address, decimal Amount)> rows)
    {
        var byCity = new Dictionary<string, (int Count, decimal Revenue, CityDef Def)>();
        foreach (var (address, amount) in rows)
        {
            var match = DetectCity(address);
            if (match is null) continue;
            if (byCity.TryGetValue(match.Name, out var agg))
                byCity[match.Name] = (agg.Count + 1, agg.Revenue + amount, agg.Def);
            else
                byCity[match.Name] = (1, amount, match);
        }
        return byCity
            .Select(kv => new CityOrderDto
            {
                City = kv.Value.Def.Name,
                Lat = kv.Value.Def.Lat,
                Lng = kv.Value.Def.Lng,
                OrderCount = kv.Value.Count,
                Revenue = kv.Value.Revenue,
            })
            .OrderByDescending(c => c.OrderCount)
            .ToList();
    }

    /// <summary>
    /// First city whose alias appears anywhere in <paramref name="address"/>,
    /// or <c>null</c> if no AZ city was detected.
    /// </summary>
    public static (string Name, double Lat, double Lng)? Lookup(string? address)
    {
        var c = DetectCity(address);
        return c is null ? null : (c.Name, c.Lat, c.Lng);
    }

    /// <summary>
    /// Pre-built lookup: (normalized-alias, city), sorted longest-alias-first
    /// so the first containment hit is automatically the most specific match.
    /// </summary>
    private static readonly (string Alias, CityDef City)[] AliasIndex = Cities
        .SelectMany(c => c.Aliases.Select(a => (Alias: Normalize(a), City: c)))
        .Where(t => t.Alias.Length >= 3) // safety: refuse 1-2 letter aliases
        .OrderByDescending(t => t.Alias.Length)
        .ToArray();

    private static CityDef? DetectCity(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        var hay = Normalize(address);
        // Aliases are sorted longest-first, so the first containment match
        // is also the most specific one (no need to scan further).
        foreach (var (alias, city) in AliasIndex)
        {
            if (hay.Contains(alias, StringComparison.Ordinal))
                return city;
        }
        return null;
    }

    /// <summary>
    /// Lower-case + strip Unicode diacritics so "Bakı" ≡ "baki", "Şəki" ≡ "seki".
    /// Cyrillic characters are preserved (Russian aliases match Cyrillic input).
    /// </summary>
    private static string Normalize(string input)
    {
        var lowered = input.ToLowerInvariant();
        var decomposed = lowered.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            // Map AZ-specific letters to ASCII equivalents.
            switch (ch)
            {
                case 'ə': sb.Append('e'); break;
                case 'ı': sb.Append('i'); break;
                case 'ş': sb.Append('s'); break;
                case 'ç': sb.Append('c'); break;
                case 'ğ': sb.Append('g'); break;
                case 'ö': sb.Append('o'); break;
                case 'ü': sb.Append('u'); break;
                default:  sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }
}
