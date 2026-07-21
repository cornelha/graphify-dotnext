namespace Graphify.Sdk;

/// <summary>
/// Configuration options for OpenAI-compatible provider.
/// Works with any OpenAI-compatible endpoint (OpenAI API, LocalAI, LiteLLM, vLLM, etc.).
/// </summary>
/// <param name="Endpoint">Service endpoint URL, e.g. "https://api.openai.com/v1"</param>
/// <param name="ApiKey">API key for the service.</param>
/// <param name="ModelId">Model identifier, e.g. "gpt-4o" or "llama3.2".</param>
public record OpenAIOptions(
    string Endpoint,
    string ApiKey,
    string ModelId
);
