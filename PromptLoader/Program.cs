using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PromptLoader.Adapters;
using PromptLoader.Core;
using PromptLoader.Fluent;
using PromptLoader.Models.MCP;
using PromptLoader.Utils;

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

Console.WriteLine("=== LEGACY API USAGE ===");

// --- LEGACY FLUENT API USAGE ---
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

// Get the chat completion service and send the chat history  
var chatService = kernel.GetRequiredService<IChatCompletionService>();
var response = await chatService.GetChatMessageContentAsync(chatHistory);

// Output the assistant's reply  
Console.WriteLine("Legacy API Response:");
Console.WriteLine(response.Content);
Console.WriteLine();

Console.WriteLine("=== NEW MCP-COMPATIBLE API USAGE ===");

// --- NEW MCP-COMPATIBLE API USAGE ---

// Example 1: PromptRoot, PromptStore, and PromptCompose
Console.WriteLine("Example 1: Using PromptRoot, PromptStore, and PromptCompose");

// Create a root from a local directory
var root = PromptRoot.FromFile("./Prompts");
var store = new PromptStore(root);

// Initialize the store to load prompts
await store.InitializeAsync();

try
{
    // Get and compose a prompt
    var prompt = await store.GetAsync("summarize-error");
    var result = prompt
        .WithArgument("log", "Error: Connection timeout in network.py:127")
        .WithLanguage("en-US")
        .Compose();

    // Example of how to use the result with Semantic Kernel
    var messageHistory = new ChatHistory();
    foreach (var message in result.Messages)
    {
        if (message.Content is PromptLoader.Models.MCP.TextContent textContent)
        {
            switch (message.Role)
            {
                case "system":
                    messageHistory.AddSystemMessage(textContent.Text);
                    break;
                case "user":
                    messageHistory.AddUserMessage(textContent.Text);
                    break;
                case "assistant":
                    messageHistory.AddAssistantMessage(textContent.Text);
                    break;
            }
        }
    }

    // Note: This would actually call the LLM if a real prompt exists
    Console.WriteLine("Example 1 result structure:");
    Console.WriteLine($"- Description: {result.Description}");
    Console.WriteLine($"- Message count: {result.Messages.Count}");
    Console.WriteLine();
}
catch (KeyNotFoundException)
{
    Console.WriteLine("Note: 'summarize-error' prompt not found in the Prompts directory.");
    Console.WriteLine("This is expected in this example since we're demonstrating the API structure.");
    Console.WriteLine();
}

// Example 2: PromptKit fluent API
Console.WriteLine("Example 2: Using PromptKit fluent API");

try
{
    var result = await PromptLoader.PromptKit
        .UseRoot("./Prompts")
        .Prompt("summarize-error")
        .WithInput("log", "Error: Connection timeout in network.py:127")
        .InLanguage("en-US")
        .RunAsync();

    Console.WriteLine("Example 2 result structure:");
    Console.WriteLine($"- Description: {result.Description}");
    Console.WriteLine($"- Message count: {result.Messages.Count}");
    Console.WriteLine();
}
catch (KeyNotFoundException)
{
    Console.WriteLine("Note: 'summarize-error' prompt not found in the Prompts directory.");
    Console.WriteLine("This is expected in this example since we're demonstrating the API structure.");
    Console.WriteLine();
}

// Example 3: Using the adapter to bridge legacy code
Console.WriteLine("Example 3: Using the adapter to bridge legacy code");

var adapter = await PromptContextAdapter.FromFolderAsync("./PromptSets", config);
var legacyResult = await adapter.ComposePromptAsync("Sales");

Console.WriteLine("Example 3 result structure:");
Console.WriteLine($"- Description: {legacyResult.Description}");
Console.WriteLine($"- Message count: {legacyResult.Messages.Count}");
Console.WriteLine();

Console.WriteLine("=== END OF EXAMPLES ===");
