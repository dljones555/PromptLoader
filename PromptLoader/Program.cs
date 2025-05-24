using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json; // Add this namespace for AddJsonFile extension method  
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PromptLoader.Models;
using PromptLoader.Services;

// Build configuration to read from appsettings.json  
var config = new ConfigurationBuilder()
  .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
  .Build();

var apiKey = config["OpenAI:ApiKey"];

// Set up the kernel and OpenAI chat completion service  
var builder = Kernel.CreateBuilder();

builder.AddOpenAIChatCompletion(
  modelId: "gpt-4-1106-preview", // or "gpt-4o" or the latest GPT-4.1 model name  
  apiKey: apiKey
);

var kernel = builder.Build();

var promptsFolder = Path.Combine(Directory.GetCurrentDirectory(), "prompts");
var prompts = PromptLoader.Services.PromptLoader.LoadPrompts(promptsFolder, true);

// This is the GitHub Models format.  
// TODO: give them feedback to add a name or id for each prompt  
//       ask if have an API or prompt loader or have a deployment plan  
//       with GitHub actions  
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
