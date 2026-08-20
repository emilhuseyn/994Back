using ECommerce.Application.DTOs;

namespace ECommerce.Application.Interfaces.Infrastructure;

public interface ICurrencyRateProvider
{
    /// <summary>
    /// AZN-based exchange rates for the storefront. Cached; never throws —
    /// on total failure it returns the last known good rates, or AZN only.
    /// </summary>
    Task<CurrencyRatesDto> GetRatesAsync(CancellationToken ct = default);
}
