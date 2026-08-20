using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.API.Controllers;

/// <summary>
/// AI stylist endpoint — public so guests can use the outfit builder
/// without an account.  Results are cached server-side per
/// (productId, style, locale) for 24h.
///
/// The whole feature can be disabled via the admin panel by flipping the
/// <c>feature.aiStylist</c> site setting to <c>"false"</c>.  When disabled
/// the endpoint short-circuits with an empty suggestion and the storefront
/// hides the OutfitBuilder section.
/// </summary>
[ApiController]
[Route("api/stylist")]
public class StylistController : ControllerBase
{
    /// <summary>Site-setting key that toggles the AI stylist on/off.</summary>
    public const string FeatureKey = "feature.aiStylist";

    private readonly IStylistService _stylist;
    private readonly IUnitOfWork _uow;
    public StylistController(IStylistService stylist, IUnitOfWork uow)
    {
        _stylist = stylist;
        _uow = uow;
    }

    [HttpPost("suggest")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<StylistSuggestionDto>>> Suggest(
        [FromBody] StylistRequestDto request, CancellationToken ct)
    {
        // Feature gate — when missing the setting we treat it as enabled
        // (existing installations keep working).  Explicit "false" disables.
        var enabled = await IsFeatureEnabledAsync(ct);
        if (!enabled)
        {
            return Ok(ApiResponse<StylistSuggestionDto>.Ok(new StylistSuggestionDto
            {
                OutfitName = string.Empty,
                Items = new(),
                AiPowered = false,
            }));
        }

        var result = await _stylist.SuggestAsync(request, ct);
        return Ok(ApiResponse<StylistSuggestionDto>.Ok(result));
    }

    private async Task<bool> IsFeatureEnabledAsync(CancellationToken ct)
    {
        var raw = await _uow.SiteSettings.Query()
            .Where(s => s.Key == FeatureKey)
            .Select(s => s.ValueAz)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(raw)) return true;     // default: on
        return !raw.Trim().Equals("false", StringComparison.OrdinalIgnoreCase);
    }
}
