using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PromptLoader.Models;
using PromptLoader.Utils;
using PromptLoader.Fluent;

// Build configuration to read from appsettings.json  
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables() // This requires the correct namespace
    .Build();

// Set up DI
var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(config);
var provider = services.BuildServiceProvider();

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

// --- FLUENT API USAGE ---
// Load a prompt set and combine prompts using the fluent API
var salesPromptContext = await PromptContext
    .FromFolder()
    .WithConfig(config)
    .LoadAsync();

string salesCombined = salesPromptContext
    .Get("Sales")
    .CombineWithRoot()
    .AsString();

// Load a single prompt file using the fluent API
var systemPromptContext = await PromptContext
    .FromFile(Path.Combine(PathUtils.ResolvePromptPath(config["PromptsFolder"] ?? "Prompts"), "system.txt"))
    .WithConfig(config)
    .LoadAsync();
string systemPrompt = systemPromptContext.Get("system").AsString();

// Prepare chat history with a system prompt and user/assistant pairs  
var chatHistory = new ChatHistory();
chatHistory.AddSystemMessage(systemPrompt);
chatHistory.AddSystemMessage(salesCombined);
chatHistory.AddUserMessage("I want to send a small payload into space and piggyback with other payloads. Which rocket companies can do this?");

// MCP direction
//var roots = new List<Root>
//{
//    new Root { Uri = "file:///C:/Projects/PromptLoader/Prompts", Name = "Local Prompts" },
//    new Root { Uri = "https://api.example.com/prompts", Name = "Remote Prompts" }
//};

// Define prompt sources
var roots = new List<IPromptSource>
{
    new FileSystemPromptSource(folder: "PromptSets/CustomerService"),
    new FileSystemPromptSource(folder: "PromptSets/Sales"),
    // In the future: new RemotePromptSource(config, uri: "https://api.example.com/prompts")
};

var promptContext = new PromptContext(roots);
var rootsPromptContext = await promptContext.LoadAsync();

// Get the chat completion service and send the chat history  
var chatService = kernel.GetRequiredService<IChatCompletionService>();
var response = await chatService.GetChatMessageContentAsync(chatHistory);

// Output the assistant's reply  
Console.WriteLine(response.Content);
