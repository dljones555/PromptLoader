# Prompt Loader - it's like AI on Rails!

# Overview

LLM prompts are the standard operating procedures and knowledge assets of your Agents and AI based value creation!

These reflect your business rules and processes, and alignment with your organization's values.

***Tired of prompt spaghetti? Time to ditch inlne prompts!***

***Let product managers and business analysts share the work with engineers to define these and run evals!***

## Structured Prompt Management - external prompt management.  
- Separates prompt from code as management assets.
- Provide convention based approach to AI projects of any type. Comparable to MVC frameworks like RoR
- Segment prompts by functional or business area
- Support prompt versioning and source control

```
Project/
├── PromptSets/
│   ├── CustomerService/
│   │   ├── system.yml
│   │   ├── refund-policy.md
│   │   └── examples.txt
│   └── Sales/
│       ├── system.yml
│       └── product-specs.md
└── Tests/
    └── prompt-tests.yml
```

## Multi-Team Collaboration
- Different teams and roles (not just engineers) can manage their own prompt sets
- Supports testing of evals across teams

## Compliance & Governance
- Centralized prompt management
- Audit trail for prompt changes
- Validation rules for content safety

## Examples
```
C#

// Load a single prompt file
var prompt = promptService.LoadPrompt("Prompts/system.yml");
Console.WriteLine(prompt.Text);

// Load a prompt set
var customerService = promptService.LoadPromptSet("PromptSets/CustomerService");
var refundPrompt = customerService["Refund"].GetCombinedPrompt();

var chatHistory = new ChatHistory();
chatHistory.AddSystemMessage(refundPrompt);
chatHistory.AddUserMessage("I bought the shirt 20 days ago. It doesn't fit. What is the refund policy?");

var chatService = kernel.GetRequiredService<IChatCompletionService>();
var response = await chatService.GetChatMessageContentAsync(chatHistory);
```

```
Python

# Load a single prompt file
prompt = PromptLoader.load_prompt("prompts/system.yml")
print(prompt.text)

# Load a prompt set
customer_service = PromptSet.load("promptsets/customer_service")
refund_prompt = customer_service["refund"].get_combined_prompt()

# LangChain
from promptloader import PromptSet
prompt_set = PromptSet.load("customer_service")
chain = prompt_set.to_langchain() | ChatOpenAI()

# OpenAI
from openai import OpenAI
prompt = PromptLoader.load_prompt("system.yml")
response = client.chat.completions.create(
    messages=[{"role": "system", "content": prompt.text}]
)
```
