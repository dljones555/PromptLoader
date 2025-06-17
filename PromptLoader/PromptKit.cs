using PromptLoader.Core;
using PromptLoader.Models.MCP;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PromptLoader
{
    /// <summary>
    /// Provides a fluent API for working with prompts.
    /// </summary>
    public class PromptKit
    {
        private PromptRoot? _root;
        private PromptStore? _store;
        private PromptRegistry? _registry;
        private string? _promptName;
        private string? _promptVersion;
        private readonly Dictionary<string, object> _arguments = new(StringComparer.OrdinalIgnoreCase);
        private string? _language;
        private readonly List<Resource> _resources = new();
        private readonly List<PromptMessage> _messages = new();
        private readonly PromptComposeOptions _options = new();
        private bool _preservePromptStructure = false;

        /// <summary>
        /// Creates a new instance of the PromptKit class.
        /// </summary>
        private PromptKit() { }

        /// <summary>
        /// Creates a PromptKit instance with a file-based root.
        /// </summary>
        /// <param name="path">The file path to use as the root.</param>
        /// <returns>A new PromptKit instance.</returns>
        public static PromptKit UseRoot(string path)
        {
            return new PromptKit
            {
                _root = PromptRoot.FromFile(path)
            };
        }

        /// <summary>
        /// Creates a PromptKit instance with a specific root.
        /// </summary>
        /// <param name="root">The root to use.</param>
        /// <returns>A new PromptKit instance.</returns>
        public static PromptKit UseRoot(PromptRoot root)
        {
            return new PromptKit
            {
                _root = root
            };
        }

        /// <summary>
        /// Creates a PromptKit instance with a specific store.
        /// </summary>
        /// <param name="store">The store to use.</param>
        /// <returns>A new PromptKit instance.</returns>
        public static PromptKit UseStore(PromptStore store)
        {
            return new PromptKit
            {
                _store = store
            };
        }

        /// <summary>
        /// Creates a PromptKit instance with a specific registry.
        /// </summary>
        /// <param name="registry">The registry to use.</param>
        /// <returns>A new PromptKit instance.</returns>
        public static PromptKit UseRegistry(PromptRegistry registry)
        {
            return new PromptKit
            {
                _registry = registry
            };
        }

        /// <summary>
        /// Creates a PromptKit instance with a Git-based root.
        /// </summary>
        /// <param name="repoUrl">The Git repository URL.</param>
        /// <returns>A new PromptKit instance.</returns>
        public static PromptKit UseGitRoot(string repoUrl)
        {
            return new PromptKit
            {
                _root = PromptRoot.FromGit(repoUrl)
            };
        }

        /// <summary>
        /// Creates a PromptKit instance with an HTTP-based root.
        /// </summary>
        /// <param name="url">The HTTP URL.</param>
        /// <param name="name">The name of the root.</param>
        /// <returns>A new PromptKit instance.</returns>
        public static PromptKit UseHttpRoot(string url, string name)
        {
            return new PromptKit
            {
                _root = PromptRoot.FromUri(url, name)
            };
        }

        /// <summary>
        /// Specifies the prompt to use.
        /// </summary>
        /// <param name="promptName">The name of the prompt.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptKit Prompt(string promptName)
        {
            // Check if this includes a version
            if (promptName.Contains('@'))
            {
                var parts = promptName.Split('@', 2);
                _promptName = parts[0];
                _promptVersion = parts[1];
            }
            else
            {
                _promptName = promptName;
            }

            return this;
        }

        /// <summary>
        /// Specifies the prompt version to use.
        /// </summary>
        /// <param name="version">The version of the prompt.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptKit Version(string version)
        {
            _promptVersion = version;
            return this;
        }

        /// <summary>
        /// Sets an argument value.
        /// </summary>
        /// <param name="name">The name of the argument.</param>
        /// <param name="value">The value of the argument.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptKit WithArgument(string name, string value)
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
        public PromptKit WithArgumentObject(string name, object value)
        {
            _arguments[name] = value;
            return this;
        }

        /// <summary>
        /// Sets an input value (alias for WithArgument).
        /// </summary>
        /// <param name="name">The name of the input.</param>
        /// <param name="value">The value of the input.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptKit WithInput(string name, string value)
        {
            return WithArgument(name, value);
        }

        /// <summary>
        /// Sets the language for the prompt.
        /// </summary>
        /// <param name="language">The language code (e.g., "en-US").</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptKit InLanguage(string language)
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
        public PromptKit WithResource(string uri, string text, string? mimeType = null)
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
        /// Adds a system message to the prompt.
        /// </summary>
        /// <param name="text">The text content of the message.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptKit WithSystemMessage(string text)
        {
            _messages.Add(PromptMessage.System(text));
            return this;
        }

        /// <summary>
        /// Adds a user message to the prompt.
        /// </summary>
        /// <param name="text">The text content of the message.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptKit WithUserMessage(string text)
        {
            _messages.Add(PromptMessage.User(text));
            return this;
        }

        /// <summary>
        /// Adds an assistant message to the prompt.
        /// </summary>
        /// <param name="text">The text content of the message.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptKit WithAssistantMessage(string text)
        {
            _messages.Add(PromptMessage.Assistant(text));
            return this;
        }

        /// <summary>
        /// Preserves the original prompt structure defined in YAML/JSON.
        /// </summary>
        /// <returns>This instance for method chaining.</returns>
        public PromptKit PreservePromptStructure()
        {
            _preservePromptStructure = true;
            return this;
        }

        /// <summary>
        /// Sets options for prompt composition.
        /// </summary>
        /// <param name="options">The options to set.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptKit WithOptions(PromptComposeOptions options)
        {
            _options.IncludeSystemPrompt = options.IncludeSystemPrompt;
            _options.IncludeArgumentDescriptions = options.IncludeArgumentDescriptions;
            _options.AutoFormatCodeBlocks = options.AutoFormatCodeBlocks;
            return this;
        }

        /// <summary>
        /// Sets whether to include a system prompt.
        /// </summary>
        /// <param name="include">Whether to include a system prompt.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptKit IncludeSystemPrompt(bool include)
        {
            _options.IncludeSystemPrompt = include;
            return this;
        }

        /// <summary>
        /// Runs the prompt and returns the result.
        /// </summary>
        /// <returns>A task that returns the composed prompt as a GetPromptResult.</returns>
        public async Task<GetPromptResult> RunAsync()
        {
            if (string.IsNullOrEmpty(_promptName))
                throw new InvalidOperationException("Prompt name is not specified. Call Prompt() method first.");

            PromptDefinition promptDef;

            // Try to get the prompt definition from registry, store, or create from root
            if (_registry != null)
            {
                string promptKey = _promptName;
                if (!string.IsNullOrEmpty(_promptVersion))
                {
                    promptKey = $"{_promptName}@{_promptVersion}";
                }

                promptDef = await _registry.GetPromptAsync(promptKey);
            }
            else if (_store != null)
            {
                await _store.InitializeAsync();
                var composer = await _store.GetAsync(_promptName);
                // This is a temporary workaround until we refactor PromptCompose to work directly with PromptDefinition
                return ComposePrompt(composer);
            }
            else if (_root != null)
            {
                _store = new PromptStore(_root);
                await _store.InitializeAsync();
                var composer = await _store.GetAsync(_promptName);
                // This is a temporary workaround until we refactor PromptCompose to work directly with PromptDefinition
                return ComposePrompt(composer);
            }
            else
            {
                throw new InvalidOperationException("No root, store, or registry specified. Call UseRoot(), UseStore(), or UseRegistry() first.");
            }

            // Create and configure a prompt composer
            var composerFromDef = new PromptCompose(promptDef)
                .WithOptions(_options);

            if (_preservePromptStructure)
            {
                composerFromDef.PreservePromptStructure();
            }

            // Add arguments, language, resources, and messages
            foreach (var arg in _arguments)
            {
                composerFromDef.WithArgumentObject(arg.Key, arg.Value);
            }

            if (_language != null)
            {
                composerFromDef.WithLanguage(_language);
            }

            foreach (var resource in _resources)
            {
                composerFromDef.WithResource(resource.Uri, resource.Text ?? string.Empty, resource.MimeType);
            }

            foreach (var message in _messages)
            {
                composerFromDef.WithMessage(message);
            }

            // Compose and return the result
            return await composerFromDef.ComposeAsync();
        }

        // Helper method for the temporary workaround
        private GetPromptResult ComposePrompt(PromptCompose composer)
        {
            // Apply options
            composer = composer.WithOptions(_options);

            if (_preservePromptStructure)
            {
                composer = composer.PreservePromptStructure();
            }

            // Add arguments, language, resources, and messages
            foreach (var arg in _arguments)
            {
                composer = composer.WithArgumentObject(arg.Key, arg.Value);
            }

            if (_language != null)
            {
                composer = composer.WithLanguage(_language);
            }

            foreach (var resource in _resources)
            {
                composer = composer.WithResource(resource.Uri, resource.Text ?? string.Empty, resource.MimeType);
            }

            foreach (var message in _messages)
            {
                composer = composer.WithMessage(message);
            }

            return composer.Compose();
        }

        /// <summary>
        /// Gets the available prompts.
        /// </summary>
        /// <param name="includeVersions">Whether to include all versions.</param>
        /// <param name="includeDrafts">Whether to include drafts.</param>
        /// <param name="category">Category filter.</param>
        /// <param name="tags">Tags filter.</param>
        /// <returns>A list of available prompts.</returns>
        public async Task<List<PromptDefinition>> GetAvailablePromptsAsync(
            bool includeVersions = false,
            bool includeDrafts = false,
            string? category = null,
            List<string>? tags = null)
        {
            if (_registry != null)
            {
                await _registry.InitializeAsync();
                return await _registry.ListPromptsAsync(includeVersions, includeDrafts, category, tags);
            }

            if (_store != null)
            {
                await _store.InitializeAsync();
                // Basic listing without versioning support
                return new List<PromptDefinition>(_store.ListPrompts());
            }

            if (_root != null)
            {
                return new List<PromptDefinition>(await _root.LoadPromptsAsync());
            }

            throw new InvalidOperationException("No root, store, or registry specified. Call UseRoot(), UseStore(), or UseRegistry() first.");
        }
    }
}