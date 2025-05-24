using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json; // Add this namespace for AddJsonFile extension method  
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PromptLoader.Models;
using PromptLoader.Services;

// Helper to resolve prompt folder path for dev, test, and published scenarios
static string ResolvePromptPath(string relativePath)
{
    // Try relative to executable (published scenario)
    var exeDir = AppContext.BaseDirectory;
    var exePath = Path.GetFullPath(Path.Combine(exeDir, relativePath));
    if (Directory.Exists(exePath)) return exePath;

    // Try walking up to solution root (dev/test scenario)
    var dir = exeDir;
    for (int i = 0; i < 5; i++) // go up max 5 levels
    {
        var candidate = Path.GetFullPath(Path.Combine(dir, relativePath));
        if (Directory.Exists(candidate)) return candidate;
        dir = Path.GetFullPath(Path.Combine(dir, ".."));
    }
    // Fallback to original
    return exePath;
}

// Build configuration to read from appsettings.json  
var config = new ConfigurationBuilder()
  .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
  .Build();

// Set supported prompt extensions from config
PromptLoader.Services.PromptLoader.SetSupportedExtensionsFromConfig(config);

var apiKey = config["OpenAI:ApiKey"];
// Set up the kernel and OpenAI chat completion service  
var builder = Kernel.CreateBuilder();

builder.AddOpenAIChatCompletion(
  modelId: "gpt-4-1106-preview", // or "gpt-4o" or the latest GPT-4.1 model name  
  apiKey: apiKey
);

var kernel = builder.Build();

// Read prompt folders from appsettings.json and resolve them
var promptsFolder = ResolvePromptPath(config["PromptsFolder"] ?? "Prompts");
var promptSetFolder = ResolvePromptPath(config["PromptSetFolder"] ?? "PromptSets");

var prompts = PromptLoader.Services.PromptLoader.LoadPrompts(promptsFolder, true);
var promptSets = PromptLoader.Services.PromptSetLoader.LoadPromptSets(promptSetFolder, true);

var refundPromptSet = promptSets["CustomerService"]["Refund"];
var salesPromptContextToPrependToUserChatHistory = PromptSetLoader.JoinPrompts(promptSets["Sales"], "Main", config);   

// This is the GitHub Models format.  

PromptYml textSummarizePrompt = prompts["sample.prompt"].ToPromptYml();

// Prepare chat history with a system prompt and user/assistant pairs  
var chatHistory = new ChatHistory();
chatHistory.AddSystemMessage(prompts["system"].Text);
chatHistory.AddUserMessage("Hello, who won the world cup in 2022?");
chatHistory.AddAssistantMessage("Argentina won the 2022 FIFA World Cup.");
chatHistory.AddUserMessage("Who was the captain?");

// Get the chat completion service and send the chat history  
var chatService = kernel.GetRequiredService<IChatCompletionService>();
var response = await chatService.GetChatMessageContentAsync(chatHistory);

// Output the assistant's reply  
Console.WriteLine(response.Content);
