using PromptLoader.Models.MCP;
using Scriban;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PromptLoader.Core
{
    /// <summary>
    /// Composes prompts with arguments and resources using advanced templating.
    /// </summary>
    public class PromptCompose
    {
        private readonly PromptDefinition _promptDefinition;
        private readonly Dictionary<string, object> _arguments = new(StringComparer.OrdinalIgnoreCase);
        private string? _language;
        private readonly List<Resource> _resources = new();
        private readonly List<PromptMessage> _customMessages = new();
        private bool _preservePromptStructure = false;
        private PromptComposeOptions _options = new();

        /// <summary>
        /// Creates a new instance of the PromptCompose class.
        /// </summary>
        /// <param name="promptDefinition">The prompt definition to compose.</param>
        internal PromptCompose(PromptDefinition promptDefinition)
        {
            _promptDefinition = promptDefinition;
        }

        /// <summary>
        /// Sets an argument value.
        /// </summary>
        /// <param name="name">The name of the argument.</param>
        /// <param name="value">The value of the argument.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptCompose WithArgument(string name, string value)
        {
            _arguments[name] = value;
            return this;
        }

        /// <summary>
        /// Sets an argument value with any object type.
        /// </summary>
        /// <param name="name">The name of the argument.</param>
        /// <param name="value">The value of the argument (can be complex objects).</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptCompose WithArgumentObject(string name, object value)
        {
            _arguments[name] = value;
            return this;
        }

        /// <summary>
        /// Sets multiple argument values.
        /// </summary>
        /// <param name="arguments">The arguments to set.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptCompose WithArguments(Dictionary<string, string> arguments)
        {
            foreach (var arg in arguments)
            {
                _arguments[arg.Key] = arg.Value;
            }
            return this;
        }

        /// <summary>
        /// Sets the language for the prompt.
        /// </summary>
        /// <param name="language">The language code (e.g., "en-US").</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptCompose WithLanguage(string language)
        {
            _language = language;
            _arguments["language"] = language;
            return this;
        }

        /// <summary>
        /// Adds a resource to the prompt.
        /// </summary>
        /// <param name="uri">The URI of the resource.</param>
        /// <param name="text">The text content of the resource.</param>
        /// <param name="mimeType">The MIME type of the resource.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptCompose WithResource(string uri, string text, string? mimeType = null)
        {
            _resources.Add(new Resource
            {
                Uri = uri,
                Text = text,
                MimeType = mimeType
            });
            return this;
        }

        /// <summary>
        /// Adds an input as a resource to the prompt.
        /// </summary>
        /// <param name="name">The name of the input.</param>
        /// <param name="content">The content of the input.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptCompose WithInput(string name, string content)
        {
            _arguments[name] = content;
            return this;
        }

        /// <summary>
        /// Adds a custom message to the prompt.
        /// </summary>
        /// <param name="message">The message to add.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptCompose WithMessage(PromptMessage message)
        {
            _customMessages.Add(message);
            return this;
        }

        /// <summary>
        /// Adds a system message to the prompt.
        /// </summary>
        /// <param name="text">The text content of the message.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptCompose WithSystemMessage(string text)
        {
            _customMessages.Add(PromptMessage.System(text));
            return this;
        }

        /// <summary>
        /// Adds a user message to the prompt.
        /// </summary>
        /// <param name="text">The text content of the message.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptCompose WithUserMessage(string text)
        {
            _customMessages.Add(PromptMessage.User(text));
            return this;
        }

        /// <summary>
        /// Adds an assistant message to the prompt.
        /// </summary>
        /// <param name="text">The text content of the message.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptCompose WithAssistantMessage(string text)
        {
            _customMessages.Add(PromptMessage.Assistant(text));
            return this;
        }

        /// <summary>
        /// Sets options for prompt composition.
        /// </summary>
        /// <param name="options">The options to set.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptCompose WithOptions(PromptComposeOptions options)
        {
            _options = options;
            return this;
        }

        /// <summary>
        /// Preserves the original prompt structure defined in YAML/JSON.
        /// </summary>
        /// <returns>This instance for method chaining.</returns>
        public PromptCompose PreservePromptStructure()
        {
            _preservePromptStructure = true;
            return this;
        }

        /// <summary>
        /// Validates that all required arguments are provided.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a required argument is missing.</exception>
        private void ValidateRequiredArguments()
        {
            if (_promptDefinition.Arguments == null)
                return;

            var missingArgs = _promptDefinition.Arguments
                .Where(arg => arg.Required && !_arguments.ContainsKey(arg.Name))
                .Select(arg => arg.Name)
                .ToList();

            if (missingArgs.Any())
            {
                throw new ArgumentException(
                    $"Missing required arguments for prompt '{_promptDefinition.Name}': {string.Join(", ", missingArgs)}");
            }
        }

        /// <summary>
        /// Renders a template string with the provided arguments.
        /// </summary>
        /// <param name="templateText">The template text to render.</param>
        /// <returns>The rendered text.</returns>
        private string RenderTemplate(string templateText)
        {
            try
            {
                var template = Template.Parse(templateText);
                return template.Render(_arguments);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error rendering template: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Composes the prompt with the provided arguments and resources.
        /// </summary>
        /// <returns>A task that returns the composed prompt as a GetPromptResult.</returns>
        public GetPromptResult Compose()
        {
            ValidateRequiredArguments();

            var result = new GetPromptResult
            {
                Description = _promptDefinition.Description
            };

            // If custom messages are provided and we're not preserving structure, use them directly
            if (_customMessages.Count > 0 && !_preservePromptStructure)
            {
                result.Messages.AddRange(ProcessMessages(_customMessages));
                return result;
            }

            // If template is provided in the prompt definition, use it
            if (_promptDefinition.Template != null)
            {
                var renderedTemplate = RenderTemplate(_promptDefinition.Template);
                result.Messages.Add(PromptMessage.User(renderedTemplate));
                return result;
            }

            // If messages are provided in the prompt definition, use them
            if (_promptDefinition.Messages != null && _promptDefinition.Messages.Count > 0 && _preservePromptStructure)
            {
                result.Messages.AddRange(ProcessMessages(_promptDefinition.Messages));
                return result;
            }

            // Fall back to standard format
            // Add a system message with the prompt name
            if (_options.IncludeSystemPrompt)
            {
                result.Messages.Add(PromptMessage.System($"Using prompt: {_promptDefinition.Name}"));
            }

            // Add user message with arguments
            if (_arguments.Any())
            {
                var sb = new StringBuilder();

                if (!string.IsNullOrEmpty(_promptDefinition.Description))
                {
                    sb.AppendLine(_promptDefinition.Description);
                    sb.AppendLine();
                }

                // Format arguments in a readable way
                foreach (var arg in _arguments)
                {
                    var value = arg.Value?.ToString() ?? string.Empty;

                    // Special formatting for longer content
                    if (value.Length > 100)
                    {
                        sb.AppendLine($"{arg.Key}:");
                        sb.AppendLine("```");
                        sb.AppendLine(value);
                        sb.AppendLine("```");
                    }
                    else
                    {
                        sb.AppendLine($"{arg.Key}: {value}");
                    }
                }

                result.Messages.Add(PromptMessage.User(sb.ToString()));
            }

            // Add resource messages
            foreach (var resource in _resources)
            {
                result.Messages.Add(new PromptMessage
                {
                    Role = "user",
                    Content = new ResourceContent
                    {
                        Resource = resource
                    }
                });
            }

            return result;
        }

        /// <summary>
        /// Processes a list of messages by rendering any templates in them.
        /// </summary>
        /// <param name="messages">The messages to process.</param>
        /// <returns>The processed messages.</returns>
        private List<PromptMessage> ProcessMessages(List<PromptMessage> messages)
        {
            var result = new List<PromptMessage>();

            foreach (var message in messages)
            {
                if (message.Content is TextContent textContent)
                {
                    var renderedText = RenderTemplate(textContent.Text);
                    var processedMessage = new PromptMessage
                    {
                        Role = message.Role,
                        Content = new TextContent { Text = renderedText }
                    };
                    result.Add(processedMessage);
                }
                else
                {
                    // Non-text content is passed through unchanged
                    result.Add(message);
                }
            }

            return result;
        }

        /// <summary>
        /// Composes the prompt with the provided arguments and resources asynchronously.
        /// </summary>
        /// <returns>A task that returns the composed prompt as a GetPromptResult.</returns>
        public Task<GetPromptResult> ComposeAsync()
        {
            return Task.FromResult(Compose());
        }
    }

    /// <summary>
    /// Options for prompt composition.
    /// </summary>
    public class PromptComposeOptions
    {
        /// <summary>
        /// Gets or sets whether to include a system prompt.
        /// </summary>
        public bool IncludeSystemPrompt { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to include argument descriptions.
        /// </summary>
        public bool IncludeArgumentDescriptions { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to auto-format code blocks.
        /// </summary>
        public bool AutoFormatCodeBlocks { get; set; } = true;
    }
}