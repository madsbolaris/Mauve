using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Mauve.Core.Models;
using Polly;
using Polly.Registry;
using System.Text.RegularExpressions;

namespace Mauve.RPA.Outlook;

public class MessageExtractor(
    PersonExtractor personExtractor,
    InlineImageExtractor imageExtractor,
    ILogger<MessageExtractor> logger,
    IReadOnlyPolicyRegistry<string> policyRegistry)
{
    private readonly IAsyncPolicy _playwrightRetryPolicy = policyRegistry.Get<IAsyncPolicy>("PlaywrightRetry");

    public async Task<IElementHandle?> FindNextConversationAsync(IPage page, HashSet<string> seen, CancellationToken cancellationToken)
    {
        var conversations = await page.QuerySelectorAllAsync("div[role='option'][data-focusable-row='true']");
        foreach (var convo in conversations)
        {
            var convoId = await convo.GetAttributeAsync("data-convid");
            if (!string.IsNullOrEmpty(convoId) && !seen.Contains(convoId))
                return convo;
        }

        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        return null;
    }

    public async IAsyncEnumerable<OutlookMessage> ExtractMessagesAsync(
    IPage page,
    string convoId,
    CancellationToken cancellationToken,
    Func<string, string, DateTime, bool> shouldSkip)
    {
        try
        {
            await _playwrightRetryPolicy.ExecuteAsync(async () =>
            {
                var conversationElement = await page.QuerySelectorAsync($"div[role='option'][data-convid='{convoId}']");
                await conversationElement!.ScrollIntoViewIfNeededAsync();
                await conversationElement.ClickAsync();
            });
        }
        catch (PlaywrightException ex)
        {
            logger.LogWarning(ex, "Conversation {ConversationId} could not be interacted with", convoId);
            throw;
        }

        await page.WaitForSelectorAsync("div[data-app-section='ConversationContainer'] > div > div > div[aria-expanded], div#extendedCardFullViewCollapsableWrapperBodyCustomScrollBar", new() { Timeout = 30000 });

        var subjectHandle = await page.QuerySelectorAsync("div[role='heading'][aria-level='2'] span");
        var subject = await subjectHandle!.InnerTextAsync();

        string? previousMessageId = null;

        var containers = (await page.QuerySelectorAllAsync("div[data-app-section='ConversationContainer'] > div > div > div[aria-expanded], div#extendedCardFullViewCollapsableWrapperBodyCustomScrollBar"))
            .Reverse().ToList();

        foreach (var container in containers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if ((await container.GetAttributeAsync("aria-expanded")) == "false")
            {
                await container.ClickAsync();
                await container.WaitForElementStateAsync(ElementState.Stable);
                await container.WaitForSelectorAsync("div.wide-content-host, div[role='document']", new() { Timeout = 10000 });
            }

            var blocks = await container.QuerySelectorAllAsync("div.wide-content-host, div.qaYammerOutlookThreadView");
            if (blocks.Count == 0) continue;

            // Try to find a datetime-like substring
            var timestampElement = await container.QuerySelectorAsync("div[data-testid='SentReceivedSavedTime']");
            if (timestampElement is null)
            {
                throw new InvalidOperationException($"Timestamp element not attached to the DOM");
            }

            var rawText = await timestampElement.InnerTextAsync();

            // Match common datetime formats like "Fri 4/11/2025 2:07 PM"
            var match = Regex.Match(rawText, @"\b(?:Mon|Tue|Wed|Thu|Fri|Sat|Sun)?\s*\d{1,2}/\d{1,2}/\d{4}\s+\d{1,2}:\d{2}\s*(?:AM|PM)?", RegexOptions.IgnoreCase);
            if (!match.Success || !DateTime.TryParse(match.Value, out var timestamp))
            {
                throw new InvalidOperationException(
                    $"Message in conversation {convoId} has unparseable timestamp: '{rawText}'"
                );
            }


            var html = new StringBuilder();
            foreach (var block in blocks)
                html.Append(await block.InnerHTMLAsync());

            var body = html.ToString();
            var from = await personExtractor.ExtractFromAsync(page, blocks[0]);

            var messageId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{convoId}|{string.Join(",", from.Select(p => p.Email))}|{timestamp:O}")))
                .ToLowerInvariant()[..6];

            if (shouldSkip(convoId, messageId, timestamp))
            {
                previousMessageId = messageId;
                logger.LogInformation("Skipping message {MessageId} in conversation {ConversationId}", messageId, convoId);
                continue;
            }

            var to = await personExtractor.ExtractRecipientsAsync(page, blocks[0], "To:");
            var cc = await personExtractor.ExtractRecipientsAsync(page, blocks[0], "Cc:");
            var images = await imageExtractor.ExtractInlineImagesAsync(page, blocks[0]);

            if (string.IsNullOrWhiteSpace(body))
            {
                throw new InvalidOperationException($"Message {messageId} in conversation {convoId} has an empty body.");
            }

            var message = new OutlookMessage(convoId)
            {
                MessageId = messageId,
                PreviousMessageId = previousMessageId,
                Subject = subject,
                Timestamp = timestamp,
                Body = body,
                From = from,
                To = to,
                Cc = cc,
                Images = images
            };

            yield return message;

            previousMessageId = messageId;
        }
    }

}
