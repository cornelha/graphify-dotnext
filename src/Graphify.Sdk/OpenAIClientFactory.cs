using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Graphify.Sdk;

/// <summary>
/// Factory for creating IChatClient instances configured for any OpenAI-compatible endpoint.
/// Supports OpenAI API, LocalAI, LiteLLM, vLLM, and any other OpenAI-compatible service.
/// </summary>
public static class OpenAIClientFactory
{
    /// <summary>
    /// Creates an IChatClient using an OpenAI-compatible endpoint with API key authentication.
    /// </summary>
    /// <param name="options">OpenAI-compatible configuration (endpoint, key, model).</param>
    /// <returns>An IChatClient wired to the specified endpoint.</returns>
    public static IChatClient Create(OpenAIOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(options.Endpoint)
        };

        var client = new OpenAI.OpenAIClient(new ApiKeyCredential(options.ApiKey), clientOptions);
        return client.GetChatClient(options.ModelId).AsIChatClient();
    }
}
