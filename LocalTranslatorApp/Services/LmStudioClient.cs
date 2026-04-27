using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using LocalTranslatorApp.Models;

namespace LocalTranslatorApp.Services;

public sealed class LmStudioClient
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(90)
    };

    public async Task<(bool IsConnected, string Message, string Model)> CheckAsync(AppSettings settings)
    {
        try
        {
            var endpoint = NormalizeEndpoint(settings.LmStudioEndpoint);
            using var response = await _httpClient.GetAsync($"{endpoint}/models");
            if (!response.IsSuccessStatusCode)
            {
                return (false, $"LM Studio response error: {(int)response.StatusCode}", "");
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            var model = "";
            if (document.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
            {
                model = data[0].GetProperty("id").GetString() ?? "";
            }

            return string.IsNullOrWhiteSpace(model)
                ? (false, "No model is loaded", "")
                : (true, "LM Studio connected", model);
        }
        catch (Exception ex)
        {
            return (false, $"Connection failed: {ex.Message}", "");
        }
    }

    public async Task<string> TranslateAsync(string sourceText, AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return "";
        }

        var endpoint = NormalizeEndpoint(settings.LmStudioEndpoint);
        var model = string.IsNullOrWhiteSpace(settings.Model) ? "local-model" : settings.Model;
        var sourceLanguage = settings.SourceLanguage == "Auto" ? "auto-detected source language" : settings.SourceLanguage;

        var request = new
        {
            model,
            temperature = settings.Temperature,
            max_tokens = settings.MaxTokens,
            messages = new[]
            {
                new { role = "system", content = BuildSystemPrompt(sourceLanguage, settings.TargetLanguage, settings.Instruction, strictRetry: false) },
                new { role = "user", content = $"Translate only the text between <text> and </text>.\n<text>\n{sourceText}\n</text>" }
            }
        };

        var translated = await SendTranslateRequestAsync(endpoint, request);
        if (!LooksLikeMetaResponse(translated))
        {
            return translated;
        }

        var strictRequest = new
        {
            model,
            temperature = 0,
            max_tokens = settings.MaxTokens,
            messages = new[]
            {
                new { role = "system", content = BuildSystemPrompt(sourceLanguage, settings.TargetLanguage, settings.Instruction, strictRetry: true) },
                new { role = "user", content = $"<text>\n{sourceText}\n</text>" }
            }
        };

        translated = await SendTranslateRequestAsync(endpoint, strictRequest);
        if (LooksLikeMetaResponse(translated))
        {
            throw new InvalidOperationException("The model returned commentary instead of a translation. Try again or simplify the translation instruction.");
        }

        return translated;
    }

    private async Task<string> SendTranslateRequestAsync(string endpoint, object request)
    {
        using var response = await _httpClient.PostAsJsonAsync($"{endpoint}/chat/completions", request);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            if (responseText.Contains("Context size has been exceeded", StringComparison.OrdinalIgnoreCase) ||
                responseText.Contains("context", StringComparison.OrdinalIgnoreCase) &&
                responseText.Contains("exceeded", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "LM Studio context limit was exceeded. This request is independent, but the selected text plus prompt is too long for the loaded model context. Increase the model context length in LM Studio or select a shorter passage.");
            }

            throw new InvalidOperationException($"LM Studio response error {(int)response.StatusCode}: {responseText}");
        }

        using var document = JsonDocument.Parse(responseText);
        var choices = document.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Translation response was empty.");
        }

        return choices[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()
            ?.Trim() ?? "";
    }

    private static string BuildSystemPrompt(string sourceLanguage, string targetLanguage, string instruction, bool strictRetry)
    {
        var strictLine = strictRetry
            ? "This is a retry because a previous response was invalid. Output a direct translation only."
            : "";

        return $"""
You are a translation-only engine.
Translate the user's text from {sourceLanguage} to {targetLanguage}.
Each request is independent. Ignore any previous conversation or previous translation.
Output only the translated text.
Do not explain the input.
Do not describe the text.
Do not mention the user, the prompt, the block, labels, styles, or whether the text is technical.
Do not add quotes, markdown, prefaces, notes, alternatives, or commentary.
Preserve line breaks where natural.
{strictLine}

User instruction:
{instruction}
""";
    }

    public static bool LooksLikeMetaResponse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.TrimStart().ToLowerInvariant();
        var suspiciousPrefixes = new[]
        {
            "the user has provided",
            "the text",
            "this text",
            "the provided text",
            "the input",
            "there is no",
            "it is pure",
            "this is a",
            "i can translate",
            "here is",
            "translation:",
            "translated text:"
        };

        return suspiciousPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static string NormalizeEndpoint(string endpoint)
    {
        endpoint = string.IsNullOrWhiteSpace(endpoint) ? "http://localhost:1234/v1" : endpoint.Trim();
        return endpoint.TrimEnd('/');
    }
}
