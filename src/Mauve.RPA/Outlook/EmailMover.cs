using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Polly;
using Polly.Registry;

namespace Mauve.RPA.Outlook;

public class EmailMover(ILogger<EmailMover> logger, IReadOnlyPolicyRegistry<string> policyRegistry)
{
    private readonly IAsyncPolicy _retryPolicy = policyRegistry.Get<IAsyncPolicy>("PlaywrightRetry");

    public async Task<bool> MoveToProcessedAsync(IPage page, string convoId)
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var convoElement = await page.QuerySelectorAsync($"div[role='option'][data-convid='{convoId}']");
                if (convoElement is null)
                {
                    logger.LogWarning("Conversation {ConversationId} not found in DOM", convoId);
                    throw new PlaywrightException($"Conversation {convoId} not found"); // triggers retry
                }

                await convoElement.ClickAsync(new() { Button = MouseButton.Right });

                await page.WaitForSelectorAsync("div[role='menuitem'][aria-label='Move']", new() { Timeout = 10000 });
                var moveMenu = await page.QuerySelectorAsync("div[role='menuitem'][aria-label='Move']");
                if (moveMenu is null) throw new PlaywrightException("Move menu not found");

                await moveMenu.ClickAsync();

                await page.WaitForSelectorAsync("input[placeholder='Search for a folder']", new() { Timeout = 10000 });
                var searchBox = await page.QuerySelectorAsync("input[placeholder='Search for a folder']");
                if (searchBox is null) throw new PlaywrightException("Search box not found");

                await searchBox.FillAsync("Processed");

                await page.WaitForSelectorAsync("div[role='menuitem'][title='Processed']", new() { Timeout = 10000 });
                var targetFolder = await page.QuerySelectorAsync("div[role='menuitem'][title='Processed']");
                if (targetFolder is null) throw new PlaywrightException("Target folder not found");

                await targetFolder.ClickAsync();

                logger.LogInformation("Moved conversation {ConversationId} to 'Processed'", convoId);
                return true;
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to move conversation {ConversationId} to 'Processed'", convoId);
            return false;
        }
    }
}
