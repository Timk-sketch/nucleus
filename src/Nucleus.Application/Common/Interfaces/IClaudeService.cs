namespace Nucleus.Application.Common.Interfaces;

public interface IClaudeService
{
    /// <summary>
    /// Calls Claude API with a system prompt + user prompt and returns the generated text.
    /// </summary>
    Task<string> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        string model = "claude-sonnet-4-6",
        int maxTokens = 4096,
        CancellationToken ct = default);
}
