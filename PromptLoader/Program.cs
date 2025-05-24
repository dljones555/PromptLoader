using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PromptLoader.Models;
using PromptLoader.Services;
using PromptLoader.Utils;
using Loader = PromptLoader.Services.PromptLoader; // Using the alias for clarity. 

// Build configuration to read from appsettings.json  
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables() // This requires the correct namespace
    .Build();

// Set supported prompt extensions from config
Loader.SetSupportedExtensionsFromConfig(config);

// Set up the kernel and OpenAI chat completion service  
var builder = Kernel.CreateBuilder();

var apiKey = Environment.GetEnvironmentVariable("OpenAI:ApiKey") ?? config["OpenAI:ApiKey"];

if (string.IsNullOrEmpty(apiKey))
{
    throw new InvalidOperationException("OpenAI API key is not set. Please set it in appsettings.json or as an environment variable.");
}

builder.AddOpenAIChatCompletion(
  modelId: "gpt-4-1106-preview", // or "gpt-4o" or the latest GPT-4.1 model name  
  apiKey: apiKey
);

var kernel = builder.Build();

// Read prompt folders from appsettings.json and resolve them
var promptsFolder = PathUtils.ResolvePromptPath(config["PromptsFolder"] ?? "Prompts");
var promptSetFolder = PathUtils.ResolvePromptPath(config["PromptSetFolder"] ?? "PromptSets");

// TODO: Make these Singletons in the DI container.
//  
// var promptSets = promptSetLoader.LoadPromptSets(promptSetFolder, true);
// var prompts = promptLoader.LoadPrompts(promptsFolder, true);

var prompts = Loader.LoadPrompts(promptsFolder, true);   
var promptSets = PromptSetLoader.LoadPromptSets(promptSetFolder, true);

var refundPromptSet = promptSets["CustomerService"]["Refund"];
var salesPromptContext = PromptSetLoader.JoinPrompts(promptSets["Sales"], "Main", config);   

// This is the GitHub Models format.  
PromptYml textSummarizePrompt = prompts["sample.prompt"].ToPromptYml();

// Prepare chat history with a system prompt and user/assistant pairs  
var chatHistory = new ChatHistory();
chatHistory.AddSystemMessage(prompts["system"].Text);
chatHistory.AddUserMessage(salesPromptContext);
chatHistory.AddAssistantMessage("Understood. I will sale aligned to those guidelines.");
chatHistory.AddUserMessage("I want to send a small payload into space and piggyback with other payloads. Which rocket companies can do this?");

// Get the chat completion service and send the chat history  
var chatService = kernel.GetRequiredService<IChatCompletionService>();
var response = await chatService.GetChatMessageContentAsync(chatHistory);

// Output the assistant's reply  
Console.WriteLine(response.Content);
