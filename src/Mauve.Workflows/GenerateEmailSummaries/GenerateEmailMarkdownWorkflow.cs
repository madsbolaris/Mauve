// using Mauve.Workflows.GenerateEmailSummaries.Steps;
// using Microsoft.SemanticKernel;

// namespace Mauve.Workflows.GenerateEmailSummaries;

// public class GenerateEmailSummariesWorkflow()
// {
//     public KernelProcess Create()
//     {
//         var process = new ProcessBuilder("GenerateEmailSummaries");

//         var generateSummaries = process.AddStepFromType<HtmlToMarkdownConverter>();
//         var saveEmail = process.AddStepFromType<SaveMarkdownEmail>();
//         var deleteEmail = process.AddStepFromType<DeleteRawEmail>();

//         process
//             .OnInputEvent(GenerateEmailSummariesEvents.NewEmail)
//             .SendEventTo(new ProcessFunctionTargetBuilder(htmlToMarkdown));

//         htmlToMarkdown
//             .OnFunctionResult()
//             .SendEventTo(new ProcessFunctionTargetBuilder(saveEmail));

//         saveEmail
//             .OnEvent(GenerateEmailSummariesEvents.EmailSaved)
//             .SendEventTo(new ProcessFunctionTargetBuilder(deleteEmail));

//         return process.Build();
//     }
// }
