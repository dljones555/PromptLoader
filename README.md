# Prompt Loader - it's like AI on Rails!

# Overview

6/24/2025 - MCP support in progress. Have checked out [https://modelcontextprotocol.io](https://modelcontextprotocol.io). This is being refactored to support Prompts and Roots.

MCP Server support for Prompts will not so much focused on Web API or method annotation, but root source loading (file://, https://, API, github and cloud prompt SaaS) named prompt support.

Have also done market research on other prompt management offerings out there.

- Prompt and eval tools exist, but don't allow pulling prompts into code for LLM chat completion and agent API code.
- Some of the prompt authoring tools are more consumer focused and fit the idea of allowing non-engineer roles.
- There is some vendor lockin potential with prompt management out there across bigtech, AI frontier companies, and startups.
- LangChain has a pretty complete soluion as does PromptLayer.
- This library aims to play well with all options in Python and C#.
- Searching github for functions that load prompts. A lot of inline prompts that need cleanup!

**Please consider supporting this project with a GitHub star or sponsorship contribution!**

LLM prompts are the standard operating procedures and knowledge assets of your Agents and AI based value creation!

These reflect your business rules and processes, and alignment with your organization's values.

***Tired of prompt spaghetti? Time to ditch inlne prompts!***

***Let product managers and business analysts share the work with engineers to define these and run evals!***

## Structured Prompt Management - external prompt management.  
- Separates prompt from code as management assets
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
    messages=[{"role": "system", "content": prompt.text},{"role": "user": "How do I return the shirt?"}]
)
```
