using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PromptLoader.Adapters;
using PromptLoader.Core;
using PromptLoader.Fluent;
using PromptLoader.Models.MCP;
using PromptLoader.Utils;
using System.Text.Json;
using TextContent = PromptLoader.Models.MCP.TextContent;

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

// Create a sample YAML prompt file to demonstrate features
var promptsDir = Path.Combine(Directory.GetCurrentDirectory(), "Prompts");
Directory.CreateDirectory(promptsDir);

// Sample template-based prompt
var templatePrompt = @"
name: analyze-code
description: Analyze code for potential improvements
version: '1.0.0'
arguments:
  - name: language
    description: Programming language
    required: true
  - name: code
    description: Code to analyze
    required: true
template: |
  Please analyze the following {{ language }} code for potential improvements:
{{ code }}
  Consider:
  - Code style and formatting
  - Performance optimizations
  - Error handling
  - Best practices for {{ language }}
";

await File.WriteAllTextAsync(Path.Combine(promptsDir, "analyze-code.yml"), templatePrompt);

// Sample conversation with multiple messages
var conversationDir = Path.Combine(promptsDir, "debug-conversation");
var messagesDir = Path.Combine(conversationDir, "messages");
Directory.CreateDirectory(conversationDir);
Directory.CreateDirectory(messagesDir);

var conversationPrompt = @"
name: debug-conversation
description: Multi-step conversation for debugging
version: '1.0.0'
arguments:
  - name: error
    description: Error message to debug
    required: true
  - name: language
    description: Programming language
    required: true
";

await File.WriteAllTextAsync(Path.Combine(conversationDir, "prompt.yml"), conversationPrompt);
await File.WriteAllTextAsync(Path.Combine(messagesDir, "01-system.txt"), 
    "You are an expert {{ language }} developer helping debug code.");
await File.WriteAllTextAsync(Path.Combine(messagesDir, "02-user.txt"), 
    "I'm getting this error in my {{ language }} code: {{ error }}");
await File.WriteAllTextAsync(Path.Combine(messagesDir, "03-assistant.txt"), 
    "I'll help you debug this {{ language }} error. Let me analyze it step by step.");

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

//var chatService = kernel.GetRequiredService<IChatCompletionService>();
//var response = await chatService.GetChatMessageContentAsync(chatHistory);

// Output the assistant's reply  
// Console.WriteLine(response.Content);
Console.WriteLine();

Console.WriteLine("=== NEW MCP-COMPATIBLE API USAGE ===");

Console.WriteLine("Example 1: Template-based Prompts with Scriban");
Console.WriteLine("---------------------------------------------");

// Create a registry and initialize it
var registry = new PromptRegistry();
registry.AddRoot(PromptRoot.FromFile(promptsDir));
await registry.InitializeAsync();

// List available prompts
var availablePrompts = await registry.ListPromptsAsync();
Console.WriteLine($"Available prompts: {string.Join(", ", availablePrompts.Select(p => p.Name))}");
Console.WriteLine();

// Use a template-based prompt
try
{
    var analyzeCodePrompt = await PromptLoader.PromptKit
        .UseRegistry(registry)
        .Prompt("analyze-code")
        .WithArgument("language", "C#")
        .WithArgument("code", "public int Add(int a, int b) { return a + b; }")
        .RunAsync();

    Console.WriteLine("Analyze Code Prompt Result:");
    foreach (var message in analyzeCodePrompt.Messages)
    {
        Console.WriteLine($"[{message.Role}] {((TextContent)message.Content).Text}");
    }
    Console.WriteLine();
}
catch (KeyNotFoundException)
{
    Console.WriteLine("Note: 'analyze-code' prompt not found. This example requires the prompt file to be created first.");
    Console.WriteLine();
}

Console.WriteLine("Example 2: Multi-step Conversation Prompts");
Console.WriteLine("------------------------------------------");

try
{
    var debugConversation = await PromptLoader.PromptKit
        .UseRegistry(registry)
        .Prompt("debug-conversation")
        .WithArgument("error", "TypeError: Cannot read property 'length' of undefined")
        .WithArgument("language", "JavaScript")
        .PreservePromptStructure()
        .RunAsync();

    Console.WriteLine("Debug Conversation Result:");
    foreach (var message in debugConversation.Messages)
    {
        Console.WriteLine($"[{message.Role}] {((TextContent)message.Content).Text}");
    }
    Console.WriteLine();
}
catch (KeyNotFoundException)
{
    Console.WriteLine("Note: 'debug-conversation' prompt not found. This example requires the prompt file to be created first.");
    Console.WriteLine();
}

Console.WriteLine("Example 3: Version-specific Prompts");
Console.WriteLine("---------------------------------");

// Create another version of the analyze-code prompt
var templatePromptV2 = @"
name: analyze-code
description: Analyze code for potential improvements (V2)
version: '2.0.0'
arguments:
  - name: language
    description: Programming language
    required: true
  - name: code
    description: Code to analyze
    required: true
template: |
  As a senior {{ language }} developer, please review this code and suggest improvements:
{{ code }}
  Consider:
  - Code quality and readability
  - Potential bugs or edge cases
  - Performance considerations
  - Modern {{ language }} features that could be used
";

await File.WriteAllTextAsync(Path.Combine(promptsDir, "analyze-code-v2.yml"), templatePromptV2);

// Refresh the registry
registry.InvalidateCache();
await registry.InitializeAsync();

try
{
    // Use a specific version
    var analyzeCodeV1 = await PromptLoader.PromptKit
        .UseRegistry(registry)
        .Prompt("analyze-code@1.0.0")
        .WithArgument("language", "C#")
        .WithArgument("code", "public int Add(int a, int b) { return a + b; }")
        .RunAsync();

    Console.WriteLine("Analyze Code v1 Result:");
    foreach (var message in analyzeCodeV1.Messages)
    {
        Console.WriteLine($"[{message.Role}] {((TextContent)message.Content).Text}");
    }
    Console.WriteLine();

    // Use latest version (should be v2)
    var analyzeCodeLatest = await PromptLoader.PromptKit
        .UseRegistry(registry)
        .Prompt("analyze-code")  // No version specified, should get latest
        .WithArgument("language", "C#")
        .WithArgument("code", "public int Add(int a, int b) { return a + b; }")
        .RunAsync();

    Console.WriteLine("Analyze Code Latest (v2) Result:");
    foreach (var message in analyzeCodeLatest.Messages)
    {
        Console.WriteLine($"[{message.Role}] {((TextContent)message.Content).Text}");
    }
    Console.WriteLine();
}
catch (KeyNotFoundException)
{
    Console.WriteLine("Note: Versioned prompts not found. This example requires the prompt files to be created first.");
    Console.WriteLine();
}

Console.WriteLine("Example 4: Advanced Usage with Custom Messages");
Console.WriteLine("--------------------------------------------");

try
{
    var customResult = await PromptLoader.PromptKit
        .UseRegistry(registry)
        .Prompt("analyze-code")
        .WithArgument("language", "Python")
        .WithArgument("code", "def factorial(n):\n    if n == 0:\n        return 1\n    return n * factorial(n-1)")
        .WithSystemMessage("You are a Python expert specializing in optimization.")
        .WithUserMessage("I need help optimizing this recursive function for very large inputs.")
        .RunAsync();

    Console.WriteLine("Custom Messages Result:");
    foreach (var message in customResult.Messages)
    {
        Console.WriteLine($"[{message.Role}] {((TextContent)message.Content).Text}");
    }
    Console.WriteLine();
}
catch (KeyNotFoundException)
{
    Console.WriteLine("Note: 'analyze-code' prompt not found. This example requires the prompt file to be created first.");
    Console.WriteLine();
}

Console.WriteLine("Example 5: Using the adapter to bridge legacy code");
Console.WriteLine("------------------------------------------------");

var adapter = await PromptContextAdapter.FromFolderAsync("./PromptSets", config);
var legacyResult = await adapter.ComposePromptAsync("Sales");

Console.WriteLine("Legacy Adapter Result:");
Console.WriteLine($"- Description: {legacyResult.Description}");
Console.WriteLine($"- Message count: {legacyResult.Messages.Count}");
if (legacyResult.Messages.Count > 0)
{
    Console.WriteLine($"- First message: {((TextContent)legacyResult.Messages[0].Content).Text.Substring(0, Math.Min(100, ((TextContent)legacyResult.Messages[0].Content).Text.Length))}...");
}
Console.WriteLine();

Console.WriteLine("=== END OF EXAMPLES ===");

Console.ReadLine();