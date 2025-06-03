using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Mauve.Core.Models;
using Polly;
using Polly.Retry;
using Polly.Registry;

namespace Mauve.RPA.Outlook;

public class PersonExtractor(ILogger<PersonExtractor> logger, IReadOnlyPolicyRegistry<string> policyRegistry)
{
    private readonly IAsyncPolicy _retryPolicy = policyRegistry.Get<IAsyncPolicy>("PlaywrightRetry");
    public async Task<List<Person>> ExtractFromAsync(IPage page, IElementHandle msg)
        => await ExtractPeopleFromButtonFieldsAsync(page, msg, "From:");

    public async Task<List<Person>> ExtractRecipientsAsync(IPage page, IElementHandle msg, string field)
    {
        var people = new List<Person>();

        await _retryPolicy.ExecuteAsync(async () =>
        {
            var container = await msg.QuerySelectorAsync($"div[role='edit'][aria-label^='{field}']");
            if (container is null)
            {
                if (field != "Cc:")
                {
                    logger.LogWarning("Could not find container for field '{Field}'", field);
                }
                return;
            }

            var expand = await container.QuerySelectorAsync("button[id='plusOthersButton']");
            if (expand is not null)
            {
                await expand.ClickAsync();
                await expand.WaitForElementStateAsync(ElementState.Stable);
            }

            var spans = await container.QuerySelectorAllAsync("span[aria-label][role='button']");
            foreach (var span in spans)
            {
                var name = await span.GetAttributeAsync("aria-label");
                string? email = null;

                await _retryPolicy.ExecuteAsync(async () =>
                {
                    try
                    {
                        await span.ClickAsync();
                        await page.WaitForSelectorAsync("div[data-log-name='Chat'], div[data-log-name='Email']", new() { Timeout = 10000 });
                        email = await TryGetEmailFromModalAsync(page);
                    }
                    finally
                    {
                        await page.Keyboard.PressAsync("Escape");
                    }
                });

                if (!string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(email))
                    people.Add(new Person { DisplayName = name, Email = email });
            }
        });

        return people;
    }

    private async Task<List<Person>> ExtractPeopleFromButtonFieldsAsync(IPage page, IElementHandle msg, string prefix)
    {
        var people = new List<Person>();

        await _retryPolicy.ExecuteAsync(async () =>
        {
            var buttons = await msg.QuerySelectorAllAsync($"span[aria-label^='{prefix}'][role='button'][aria-haspopup='dialog']:not([tabindex='-1'])");

            foreach (var btn in buttons)
            {
                var name = await btn.InnerTextAsync();
                string? email = null;

                await _retryPolicy.ExecuteAsync(async () =>
                {
                    await btn.ClickAsync();
                    await page.WaitForSelectorAsync("div[data-log-name='Chat'], div[data-log-name='Email']", new() { Timeout = 30000 });
                    email = await TryGetEmailFromModalAsync(page);
                    await page.Keyboard.PressAsync("Escape");
                });

                if (!string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(email))
                    people.Add(new Person { DisplayName = name, Email = email });
            }
        });

        return people
                .Where(p => !string.IsNullOrWhiteSpace(p.Email) || !string.IsNullOrWhiteSpace(p.DisplayName))
                .DistinctBy(p => $"{p.Email?.ToLowerInvariant()}|{p.DisplayName?.ToLowerInvariant()}")
                .ToList();
    }

    private async Task<string> TryGetEmailFromModalAsync(IPage page)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var modal = await page.QuerySelectorAsync("div[data-log-name='Chat']")
                     ?? await page.QuerySelectorAsync("div[data-log-name='Email']");
            var span = await modal!.QuerySelectorAsync("button span[title]");
            var title = await span!.GetAttributeAsync("title");

            if (!string.IsNullOrWhiteSpace(title))
                return title.Trim();

            throw new InvalidOperationException("Email title was empty or null.");
        });
    }
}
