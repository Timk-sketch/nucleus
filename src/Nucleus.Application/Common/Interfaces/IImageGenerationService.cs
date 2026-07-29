namespace Nucleus.Application.Common.Interfaces;

public interface IImageGenerationService
{
    /// <summary>
    /// Generates an image from a text prompt and returns the public URL.
    /// </summary>
    Task<string> GenerateImageAsync(
        string prompt,
        int width = 1024,
        int height = 1024,
        CancellationToken ct = default);
}
