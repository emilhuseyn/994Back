using ECommerce.Application.Interfaces.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Storage;

public class FileStorageSettings
{
    public const string SectionName = "FileStorage";
    public string RootPath { get; set; } = "wwwroot/uploads";
    public string PublicBaseUrl { get; set; } = "/uploads";
}

public class LocalFileStorageService : IFileStorageService
{
    private readonly FileStorageSettings _settings;

    public LocalFileStorageService(IOptions<FileStorageSettings> options)
    {
        _settings = options.Value;
    }

    public async Task<string> SaveAsync(IFormFile file, string subfolder, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0) throw new ArgumentException("Boş fayl.", nameof(file));

        var safeSub = string.Join('/', subfolder
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => string.Concat(p.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))));

        var folder = Path.Combine(_settings.RootPath, safeSub);
        Directory.CreateDirectory(folder);

        var ext = Path.GetExtension(file.FileName);
        var safeName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, safeName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream, ct);

        var relUrl = $"{_settings.PublicBaseUrl}/{safeSub}/{safeName}".Replace('\\', '/');
        return relUrl;
    }

    public Task<bool> DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return Task.FromResult(false);
        var trimmed = relativePath.StartsWith(_settings.PublicBaseUrl)
            ? relativePath[_settings.PublicBaseUrl.Length..].TrimStart('/')
            : relativePath.TrimStart('/');
        var path = Path.Combine(_settings.RootPath, trimmed.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) return Task.FromResult(false);
        try { File.Delete(path); return Task.FromResult(true); }
        catch { return Task.FromResult(false); }
    }
}
