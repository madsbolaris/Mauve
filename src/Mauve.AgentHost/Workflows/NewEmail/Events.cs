// Copyright (c) Microsoft. All rights reserved.
namespace Mauve.AgentHost.Workflows.NewEmail;

/// <summary>
/// Processes Events emitted by shared steps.<br/>
/// </summary>
public static class Events
{
    public static readonly string NewEmail = nameof(NewEmail);
    public static readonly string SaveEmail = nameof(SaveEmail);
    public static readonly string RequestSummary = nameof(RequestSummary);
    public static readonly string ExtractEntities = nameof(ExtractEntities);
    public static readonly string NewEntity = nameof(NewEntity);
}