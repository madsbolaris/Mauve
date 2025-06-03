// Copyright (c) Microsoft. All rights reserved.

using Mauve.Core.Helpers;
using Mauve.Core.Models;
using Mauve.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace Mauve.Workflows.GenerateEmailMarkdown.Steps;

/// <summary>
/// Deletes the raw JSON email and any referenced image files.
/// </summary>
public sealed class DeleteRawEmail(
    IOptions<PathSettings> pathSettings,
    ILogger<DeleteRawEmail> logger) : KernelProcessStep
{
    private readonly string _rawBasePath = PathUtils.ExpandHome(pathSettings.Value.NewEmailStorageDirectory);

    [KernelFunction("delete_email")]
    public void Invoke(KernelProcessStepContext context, OutlookMessage message)
    {
        try
        {
            var convoDir = Path.Combine(_rawBasePath, message.ConversationId ?? "unknown");
            var jsonPath = Path.Combine(convoDir, $"{message.MessageId}.json");
            var imagesDir = Path.Combine(convoDir, "cid");

            if (File.Exists(jsonPath))
            {
                try
                {
                    File.Delete(jsonPath);
                    logger.LogInformation("Deleted JSON file: {JsonPath}", jsonPath);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete JSON file: {JsonPath}", jsonPath);
                }
            }

            if (message.Images?.Count > 0 && Directory.Exists(imagesDir))
            {
                foreach (var img in message.Images)
                {
                    var imagePath = Path.Combine(imagesDir, $"{img.Cid}{img.FileExtension}");
                    if (File.Exists(imagePath))
                    {
                        try
                        {
                            File.Delete(imagePath);
                            logger.LogInformation("Deleted image file: {ImagePath}", imagePath);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to delete image file: {ImagePath}", imagePath);
                        }
                    }
                }

                try
                {
                    Directory.Delete(imagesDir);
                    logger.LogInformation("Deleted images directory: {ImagesDir}", imagesDir);
                }
                catch (IOException)
                {
                    logger.LogDebug("Images directory not empty: {ImagesDir}", imagesDir);
                }
                catch (UnauthorizedAccessException)
                {
                    logger.LogWarning("Access denied while deleting images directory: {ImagesDir}", imagesDir);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Unexpected error deleting images directory: {ImagesDir}", imagesDir);
                }
            }

            try
            {
                Directory.Delete(convoDir);
                logger.LogInformation("Deleted conversation directory: {ConvoDir}", convoDir);
            }
            catch (IOException)
            {
                logger.LogDebug("Conversation directory not empty: {ConvoDir}", convoDir);
            }
            catch (UnauthorizedAccessException)
            {
                logger.LogWarning("Access denied while deleting conversation directory: {ConvoDir}", convoDir);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Unexpected error deleting conversation directory: {ConvoDir}", convoDir);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete raw email for message {MessageId}", message.MessageId);
        }
    }
}
