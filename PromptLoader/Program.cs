using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PromptLoader.Models;
using PromptLoader.Services;
using PromptLoader.Utils;

// Build configuration to read from appsettings.json  
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables() // This requires the correct namespace
    .Build();

// Set up DI
var services = new ServiceCollection();
services.AddSingleton<IPromptService, PromptService>();
services.AddSingleton<IConfiguration>(config);
var provider = services.BuildServiceProvider();

// Get singleton instance from DI
var promptService = provider.GetRequiredService<IPromptService>();

// Set up the kernel and OpenAI chat completion service  
var builder = Kernel.CreateBuilder();

var apiKey = Environment.GetEnvironmentVariable("OpenAI:ApiKey") ?? config["OpenAI:ApiKey"];

if (string.IsNullOrEmpty(apiKey))
{
    throw new InvalidOperationException("OpenAI API key is not set. Please set it as an environment variable.");
}

builder.AddOpenAIChatCompletion(
  modelId: "gpt-4-1106-preview", // or "gpt-4o" or the latest GPT-4.1 model name  
  apiKey: apiKey
);

var kernel = builder.Build();

var prompts = await promptService.LoadPromptsAsync();
var promptSets = await promptService.LoadPromptSetsAsync();

var refundPromptSet = promptSets["CustomerService"]["Refund"];
var salesPromptContext = promptService.GetCombinedPrompts(promptSets["Sales"].Root());
// This is the GitHub Models format.  
PromptYml textSummarizePrompt = prompts["sample.prompt"].ToPromptYml();

// Example: Load a single prompt file
var singlePromptPath = Path.Combine(
    PathUtils.ResolvePromptPath(config["PromptsFolder"] ?? "Prompts"),
    "sample.prompt.yml"
);
var singlePrompt = await promptService.LoadPromptAsync(singlePromptPath);

// Prepare chat history with a system prompt and user/assistant pairs  
var chatHistory = new ChatHistory();
chatHistory.AddSystemMessage(prompts["system"].Text);
chatHistory.AddSystemMessage(salesPromptContext);
chatHistory.AddUserMessage("I want to send a small payload into space and piggyback with other payloads. Which rocket companies can do this?");

// Get the chat completion service and send the chat history  
var chatService = kernel.GetRequiredService<IChatCompletionService>();
var response = await chatService.GetChatMessageContentAsync(chatHistory);

// Output the assistant's reply  
Console.WriteLine(response.Content);
