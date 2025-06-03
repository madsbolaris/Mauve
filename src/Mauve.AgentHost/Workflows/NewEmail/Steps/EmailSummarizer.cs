// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Mauve.Core.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.PromptTemplates.Liquid;

namespace Mauve.AgentHost.Workflows.NewEmail.Steps;

/// <summary>
/// Generates a summary for a message and its images using context from the thread.
/// </summary>
public sealed class EmailSummarizer(Kernel kernel, JsonSerializerOptions jsonOptions) : KernelProcessStep
{
    [KernelFunction("summarize_email")]
    public async Task<OutlookMessage> SummarizeEmailAsync(KernelProcessStepContext context, List<OutlookMessage> messages)
    {
        OutlookMessage? current = messages.LastOrDefault(m => string.IsNullOrEmpty(m.Summary));
        if (current is null) return messages.Last();

        var promptTemplate = """
        <message role="system">
        You are an assistant that summarizes email threads. For each message, output a JSON object with the following:
        - a `summary` of the message body
        - an `images` object that maps image `cid`s to their summary

        If a message or image has already been summarized, treat the assistant reply as ground truth and don't repeat it.

        Provide as much detail as possible for images. Especially for images that are graphs, charts, or diagrams, include key insights, trends, or data points that are visually represented.
        For screenshots or photos, describe the content, context, and any important details that are visible.
        </message>

        {% for message in messages %}
            {% for image in message.images %}
                {% if image.summary %}
        <message role="user">![{{ image.alt }}](CID:{{ image.cid }})</message>
                {% else %}
        <message role="user">![{{ image.alt }}](CID:{{ image.cid }})</message>
        <image src="{{ image.uri }}" />
                {% endif %}
            {% endfor %}

        <message role="user">{{ message.body }}</message>

            {% if message.summary %}
        <message role="assistant">
        {
        "summary": "{{ message.summary }}",
        "images": {
            {% assign comma = "" %}
            {% for image in message.images %}
                {% if image.summary %}
        {{ comma }}"{{ image.cid }}": "{{ image.summary }}"
                    {% assign comma = "," %}
                {% endif %}
            {% endfor %}
        }
        }
        </message>
            {% endif %}
        {% endfor %}
        """;

        var imageCids = current.Images?
            .Where(img => !string.IsNullOrWhiteSpace(img?.Cid))
            .Select(img => img!.Cid!)
            .Distinct()
            .ToList() ?? [];

        var imagesProperties = new JsonObject();
        foreach (var cid in imageCids)
        {
            imagesProperties[cid] = new JsonObject
            {
                ["type"] = "string"
            };
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["summary"] = new JsonObject { ["type"] = "string" },
                ["images"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = imagesProperties,
                    ["required"] = new JsonArray(imageCids.Select(cid => (JsonNode)cid).ToArray())
                }
            },
            ["required"] = new JsonArray { "summary", "images" }
        };

        var settings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat(
                "message_summary",
                BinaryData.FromObjectAsJson(schema)
            )
        };

        var arguments = new KernelArguments(settings)
        {
            ["messages"] = messages.Select(m => new
            {
                body = m.Body ?? "",
                summary = m.Summary ?? "",
                images = m.Images?.Select(img => new
                {
                    cid = img?.Cid ?? "",
                    alt = img?.Alt ?? "",
                    uri = img?.Uri ?? new Uri("about:blank"),
                    summary = img?.Summary ?? ""
                }) ?? []
            }).ToList()
        };

        var result = await kernel.InvokePromptAsync(
            promptTemplate,
            arguments,
            LiquidPromptTemplateFactory.LiquidTemplateFormat,
            new LiquidPromptTemplateFactory()
        );

        var parsed = JsonSerializer.Deserialize<SummaryResult>(result.GetValue<string>()!, jsonOptions);
        current.Summary = parsed!.Summary;

        foreach (var (cid, summary) in parsed.Images)
        {
            var img = current.Images?.FirstOrDefault(i => i?.Cid == cid);
            if (img is not null)
                img.Summary = summary;
        }

        // Emit event to save updated message
        await context.EmitEventAsync(Events.SaveEmail, (OutlookMessage)current!);

        return current;
    }
}

public class SummaryResult
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("images")]
    public Dictionary<string, string> Images { get; set; } = [];
}
