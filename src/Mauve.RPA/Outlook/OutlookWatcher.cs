using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Mauve.Core;

namespace Mauve.RPA.Outlook;

public class OutlookWatcher(
    MessageExtractor extractor,
    PageHelpers pageHelpers,
    EmailMover mover,
    MessagePersister persister,
    ILogger<OutlookWatcher> logger
) : IRPAWatcher
{
    private readonly LinkedList<string> _processed = new();

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunSessionAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[OutlookWatcher] Session failed. Restarting.");
                await Task.Delay(2000, cancellationToken);
            }
        }
    }

    private async Task RunSessionAsync(CancellationToken cancellationToken)
    {
        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchPersistentContextAsync("playwright-user-data", new()
        {
            Headless = false,
            ExecutablePath = "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
        });

        var page = await browser.NewPageAsync();
        await pageHelpers.RefreshPageAsync(page); // already uses internal retry policy

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var next = await extractor.FindNextConversationAsync(page, _processed.ToHashSet(), cancellationToken);
                if (next is null) continue;

                var convoId = await next.GetAttributeAsync("data-convid");
                if (string.IsNullOrWhiteSpace(convoId)) continue;

                await foreach (var msg in extractor.ExtractMessagesAsync(page, convoId, cancellationToken, persister.ShouldSkip))
                {
                    await persister.SaveAsync(msg, cancellationToken);
                }

                if (await mover.MoveToProcessedAsync(page, convoId))
                {
                    if (!_processed.Contains(convoId))
                        _processed.AddLast(convoId);

                    while (_processed.Count > 100)
                        _processed.RemoveFirst();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[OutlookWatcher] Failed to process conversation. Refreshing page...");

                try
                {
                    await page.CloseAsync();
                }
                catch (Exception closeEx)
                {
                    logger.LogWarning(closeEx, "[OutlookWatcher] Failed to close page");
                }

                page = await browser.NewPageAsync();
                await pageHelpers.RefreshPageAsync(page); // handles retry internally
            }
        }

        await page.CloseAsync();
    }
}
