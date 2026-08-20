using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Interfaces.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

/// <summary>
/// AZN-based exchange rates for the storefront currency switcher. Public and
/// cached server-side (6h) — the browser never talks to the rate providers, so
/// there is no CORS issue and no per-visitor rate limiting.
/// </summary>
[ApiController]
[Route("api/currency")]
public class CurrencyController : ControllerBase
{
    private readonly ICurrencyRateProvider _rates;
    public CurrencyController(ICurrencyRateProvider rates) => _rates = rates;

    [HttpGet("rates")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CurrencyRatesDto>>> Rates(CancellationToken ct)
        => Ok(ApiResponse<CurrencyRatesDto>.Ok(await _rates.GetRatesAsync(ct)));
}
