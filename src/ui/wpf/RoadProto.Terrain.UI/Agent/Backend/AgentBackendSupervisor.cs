using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RoadProto.Terrain.UI.Agent.Backend;

public sealed class AgentBackendSupervisor
{
    private readonly AgentBackendOptions _options;
    private readonly AgentBackendClient _client;

    public AgentBackendSupervisor(AgentBackendOptions options, AgentBackendClient client)
    {
        _options = options;
        _client = client;
    }

    public async Task<string> EnsureBackendAsync(CancellationToken cancellationToken = default)
    {
        if (await IsHealthyAsync(cancellationToken).ConfigureAwait(false))
        {
            return "connected";
        }

        StartBackendProcess();

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsHealthyAsync(cancellationToken).ConfigureAwait(false))
            {
                return File.Exists(_options.PublishExePath) ? "started" : "started-dev";
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Agent backend health check timed out after start request.");
    }

    private async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var health = await _client.GetHealthAsync(cancellationToken).ConfigureAwait(false);
            return string.Equals(health.Status, "healthy", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void StartBackendProcess()
    {
        if (File.Exists(_options.PublishExePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _options.PublishExePath,
                WorkingDirectory = Path.GetDirectoryName(_options.PublishExePath) ?? Environment.CurrentDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            return;
        }

        if (!File.Exists(_options.DevProjectPath))
        {
            throw new FileNotFoundException("Agent backend executable and development project were not found.", _options.PublishExePath);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{_options.DevProjectPath}\"",
            WorkingDirectory = Path.GetDirectoryName(_options.DevProjectPath) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
    }
}
