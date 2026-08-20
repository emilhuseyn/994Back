using System.Collections.Concurrent;
using System.Text.Json;
using ECommerce.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

public record TranslateRequest(string Text, string SourceLang, string TargetLang);
public record TranslateResponse(string TranslatedText);

[ApiController]
[Route("api/admin/translate")]
[Authorize(Roles = "Admin")]
public class TranslateController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, string> Cache = new();
    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    static TranslateController()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<TranslateResponse>>> Translate(
        [FromBody] TranslateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return Ok(ApiResponse<TranslateResponse>.Ok(new TranslateResponse(string.Empty)));

        var key = $"{request.SourceLang}|{request.TargetLang}|{request.Text}";
        if (Cache.TryGetValue(key, out var cached))
            return Ok(ApiResponse<TranslateResponse>.Ok(new TranslateResponse(cached)));

        try
        {
            // Free unofficial Google Translate endpoint. Sufficient for an admin panel,
            // but for production swap to Google Cloud Translation API (with API key),
            // DeepL or Yandex Translate.
            var url = "https://translate.googleapis.com/translate_a/single"
                      + "?client=gtx"
                      + $"&sl={Uri.EscapeDataString(request.SourceLang)}"
                      + $"&tl={Uri.EscapeDataString(request.TargetLang)}"
                      + "&dt=t"
                      + $"&q={Uri.EscapeDataString(request.Text)}";

            using var resp = await Http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadAsStringAsync(ct);

            // Response shape: [[["translated text","original",null,...], ...], null, "az", ...]
            using var doc = JsonDocument.Parse(body);
            var translated = "";
            foreach (var seg in doc.RootElement[0].EnumerateArray())
            {
                if (seg.ValueKind == JsonValueKind.Array && seg.GetArrayLength() > 0)
                    translated += seg[0].GetString() ?? "";
            }
            if (string.IsNullOrEmpty(translated))
                translated = request.Text;

            Cache[key] = translated;
            return Ok(ApiResponse<TranslateResponse>.Ok(new TranslateResponse(translated)));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<TranslateResponse>
                .Fail($"Tərcümə uğursuz oldu: {ex.Message}"));
        }
    }
}
