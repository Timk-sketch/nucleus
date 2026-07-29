using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Infrastructure.Services;

public class ClaudeService(IHttpClientFactory httpClientFactory, IConfiguration config) : IClaudeService
{
    public async Task<string> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        string model = "claude-sonnet-4-6",
        int maxTokens = 4096,
        CancellationToken ct = default)
    {
        var apiKey = config["ANTHROPIC_API_KEY"]
            ?? throw new InvalidOperationException("ANTHROPIC_API_KEY is not configured.");

        var client = httpClientFactory.CreateClient("anthropic");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var body = new
        {
            model,
            max_tokens = maxTokens,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt }
            }
        };

        var response = await client.PostAsJsonAsync("https://api.anthropic.com/v1/messages", body, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Claude API error {(int)response.StatusCode}: {error}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

        var stopReason = json.GetProperty("stop_reason").GetString();
        if (stopReason != "end_turn")
            throw new InvalidOperationException($"Claude stopped unexpectedly: {stopReason}");

        return json
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString()
            ?? throw new InvalidOperationException("Claude returned empty content.");
    }
}
