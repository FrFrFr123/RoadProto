using RoadProto.Terrain.UI.Agent.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RoadProto.Terrain.UI.Agent.Backend;

public sealed class AgentBackendClient
{
    private static readonly DataContractJsonSerializerSettings JsonSettings = new()
    {
        UseSimpleDictionaryFormat = true,
    };

    private readonly HttpClient _httpClient;

    public AgentBackendClient(AgentBackendOptions options)
        : this(new HttpClient { BaseAddress = options.BaseUri, Timeout = TimeSpan.FromSeconds(5) })
    {
    }

    public AgentBackendClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AgentHealthDto> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("health", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<AgentHealthDto>(response).ConfigureAwait(false);
    }

    public async Task<AgentRunDto> StartRunAsync(
        string? sessionId,
        string message,
        string? currentTemplateHandle = null,
        string? currentTemplateName = null,
        CancellationToken cancellationToken = default)
    {
        var request = new StartAgentRunRequestDto
        {
            SessionId = sessionId,
            Message = message,
            CurrentTemplateHandle = currentTemplateHandle,
            CurrentTemplateName = currentTemplateName,
        };
        using var response = await _httpClient.PostAsync("api/agent/runs", CreateJsonContent(request), cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<AgentRunDto>(response).ConfigureAwait(false);
    }

    public async Task<AgentRunDto> ConfirmAsync(string taskId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"api/agent/runs/{taskId}/confirm", null, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<AgentRunDto>(response).ConfigureAwait(false);
    }

    public async Task<AgentRunDto> CancelAsync(string taskId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"api/agent/runs/{taskId}/cancel", null, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<AgentRunDto>(response).ConfigureAwait(false);
    }

    public async Task<AgentRunDto> PostUserInputAsync(
        string taskId,
        string message,
        string? currentTemplateHandle = null,
        string? currentTemplateName = null,
        CancellationToken cancellationToken = default)
    {
        var request = new AgentRunUserInputRequestDto
        {
            Message = message,
            CurrentTemplateHandle = currentTemplateHandle,
            CurrentTemplateName = currentTemplateName,
        };
        using var response = await _httpClient
            .PostAsync($"api/agent/runs/{taskId}/user-input", CreateJsonContent(request), cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<AgentRunDto>(response).ConfigureAwait(false);
    }

    public async Task<ModelProviderViewDto> SaveModelProviderAsync(
        string provider,
        ModelProviderUpdateDto update,
        CancellationToken cancellationToken = default)
    {
        var routeTemplate = "api/settings/models/{provider}";
        var path = routeTemplate.Replace("{provider}", Uri.EscapeDataString(provider));
        using var response = await _httpClient.PutAsync(path, CreateJsonContent(update), cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<ModelProviderViewDto>(response).ConfigureAwait(false);
    }

    public async Task<Dictionary<string, ModelProviderViewDto>> GetModelProvidersAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("api/settings/models", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<Dictionary<string, ModelProviderViewDto>>(response).ConfigureAwait(false);
    }

    public async Task<AgentRunDto> PostToolResultAsync(
        string taskId,
        AgentToolResultDto result,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient
            .PostAsync($"api/agent/runs/{taskId}/tool-result", CreateJsonContent(result), cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<AgentRunDto>(response).ConfigureAwait(false);
    }

    private static StringContent CreateJsonContent<T>(T value)
    {
        using var stream = new MemoryStream();
        new DataContractJsonSerializer(typeof(T), JsonSettings).WriteObject(stream, value);
        return new StringContent(Encoding.UTF8.GetString(stream.ToArray()), Encoding.UTF8, "application/json");
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return (T)new DataContractJsonSerializer(typeof(T), JsonSettings).ReadObject(stream)!;
    }
}
