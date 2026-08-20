using System.Net.Http.Json;
using System.Text.Json;
using ECommerce.Application.Interfaces.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.AI;

/// <summary>
/// Configurable Gemini bits.  The API key comes from user-secrets in dev
/// (or appsettings/env in production) — search for "Gemini" in the config
/// pipeline.  Model + timeout are tunable but the defaults are sensible
/// for the free Gemini 2.0 Flash tier.
/// </summary>
public class GeminiSettings
{
    public const string SectionName = "Gemini";

    /// <summary>Google AI Studio API key (starts with <c>AIzaSy…</c>).</summary>
    public string? ApiKey { get; set; }
    /// <summary>
    /// Model id — defaults to <c>gemini-2.5-flash</c>.  Note that as of late
    /// 2025 Google moved <c>gemini-2.0-flash</c> off the free tier for many
    /// projects (quota = 0), so we use the 2.5 line which keeps the generous
    /// free quota and offers a 1M-token context window.
    /// </summary>
    public string Model { get; set; } = "gemini-2.5-flash";
    /// <summary>Per-call timeout in seconds. 2.5 thinks longer than 2.0.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// HttpClient-based Gemini caller.  We force <c>response_mime_type=
/// application/json</c> so the model returns parseable JSON rather than
/// commentary.  Failures are swallowed and logged — the caller decides
/// whether to fall back to a non-AI path.
/// </summary>
public class GeminiClient : IGeminiClient
{
    private readonly HttpClient _http;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiClient> _logger;

    public GeminiClient(
        HttpClient http,
        IOptions<GeminiSettings> options,
        ILogger<GeminiClient> logger)
    {
        _http = http;
        _settings = options.Value;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(5, _settings.TimeoutSeconds));
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.ApiKey);

    public async Task<string?> GenerateJsonAsync(string prompt, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            _logger.LogDebug("Gemini API key not configured — skipping AI call.");
            return null;
        }

        // Gemini REST shape: POST /v1beta/models/{model}:generateContent?key=…
        // The `responseMimeType=application/json` field nudges the model into
        // returning a parseable object instead of markdown / commentary.
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_settings.Model}:generateContent?key={_settings.ApiKey}";
        var body = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } },
                },
            },
            generationConfig = new
            {
                temperature = 0.7,
                topP = 0.9,
                // 2.5-flash is a "thinking" model — give it enough room to
                // both reason and emit the JSON.  2048 covers a 4-item
                // outfit + reasons comfortably.
                maxOutputTokens = 2048,
                responseMimeType = "application/json",
                // Disable the thinking budget — for our structured stylist
                // task the extra reasoning isn't worth the latency cost.
                thinkingConfig = new { thinkingBudget = 0 },
            },
        };

        try
        {
            using var res = await _http.PostAsJsonAsync(url, body, ct);
            if (!res.IsSuccessStatusCode)
            {
                var raw = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Gemini call failed: {Status} — {Body}",
                    res.StatusCode,
                    Truncate(raw, 400));
                return null;
            }

            // Parse the standard Gemini envelope:
            //   { candidates: [{ content: { parts: [{ text: "..." }] }, finishReason: ... }] }
            using var stream = await res.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;
            if (!root.TryGetProperty("candidates", out var candidates)
                || candidates.GetArrayLength() == 0)
            {
                _logger.LogWarning("Gemini response had no candidates.");
                return null;
            }
            var first = candidates[0];
            if (!first.TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts)
                || parts.GetArrayLength() == 0)
            {
                _logger.LogWarning("Gemini response had no content parts.");
                return null;
            }
            var text = parts[0].GetProperty("text").GetString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Gemini call timed out after {Seconds}s.", _settings.TimeoutSeconds);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini call threw — falling back to non-AI path.");
            return null;
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
