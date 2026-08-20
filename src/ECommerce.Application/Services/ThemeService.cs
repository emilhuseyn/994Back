using System.Text.Json;
using ECommerce.Application.DTOs;
using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Application.Services.Abstractions;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class ThemeService : IThemeService
{
    public const string SettingKey = "_theme.config";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IUnitOfWork _uow;

    public ThemeService(IUnitOfWork uow) => _uow = uow;

    public async Task<ThemeDto> GetAsync(CancellationToken ct = default)
    {
        var row = await _uow.SiteSettings.Query()
            .FirstOrDefaultAsync(s => s.Key == SettingKey, ct);
        if (row is null || string.IsNullOrWhiteSpace(row.ValueAz))
            return ThemeDto.Default();

        try
        {
            return JsonSerializer.Deserialize<ThemeDto>(row.ValueAz, JsonOpts)
                ?? ThemeDto.Default();
        }
        catch
        {
            // Corrupt JSON — fall back to defaults rather than crashing the API.
            return ThemeDto.Default();
        }
    }

    public async Task<ThemeDto> UpdateAsync(ThemeDto theme, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(theme, JsonOpts);
        var row = await _uow.SiteSettings.Query()
            .FirstOrDefaultAsync(s => s.Key == SettingKey, ct);
        if (row is null)
        {
            row = new SiteSetting { Key = SettingKey, ValueAz = json };
            await _uow.SiteSettings.AddAsync(row, ct);
        }
        else
        {
            row.ValueAz = json;
            row.UpdatedAt = DateTime.UtcNow;
            _uow.SiteSettings.Update(row);
        }
        await _uow.SaveChangesAsync(ct);
        return theme;
    }

    public async Task<ThemeDto> ResetAsync(CancellationToken ct = default)
    {
        var row = await _uow.SiteSettings.Query()
            .FirstOrDefaultAsync(s => s.Key == SettingKey, ct);
        if (row is not null)
        {
            _uow.SiteSettings.Remove(row);
            await _uow.SaveChangesAsync(ct);
        }
        return ThemeDto.Default();
    }
}
