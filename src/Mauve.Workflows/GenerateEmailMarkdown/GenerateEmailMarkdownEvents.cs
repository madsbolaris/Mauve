// Copyright (c) Microsoft. All rights reserved.
namespace Mauve.Workflows.GenerateEmailMarkdown;

/// <summary>
/// Processes Events emitted by shared steps.<br/>
/// </summary>
public static class GenerateEmailMarkdownEvents
{
    public static readonly string NewEmail = nameof(NewEmail);
    public static readonly string EmailSaved = nameof(EmailSaved);
}