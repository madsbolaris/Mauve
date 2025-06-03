// // Copyright (c) Microsoft. All rights reserved.

// using System.Text.Json;
// using System.Text.Json.Nodes;
// using System.Text.Json.Serialization;
// using Mauve.Core.Models;
// using Microsoft.SemanticKernel;
// using Microsoft.SemanticKernel.ChatCompletion;
// using Microsoft.SemanticKernel.Connectors.OpenAI;
// using Microsoft.SemanticKernel.PromptTemplates.Liquid;
// using Polly;
// using Polly.Registry;
// using Microsoft.Extensions.Logging;

// namespace Mauve.Workflows.GenerateEmailSummaries.Steps;

// /// <summary>
// /// Converts the HTML body of an Outlook message to Markdown, referencing CID images.
// /// </summary>
// public sealed class GenerateSummaries(
//     Kernel kernel,
//     IReadOnlyPolicyRegistry<string> policyRegistry,
//     ILogger<GenerateSummaries> logger) : KernelProcessStep
// {
//     private readonly IAsyncPolicy retryPolicy = policyRegistry.Get<IAsyncPolicy>("KernelRetry");

//     [KernelFunction("html_to_markdown")]
//     public async ValueTask<List<FunctionCallContent>> InvokeAsync(KernelProcessStepContext context, List<OutlookMessage> messages)
//     {
//         var promptTemplate = /* trimmed for brevity — keep your full template here */ "...";

//         // Prepare the argument that will be used in the Liquid template
//         var liquidMessages = new JsonArray();
//         foreach (var message in messages)
//         {
//             var obj = new JsonObject
//             {
//                 ["conversationId"] = message.ConversationId,
//                 ["body"] = message.Body
//             };

//             if (message.SummaryJson is not null)
//             {
//                 obj["summary_json"] = message.SummaryJson;
//             }

//             if (message.Images?.Count > 0)
//             {
//                 var imageArray = new JsonArray();
//                 foreach (var img in message.Images)
//                 {
//                     var imageObj = new JsonObject
//                     {
//                         ["alt"] = img.Alt ?? string.Empty,
//                         ["cid"] = img.Cid ?? string.Empty,
//                         ["path"] = img.Path ?? string.Empty
//                     };
//                     imageArray.Add(imageObj);
//                 }
//                 obj["images"] = imageArray;
//             }

//             liquidMessages.Add(obj);
//         }

//         var arguments = new KernelArguments(new PromptExecutionSettings
//         {
//             FunctionChoiceBehavior = FunctionChoiceBehavior.Required()
//         })
//         {
//             ["messages"] = liquidMessages
//         };

//         try
//         {
//             var result = await retryPolicy.ExecuteAsync(() =>
//                 kernel.InvokePromptAsync(
//                     promptTemplate,
//                     arguments,
//                     LiquidPromptTemplateFactory.LiquidTemplateFormat,
//                     new LiquidPromptTemplateFactory()
//                 )
//             );

//             if (result.Items.TryGetValue("content", out var content) &&
//                 content is string rawContent &&
//                 FunctionCallContent.TryParseAll(rawContent, out var calls))
//             {
//                 logger.LogInformation("Successfully generated {Count} function calls", calls.Count);
//                 return calls;
//             }

//             logger.LogWarning("No function call content found in result.");
//             return new();
//         }
//         catch (Exception ex)
//         {
//             logger.LogError(ex, "Failed to generate function calls from email summary prompt.");
//             return new();
//         }
//     }
// }

// public class MarkdownObject
// {
//     [JsonPropertyName("markdown")]
//     public string Markdown { get; set; } = string.Empty;
// }
