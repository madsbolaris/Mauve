// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using Mauve.Core.Models;
using Microsoft.SemanticKernel;

namespace Mauve.AgentHost.Workflows.NewEmail.Steps;

/// <summary>
/// Saves a markdown-formatted email to disk, including any associated images.
/// </summary>
public sealed class EmailSaver() : KernelProcessStep
{
    private static string EscapeYaml(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        return input.Replace("\"", "\\\"").Replace("\n", "\\n");
    }

    [KernelFunction("save_email")]
    public static async Task SaveEmailAsync(KernelProcessStepContext context, OutlookMessage message)
    {
        try
        {
            var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "data", "emails");
            var convoDir = Path.Combine(basePath, message.ConversationId ?? "unknown");
            var imagesDir = Path.Combine(convoDir, "images");

            Directory.CreateDirectory(convoDir);
            Directory.CreateDirectory(imagesDir);

            var markdownImages = new List<(string Path, string? Alt, string? Summary)>();
            foreach (var img in message.Images ?? [])
            {
                var fileName = $"{img.Cid}{img.FileExtension}";
                var imagePath = Path.Combine(imagesDir, fileName);
                await File.WriteAllBytesAsync(imagePath, img.Bytes);

                markdownImages.Add((Path.Combine("images", fileName), img.Alt, img.Summary));
            }

            var timestamp = message.Timestamp;
            var filename = $"{timestamp:yyyy-MM-dd-HH-mm-ss}-{message.MessageId}.md";
            var fullPath = Path.Combine(convoDir, filename);

            var yaml = new StringBuilder();
            yaml.AppendLine("---");
            yaml.AppendLine($"conversation_id: \"{message.ConversationId}\"");
            yaml.AppendLine($"message_id: \"{message.MessageId}\"");
            yaml.AppendLine($"previous_message_id: {(message.PreviousMessageId is null ? "null" : $"\"{message.PreviousMessageId}\"")}");
            yaml.AppendLine($"timestamp: \"{timestamp:O}\"");
            yaml.AppendLine($"subject: \"{EscapeYaml(message.Subject)}\"");
            yaml.AppendLine($"summary: {(message.Summary is null ? "null" : $"\"{EscapeYaml(message.Summary)}\"")}");

            yaml.AppendLine("images:");
            foreach (var (path, alt, summary) in markdownImages)
            {
                yaml.AppendLine($"  - path: \"{path}\"");
                yaml.AppendLine($"    alt: \"{EscapeYaml(alt)}\"");
                yaml.AppendLine($"    summary: {(string.IsNullOrWhiteSpace(summary) ? "null" : $"\"{EscapeYaml(summary)}\"")}");
            }

            yaml.AppendLine("from:");
            foreach (var p in message.From ?? [])
                yaml.AppendLine($"  - name: \"{EscapeYaml(p.DisplayName)}\"\n    email: \"{p.Email}\"");

            yaml.AppendLine("to:");
            foreach (var p in message.To ?? [])
                yaml.AppendLine($"  - name: \"{EscapeYaml(p.DisplayName)}\"\n    email: \"{p.Email}\"");

            yaml.AppendLine("cc:");
            foreach (var p in message.Cc ?? [])
                yaml.AppendLine($"  - name: \"{EscapeYaml(p.DisplayName)}\"\n    email: \"{p.Email}\"");

            yaml.AppendLine("---");

            await File.WriteAllTextAsync(fullPath, yaml + "\n" + (message.Body ?? ""));
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[error] Failed to save email: {ex.Message}");
            Console.ResetColor();
        }
    }
}
