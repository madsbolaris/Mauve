// Copyright (c) Microsoft. All rights reserved.
namespace Mauve.Workflows.GenerateEmailSummaries;

/// <summary>
/// Processes Events emitted by shared steps.<br/>
/// </summary>
public static class GenerateEmailSummariesEvents
{
    public static readonly string NewEmail = nameof(NewEmail);
    public static readonly string EmailSaved = nameof(EmailSaved);
}