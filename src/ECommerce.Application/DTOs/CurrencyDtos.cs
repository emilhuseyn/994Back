namespace ECommerce.Application.DTOs;

/// <summary>
/// Exchange rates for the storefront's currency switcher.
/// <see cref="Rates"/> maps a currency code to how many units of it one unit of
/// <see cref="Base"/> (AZN) buys — i.e. <c>usd = azn * Rates["USD"]</c>.
/// Display-only: orders are always charged in AZN.
/// </summary>
public class CurrencyRatesDto
{
    public string Base { get; set; } = "AZN";
    /// <summary>Where the numbers came from: "cbar", "er-api", "cache" or "none".</summary>
    public string Source { get; set; } = "none";
    public DateTime UpdatedAt { get; set; }
    public Dictionary<string, decimal> Rates { get; set; } = new();
}
