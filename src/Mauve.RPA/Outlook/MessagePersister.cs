using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mauve.Core.Models;
using Polly;
using Polly.Registry;
using Mauve.Core.Settings;
using Mauve.Core.Helpers;

namespace Mauve.RPA.Outlook;

public class MessagePersister(
    IOptions<PathSettings> options,
    ILogger<MessagePersister> logger,
    IReadOnlyPolicyRegistry<string> policyRegistry,
    JsonSerializerOptions jsonOptions)
{
    private readonly string _baseDir = PathUtils.ExpandHome(options.Value.NewEmailStorageDirectory);
    private readonly IAsyncPolicy _fileRetryPolicy = policyRegistry.Get<IAsyncPolicy>("FileRetry");

    public async Task SaveAsync(OutlookMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var folder = Path.Combine(_baseDir, message.ConversationId);
            Directory.CreateDirectory(folder);
            
            var jsonPath = Path.Combine(folder, $"{message.MessageId}.json");
            var json = JsonSerializer.Serialize(message, jsonOptions);

            await _fileRetryPolicy.ExecuteAsync(() =>
                File.WriteAllTextAsync(jsonPath, json, cancellationToken));
            logger.LogInformation("Saved message {MessageId} to {Path}", message.MessageId, jsonPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist message {MessageId} in conversation {ConversationId}", message.MessageId, message.ConversationId);
            throw;
        }
    }

    public bool ShouldSkip(string conversationId, string messageId, DateTime timestamp)
    {
        var rawPath = Path.Combine(_baseDir, conversationId, $"{messageId}.json");

        // Check markdown directory for an existing .md file
        var markdownBaseDir = PathUtils.ExpandHome(options.Value.EmailsWithMarkdownDirectory);
        var markdownDir = Path.Combine(markdownBaseDir, conversationId);
        var markdownFilePattern = $"{timestamp:yyyy-MM-dd-HH-mm-ss}-{messageId}.md";
        var markdownPath = Path.Combine(markdownDir, markdownFilePattern);

        var rawExists = File.Exists(rawPath);
        var markdownExists = File.Exists(markdownPath);

        if (rawExists || markdownExists)
        {
            logger.LogDebug("Skipping already saved message {MessageId} in conversation {ConversationId}", messageId, conversationId);
        }

        return rawExists || markdownExists;
    }

}
