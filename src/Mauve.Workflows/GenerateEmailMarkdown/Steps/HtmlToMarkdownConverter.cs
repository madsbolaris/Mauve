// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Mauve.Core.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.PromptTemplates.Liquid;
using Polly;
using Polly.Registry;
using Microsoft.Extensions.Logging;

namespace Mauve.Workflows.GenerateEmailMarkdown.Steps;

/// <summary>
/// Converts the HTML body of an Outlook message to Markdown, referencing CID images.
/// </summary>
public sealed class HtmlToMarkdownConverter(
    Kernel kernel,
    JsonSerializerOptions jsonOptions,
    IReadOnlyPolicyRegistry<string> policyRegistry,
    ILogger<HtmlToMarkdownConverter> logger) : KernelProcessStep
{
    private readonly IAsyncPolicy retryPolicy = policyRegistry.Get<IAsyncPolicy>("KernelRetry");

    [KernelFunction("html_to_markdown")]
    public async ValueTask<OutlookMessage> InvokeAsync(KernelProcessStepContext context, OutlookMessage message)
    {
        var promptTemplate = """
        <message role="system">
        Convert this email into markdown. Do not include _any_ HTML tags or formatting. It should always start with the following:

        ```md
        **From:** [From Name]  
        **To:** [To Name(s)]  
        **Date:** [Date and Time (yyyy-MM-dd HH:mm:ss)]

        ---
        ```

        {% if images.size > 0 %}
        Use the CID when referencing images that start with https://attachments.office.net (as shown in the examples below).

        Examples of image references:
        {% for image in images %}
        ![{{ image.alt }}]({{ image.path }})
        {% endfor %}
        {% endif %}

        Do not include ```md in the output, just the markdown content. Now process the email body:
        </message>

        <message role="user">{{ body }}</message>
        """;

        var arguments = new KernelArguments(
            new OpenAIPromptExecutionSettings
            {
                ResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat(
                    "markdown",
                    BinaryData.FromString("""
                        {
                            "type": "object",
                            "properties": {
                                "markdown": { "type": "string" }
                            },
                            "required": ["markdown"]
                        }
                        """)
                )
            }
        )
        {
            ["images"] = message.Images?.Select(img => new
            {
                alt = img?.Alt ?? "",
                path = img?.Path
            }).ToArray(),
            ["body"] = message.Body ?? ""
        };

        try
        {
            var result = await retryPolicy.ExecuteAsync(() =>
                kernel.InvokePromptAsync(
                    promptTemplate,
                    arguments,
                    LiquidPromptTemplateFactory.LiquidTemplateFormat,
                    new LiquidPromptTemplateFactory()
                )
            );

            var parsed = JsonSerializer.Deserialize<MarkdownObject>(result.GetValue<string>()!, jsonOptions);
            message.Body = parsed!.Markdown;
            logger.LogInformation("Successfully converted HTML to Markdown for message {MessageId}", message.MessageId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to convert HTML to Markdown for message {MessageId}", message.MessageId);
        }

        return message;
    }
}

public class MarkdownObject
{
    [JsonPropertyName("markdown")]
    public string Markdown { get; set; } = string.Empty;
}
