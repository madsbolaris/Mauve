// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Mauve.Core.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.PromptTemplates.Liquid;

namespace Mauve.AgentHost.Workflows.NewEmail.Steps;

/// <summary>
/// Converts the HTML body of an Outlook message to Markdown, referencing CID images.
/// </summary>
public sealed class HtmlToMarkdownConverter(Kernel kernel, JsonSerializerOptions jsonOptions) : KernelProcessStep
{
    [KernelFunction("html_to_markdown")]
    public async ValueTask<OutlookMessage> HtmlToMarkdownAsync(KernelProcessStepContext context, OutlookMessage message)
    {
        var promptTemplate = """
        <message role="system">
        Convert this email into markdown. Use the CID when referencing images (as shown in the examples below). Do not include _any_ HTML tags or formatting. It should always start with the following:

        ```md
        **From:** [From Name]  
        **To:** [To Name(s)]  
        **Date:** [Date and Time (yyyy-MM-dd HH:mm:ss)]

        ---
        ```

        Do not include ```md in the output, just the markdown content
        </message>

        <message role="system">Examples of image references:</message>

        {% for image in images %}
        <message role="user">
            <text>![{{ image.alt }}](CID:{{ image.cid }})</text>
        </message>
        {% endfor %}

        <message role="system">End of examples. Now process the email body:</message>

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
                cid = img?.Cid ?? "",
                uri = img?.Uri ?? new Uri("about:blank")
            }).ToArray(),
            ["body"] = message.Body ?? ""
        };

        var result = await kernel.InvokePromptAsync(
            promptTemplate,
            arguments,
            LiquidPromptTemplateFactory.LiquidTemplateFormat,
            new LiquidPromptTemplateFactory()
        );

        var parsed = JsonSerializer.Deserialize<MarkdownObject>(result.GetValue<string>()!, jsonOptions);
        message.Body = parsed!.Markdown;

        await context.EmitEventAsync(Events.SaveEmail, message);

        return message;
    }
}

public class MarkdownObject()
{
    [JsonPropertyName("markdown")]   
    public string Markdown { get; set; } = string.Empty;
    
}
