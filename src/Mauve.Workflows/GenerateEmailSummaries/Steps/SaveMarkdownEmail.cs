// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using Mauve.Core.Helpers;
using Mauve.Core.Models;
using Mauve.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mauve.Workflows.GenerateEmailSummaries.Steps;

/// <summary>
/// Saves a markdown-formatted email to disk, including any associated images.
/// </summary>
public sealed class SaveMarkdownEmail(
    IOptions<PathSettings> pathSettings,
    ILogger<SaveMarkdownEmail> logger) : KernelProcessStep
{
    private readonly string _markdownBasePath = PathUtils.ExpandHome(pathSettings.Value.EmailsWithMarkdownDirectory);
    private readonly string _rawEmailBasePath = PathUtils.ExpandHome(pathSettings.Value.NewEmailStorageDirectory);


    [KernelFunction("save_email")]
    public async Task InvokeAsync(KernelProcessStepContext context, OutlookMessage message)
    {
        try
        {
            var convoDir = Path.Combine(_markdownBasePath, message.ConversationId ?? "unknown");
            var imagesDir = Path.Combine(convoDir, "images");

            Directory.CreateDirectory(convoDir);

            var markdownImages = new List<object>();
            if (message.Images?.Count > 0)
            {
                Directory.CreateDirectory(imagesDir);

                foreach (var img in message.Images)
                {
                    var fileName = $"{img.Cid}{img.FileExtension}";
                    var originalPath = Path.Combine(_rawEmailBasePath, message.ConversationId!, "images", fileName);

                    if (!File.Exists(originalPath))
                    {
                        logger.LogWarning("Missing image file: {Path}", originalPath);
                        continue;
                    }

                    var destPath = Path.Combine(imagesDir, fileName);
                    File.Copy(originalPath, destPath, overwrite: true);

                    markdownImages.Add(new
                    {
                        path = Path.Combine("images", fileName),
                        alt = img.Alt,
                        summary = img.Summary
                    });
                }
            }

            var metadata = new
            {
                conversation_id = message.ConversationId,
                message_id = message.MessageId,
                previous_message_id = message.PreviousMessageId,
                timestamp = message.Timestamp.ToString("O"),
                subject = message.Subject,
                images = markdownImages,
                from = message.From?.Select(p => new { name = p.DisplayName, email = p.Email }),
                to = message.To?.Select(p => new { name = p.DisplayName, email = p.Email }),
                cc = message.Cc?.Select(p => new { name = p.DisplayName, email = p.Email }),
            };

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var yaml = serializer.Serialize(metadata);
            var markdownContent = "---\n" + yaml + "---\n\n" + (message.Body ?? "");

            var timestamp = message.Timestamp;
            var filename = $"{timestamp:yyyy-MM-dd-HH-mm-ss}-{message.MessageId}.md";
            var fullPath = Path.Combine(convoDir, filename);

            await File.WriteAllTextAsync(fullPath, markdownContent);
            logger.LogInformation("Saved email to {Path}", fullPath);

            await context.EmitEventAsync(GenerateEmailSummariesEvents.EmailSaved, message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save email for conversation {ConversationId}", message.ConversationId);
        }
    }
}
