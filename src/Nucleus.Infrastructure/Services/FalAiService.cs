using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Infrastructure.Services;

public class FalAiService(IHttpClientFactory httpClientFactory, IConfiguration config) : IImageGenerationService
{
    public async Task<string> GenerateImageAsync(
        string prompt,
        int width = 1024,
        int height = 1024,
        CancellationToken ct = default)
    {
        var apiKey = config["FAL_KEY"]
            ?? throw new InvalidOperationException("FAL_KEY is not configured. Add it to Railway environment variables.");

        var client = httpClientFactory.CreateClient("falai");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Authorization", $"Key {apiKey}");

        var body = new
        {
            prompt,
            image_size = new { width, height },
            num_inference_steps = 4,
            num_images = 1,
            enable_safety_checker = true
        };

        var response = await client.PostAsJsonAsync("https://fal.run/fal-ai/flux/schnell", body, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"fal.ai API error {(int)response.StatusCode}: {error}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

        return json
            .GetProperty("images")[0]
            .GetProperty("url")
            .GetString()
            ?? throw new InvalidOperationException("fal.ai returned no image URL.");
    }
}
