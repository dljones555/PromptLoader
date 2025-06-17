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
        private string? _promptName;
        private readonly Dictionary<string, string> _arguments = new(StringComparer.OrdinalIgnoreCase);
        private string? _language;
        private readonly List<Resource> _resources = new();

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
            _promptName = promptName;
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
        /// Runs the prompt and returns the result.
        /// </summary>
        /// <returns>A task that returns the composed prompt as a GetPromptResult.</returns>
        public async Task<GetPromptResult> RunAsync()
        {
            if (string.IsNullOrEmpty(_promptName))
                throw new InvalidOperationException("Prompt name is not specified. Call Prompt() method first.");

            // Create store if not provided
            if (_store == null)
            {
                if (_root == null)
                    throw new InvalidOperationException("No root or store specified. Call UseRoot() or UseStore() first.");

                _store = new PromptStore(_root);
                await _store.InitializeAsync();
            }

            // Get and compose the prompt
            var compose = await _store.GetAsync(_promptName);

            // Add arguments, language, and resources
            compose.WithArguments(_arguments);
            if (_language != null)
                compose.WithLanguage(_language);

            foreach (var resource in _resources)
            {
                compose.WithResource(resource.Uri, resource.Text ?? string.Empty, resource.MimeType);
            }

            // Compose and return the result
            return await compose.ComposeAsync();
        }
    }
}