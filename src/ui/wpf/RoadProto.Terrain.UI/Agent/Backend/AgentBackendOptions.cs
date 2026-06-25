using System;
using System.IO;

namespace RoadProto.Terrain.UI.Agent.Backend;

public sealed class AgentBackendOptions
{
    public Uri BaseUri { get; set; } = new("http://127.0.0.1:17861/");

    public string PublishExePath { get; set; } =
        @"F:\0_GPT_RoadProtoAgentBackend\artifacts\publish\RoadProtoAgentBackend.exe";

    public string DevProjectPath { get; set; } =
        @"F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Api\RoadProtoAgentBackend.Api.csproj";

    public string StorageRoot { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RoadProtoAgent");

    public string LogRoot { get; set; } =
        @"F:\0_GPT_RoadProtoAgentRuntime\logs";

    public string RoadProtoLogDirectory =>
        Path.Combine(LogRoot, "roadproto");
}
