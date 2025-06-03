// Copyright (c) Microsoft. All rights reserved.

using Mauve.Core.Models;
using Microsoft.SemanticKernel;

namespace Mauve.AgentHost.Workflows.NewEmail.Steps;

/// <summary>
/// Executes RAG response generation for a list of triaged questions.
/// </summary>
public sealed class NewEmailOrchestrator() : KernelProcessStep
{
    private readonly Dictionary<string, OutlookMessage> Messages = [];

    [KernelFunction("new_email")]
    public async Task HandleNewEmailAsync(KernelProcessStepContext context, OutlookMessage message)
    {
        Messages[message.MessageId!] = message;

        // Attempt to walk the chain to the root
        var chain = new List<OutlookMessage>();
        var current = message;

        while (current != null)
        {
            chain.Insert(0, current); // oldest first
            if (string.IsNullOrEmpty(current.PreviousMessageId))
                break;

            if (!Messages.TryGetValue(current.PreviousMessageId, out var prev))
            {
                // Wait for the ancestor message to arrive
                return;
            }

            current = prev;
        }

        // Check if the oldest message without a summary exists
        var nextToSummarize = chain.FirstOrDefault(m => string.IsNullOrEmpty(m.Summary));
        if (nextToSummarize == null)
            return;

        var ancestors = chain
            .Where(m => m.Timestamp <= nextToSummarize.Timestamp)
            .ToList();

        await context.EmitEventAsync(Events.RequestSummary, ancestors);
    }

    [KernelFunction("new_summary")]
    public async Task HandleNewSummaryAsync(KernelProcessStepContext context, OutlookMessage updatedMessage)
    {
        if (string.IsNullOrEmpty(updatedMessage.MessageId) || string.IsNullOrEmpty(updatedMessage.Summary))
            return;

        if (Messages.TryGetValue(updatedMessage.MessageId, out var existing))
        {
            existing.Summary = updatedMessage.Summary;
        }
        else
        {
            Messages[updatedMessage.MessageId] = updatedMessage;
        }

        // Find direct child in the thread
        var child = Messages.Values
            .FirstOrDefault(m => m.PreviousMessageId == updatedMessage.MessageId);

        if (child == null || !string.IsNullOrEmpty(child.Summary))
            return;

        // Build ancestor chain for the child
        var chain = new List<OutlookMessage>();
        var current = child;
        while (current != null)
        {
            chain.Insert(0, current);
            if (string.IsNullOrEmpty(current.PreviousMessageId))
                break;

            if (!Messages.TryGetValue(current.PreviousMessageId, out var prev))
                return;

            current = prev;
        }

        await context.EmitEventAsync(Events.RequestSummary, chain);
    }
}
