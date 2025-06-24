using PromptLoader.Models.MCP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PromptLoader.Core
{
    /// <summary>
    /// Composes prompts with arguments and resources.
    /// </summary>
    public class PromptCompose
    {
        private static readonly Regex ArgumentPlaceholderRegex = new(@"\{\{([^{}]+)\}\}", RegexOptions.Compiled);

        private readonly PromptDefinition _promptDefinition;
        private readonly Dictionary<string, string> _arguments = new(StringComparer.OrdinalIgnoreCase);
        private string? _language;
        private readonly List<Resource> _resources = new();

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
        /// Replaces argument placeholders in text content.
        /// </summary>
        /// <param name="text">The text with placeholders.</param>
        /// <returns>The text with placeholders replaced by argument values.</returns>
        private string ReplaceArguments(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return ArgumentPlaceholderRegex.Replace(text, match =>
            {
                var argName = match.Groups[1].Value.Trim();
                return _arguments.TryGetValue(argName, out var value) ? value : match.Value;
            });
        }

        /// <summary>
        /// Creates a copy of a message with argument placeholders replaced.
        /// </summary>
        /// <param name="message">The original message.</param>
        /// <returns>A new message with argument placeholders replaced.</returns>
        private PromptMessage ProcessMessage(PromptMessage message)
        {
            // Create a new message with the same role
            var processedMessage = new PromptMessage
            {
                Role = message.Role
            };

            // Process the content based on its type
            if (message.Content is TextContent textContent)
            {
                // Replace arguments in the text content
                processedMessage.Content = new TextContent
                {
                    Text = ReplaceArguments(textContent.Text)
                };
            }
            else if (message.Content is ResourceContent resourceContent)
            {
                // Create a copy of the resource content
                var processedResource = new Resource
                {
                    Uri = resourceContent.Resource.Uri,
                    MimeType = resourceContent.Resource.MimeType,
                    Text = resourceContent.Resource.Text
                };

                // Replace arguments in the resource text if available
                if (!string.IsNullOrEmpty(processedResource.Text))
                {
                    processedResource.Text = ReplaceArguments(processedResource.Text);
                }

                processedMessage.Content = new ResourceContent
                {
                    Resource = processedResource
                };
            }
            else
            {
                // For other content types, just use the original
                processedMessage.Content = message.Content;
            }

            return processedMessage;
        }

        /// <summary>
        /// Composes the prompt with the provided arguments and resources.
        /// </summary>
        /// <returns>The composed prompt as a GetPromptResult.</returns>
        public GetPromptResult Compose()
        {
            ValidateRequiredArguments();

            var result = new GetPromptResult
            {
                Description = _promptDefinition.Description
            };

            // Process messages from the prompt definition (replacing argument placeholders)
            foreach (var message in _promptDefinition.Messages)
            {
                result.Messages.Add(ProcessMessage(message));
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
        /// Composes the prompt with the provided arguments and resources asynchronously.
        /// </summary>
        /// <returns>A task that returns the composed prompt as a GetPromptResult.</returns>
        public Task<GetPromptResult> ComposeAsync()
        {
            return Task.FromResult(Compose());
        }
    }
}