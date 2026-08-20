using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ECommerce.Application.Common;

public static class SlugHelper
{
    private static readonly Dictionary<char, string> CharMap = new()
    {
        { 'ə', "e" }, { 'Ə', "e" },
        { 'ı', "i" }, { 'İ', "i" },
        { 'ö', "o" }, { 'Ö', "o" },
        { 'ü', "u" }, { 'Ü', "u" },
        { 'ç', "c" }, { 'Ç', "c" },
        { 'ş', "s" }, { 'Ş', "s" },
        { 'ğ', "g" }, { 'Ğ', "g" },
    };

    public static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var sb = new StringBuilder();
        foreach (var ch in input)
        {
            if (CharMap.TryGetValue(ch, out var mapped))
                sb.Append(mapped);
            else
                sb.Append(ch);
        }

        var normalized = sb.ToString().Normalize(NormalizationForm.FormD);
        var ascii = new StringBuilder();
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark) ascii.Append(ch);
        }

        var slug = ascii.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-").Trim('-');
        return slug;
    }

    public static string EnsureUnique(string baseSlug, Func<string, bool> exists)
    {
        var slug = baseSlug;
        var i = 2;
        while (exists(slug)) slug = $"{baseSlug}-{i++}";
        return slug;
    }
}
