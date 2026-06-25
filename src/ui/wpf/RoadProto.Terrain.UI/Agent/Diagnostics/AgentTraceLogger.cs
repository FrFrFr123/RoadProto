using System;
using System.IO;

namespace RoadProto.Terrain.UI.Agent.Diagnostics;

public sealed class AgentTraceLogger
{
    private readonly string _logDirectory;

    public AgentTraceLogger(string logDirectory)
    {
        _logDirectory = logDirectory;
    }

    public string LogDirectory => _logDirectory;

    public void Write(string traceId, string stage, string message)
    {
        Directory.CreateDirectory(_logDirectory);
        var path = Path.Combine(_logDirectory, $"roadproto-{DateTime.UtcNow:yyyyMMdd}.jsonl");
        var line = $"{{\"timestamp\":\"{DateTimeOffset.UtcNow:O}\",\"traceId\":\"{Escape(traceId)}\",\"stage\":\"{Escape(stage)}\",\"message\":\"{Escape(message)}\"}}";
        File.AppendAllText(path, line + Environment.NewLine);
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
