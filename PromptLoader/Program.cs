using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PromptLoader.Models;
using PromptLoader.Services;
using PromptLoader.Utils;

// --- New PromptContext API ---
var context = await PromptContext
    .FromFolder("./PromptSets")
    .WithConfig("appsettings.json")
    .LoadAsync();

var salesPromptSet = context.Get("Sales");
var refundPromptSet = context.Get("CustomerService/Refund");
var introPrompt = context.Get("Sales/intro.md");

// Example: Combine with base and get as string
var salesPromptContext = await context.Get("Sales").CombineWithBase().AsStringAsync();

// Example: Load a single prompt file
var singlePrompt = await PromptContext.FromFile("./Prompts/sample.prompt.yml").LoadAsync();

// Prepare chat history with a system prompt and user/assistant pairs  
var chatHistory = new ChatHistory();
chatHistory.AddSystemMessage(await context.Get("system.md").AsStringAsync());
chatHistory.AddSystemMessage(salesPromptContext);
chatHistory.AddUserMessage("I want to send a small payload into space and piggyback with other payloads. Which rocket companies can do this?");

// Set up the kernel and OpenAI chat completion service  
var builder = Kernel.CreateBuilder();
var config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).Build();
var apiKey = Environment.GetEnvironmentVariable("OpenAI:ApiKey") ?? config["OpenAI:ApiKey"];
if (string.IsNullOrEmpty(apiKey))
{
    throw new InvalidOperationException("OpenAI API key is not set. Please set it as an environment variable.");
}
builder.AddOpenAIChatCompletion(
  modelId: "gpt-4-1106-preview",
  apiKey: apiKey
);
var kernel = builder.Build();
var chatService = kernel.GetRequiredService<IChatCompletionService>();
var response = await chatService.GetChatMessageContentAsync(chatHistory);
Console.WriteLine(response.Content);
