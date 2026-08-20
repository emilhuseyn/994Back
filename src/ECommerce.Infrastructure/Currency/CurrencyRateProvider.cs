using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using ECommerce.Application.DTOs;
using ECommerce.Application.Interfaces.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ECommerce.Infrastructure.Currency;

/// <summary>
/// AZN-based exchange rates for the storefront's currency switcher.
///
/// Source order:
///   1. <b>CBAR</b> (Central Bank of Azerbaijan) — the official AZN rates, free
///      and key-free. Published on business days, so we walk back up to a week
///      if today's file isn't there (weekend/holiday/not-yet-published).
///   2. <b>open.er-api.com</b> — key-free JSON fallback.
///   3. The last known good set (kept 30 days), so a transient outage never
///      drops the switcher back to AZN-only.
///
/// Fresh results are cached for 6 hours: rates move once a day at most, and this
/// keeps us far inside both providers' fair-use limits.
/// </summary>
public class CurrencyRateProvider : ICurrencyRateProvider
{
    private static readonly string[] Wanted = { "USD", "EUR", "RUB" };
    private const string FreshKey = "currency:rates:fresh";
    private const string LastGoodKey = "currency:rates:lastgood";

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CurrencyRateProvider> _logger;

    public CurrencyRateProvider(
        HttpClient http,
        IMemoryCache cache,
        ILogger<CurrencyRateProvider> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(12);
    }

    public async Task<CurrencyRatesDto> GetRatesAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(FreshKey, out CurrencyRatesDto? fresh) && fresh is not null)
            return fresh;

        var dto = await TryCbarAsync(ct) ?? await TryErApiAsync(ct);

        if (dto is not null)
        {
            _cache.Set(FreshKey, dto, TimeSpan.FromHours(6));
            _cache.Set(LastGoodKey, dto, TimeSpan.FromDays(30));
            return dto;
        }

        if (_cache.TryGetValue(LastGoodKey, out CurrencyRatesDto? lastGood) && lastGood is not null)
        {
            _logger.LogWarning(
                "Currency: both sources failed; serving last known rates from {At}.", lastGood.UpdatedAt);
            return new CurrencyRatesDto
            {
                Base = lastGood.Base,
                Source = "cache",
                UpdatedAt = lastGood.UpdatedAt,
                Rates = lastGood.Rates,
            };
        }

        _logger.LogError("Currency: no rates available; returning AZN only.");
        return new CurrencyRatesDto
        {
            Base = "AZN",
            Source = "none",
            UpdatedAt = DateTime.UtcNow,
            Rates = new Dictionary<string, decimal> { ["AZN"] = 1m },
        };
    }

    // ── CBAR — official Azerbaijani rates ──────────────────────────────────
    private async Task<CurrencyRatesDto?> TryCbarAsync(CancellationToken ct)
    {
        for (var back = 0; back < 7; back++)
        {
            // Baku is UTC+4; use local date so we ask for the right day's file.
            var day = DateTime.UtcNow.AddHours(4).AddDays(-back);
            var url = $"https://www.cbar.az/currencies/{day:dd.MM.yyyy}.xml";
            try
            {
                using var res = await _http.GetAsync(url, ct);
                if (!res.IsSuccessStatusCode) continue;

                var doc = XDocument.Parse(await res.Content.ReadAsStringAsync(ct));
                var rates = new Dictionary<string, decimal> { ["AZN"] = 1m };

                foreach (var code in Wanted)
                {
                    var v = doc.Descendants("Valute")
                        .FirstOrDefault(x => (string?)x.Attribute("Code") == code);
                    if (v is null) continue;

                    // CBAR publishes <Value> AZN per <Nominal> units of the
                    // currency (e.g. RUB is quoted per 100). We want units per
                    // 1 AZN → Nominal / Value.
                    var nominal = ParseDec((string?)v.Element("Nominal"));
                    var value = ParseDec((string?)v.Element("Value"));
                    if (nominal is null || value is null || value <= 0 || nominal <= 0) continue;

                    rates[code] = Math.Round(nominal.Value / value.Value, 6);
                }

                if (rates.Count <= 1) continue; // nothing useful parsed — try an earlier day

                return new CurrencyRatesDto
                {
                    Base = "AZN",
                    Source = "cbar",
                    UpdatedAt = day.Date,
                    Rates = rates,
                };
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Currency: CBAR fetch failed for {Url}.", url);
            }
        }
        return null;
    }

    // ── open.er-api.com — key-free JSON fallback ───────────────────────────
    private async Task<CurrencyRatesDto?> TryErApiAsync(CancellationToken ct)
    {
        try
        {
            using var res = await _http.GetAsync("https://open.er-api.com/v6/latest/AZN", ct);
            if (!res.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            if (!root.TryGetProperty("result", out var result) || result.GetString() != "success")
                return null;
            if (!root.TryGetProperty("rates", out var r)) return null;

            var rates = new Dictionary<string, decimal> { ["AZN"] = 1m };
            foreach (var code in Wanted)
                if (r.TryGetProperty(code, out var el) && el.TryGetDecimal(out var val) && val > 0)
                    rates[code] = Math.Round(val, 6);

            if (rates.Count <= 1) return null;

            var updated =
                root.TryGetProperty("time_last_update_unix", out var t) && t.TryGetInt64(out var unix)
                    ? DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime
                    : DateTime.UtcNow;

            return new CurrencyRatesDto
            {
                Base = "AZN",
                Source = "er-api",
                UpdatedAt = updated,
                Rates = rates,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Currency: er-api fallback failed.");
            return null;
        }
    }

    private static decimal? ParseDec(string? s) =>
        decimal.TryParse(s?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            ? d
            : null;
}
