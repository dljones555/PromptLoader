using PromptLoader.Models.MCP;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace PromptLoader.Core
{
    /// <summary>
    /// Manages prompts from one or more roots.
    /// </summary>
    public class PromptStore
    {
        private readonly List<IRoot> _roots = new();
        private readonly Dictionary<string, PromptDefinition> _promptCache = new(StringComparer.OrdinalIgnoreCase);
        private bool _initialized = false;

        /// <summary>
        /// Creates a new instance of the PromptStore class with no roots.
        /// </summary>
        public PromptStore() { }

        /// <summary>
        /// Creates a new instance of the PromptStore class with the specified root.
        /// </summary>
        /// <param name="root">The root to use.</param>
        public PromptStore(IRoot root)
        {
            if (root != null)
            {
                _roots.Add(root);
            }
        }

        /// <summary>
        /// Creates a new instance of the PromptStore class with the specified roots.
        /// </summary>
        /// <param name="roots">The roots to use.</param>
        public PromptStore(IEnumerable<IRoot> roots)
        {
            if (roots != null)
            {
                _roots.AddRange(roots);
            }
        }

        /// <summary>
        /// Creates a new instance of the PromptStore class from a JSON string containing root definitions.
        /// </summary>
        /// <param name="jsonRootList">A JSON string containing an array of root definitions.</param>
        public PromptStore(string jsonRootList)
        {
            if (!string.IsNullOrWhiteSpace(jsonRootList))
            {
                try
                {
                    // Deserialize as a list of root objects
                    var roots = JsonSerializer.Deserialize<List<JsonElement>>(jsonRootList);
                    if (roots != null)
                    {
                        foreach (var rootElement in roots)
                        {
                            if (rootElement.TryGetProperty("uri", out var uriElement) &&
                                rootElement.TryGetProperty("name", out var nameElement))
                            {
                                var uri = uriElement.GetString();
                                var name = nameElement.GetString();

                                if (!string.IsNullOrEmpty(uri) && !string.IsNullOrEmpty(name))
                                {
                                    if (uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var path = uri.Substring(7);
                                        _roots.Add(new FileRoot(path, name));
                                    }
                                    else if (uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                             uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                    {
                                        _roots.Add(new HttpRoot(uri, name));
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error parsing JSON root list: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Creates a new instance of the PromptStore class with a single file path as the root.
        /// </summary>
        /// <param name="path">The file path to use as the root.</param>
        /// <returns>A new PromptStore instance.</returns>
        public static PromptStore FromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new PromptStore();
            }

            return new PromptStore(new FileRoot(path));
        }

        /// <summary>
        /// Adds a root to the store.
        /// </summary>
        /// <param name="root">The root to add.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptStore AddRoot(IRoot root)
        {
            if (root != null)
            {
                _roots.Add(root);
                _initialized = false;
            }
            return this;
        }

        /// <summary>
        /// Adds a root from a file path.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="name">Optional name for the root. If not provided, the directory name will be used.</param>
        /// <returns>This instance for method chaining.</returns>
        public PromptStore AddPath(string path, string? name = null)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                _roots.Add(new FileRoot(path, name));
                _initialized = false;
            }
            return this;
        }

        /// <summary>
        /// Initializes the prompt store by loading prompts from all roots.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task<PromptStore> InitializeAsync()
        {
            if (_initialized)
                return this;

            _promptCache.Clear();

            foreach (var root in _roots)
            {
                try
                {

                    var prompts = await root.LoadAsync();
                    foreach (var prompt in prompts)
                    {
                        _promptCache[prompt.Name] = prompt;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error loading prompts from root '{root.Name}': {ex.Message}");
                }
            }

            _initialized = true;
            return this;
        }

        /// <summary>
        /// Gets a prompt by name.
        /// </summary>
        /// <param name="name">The name of the prompt.</param>
        /// <returns>A PromptCompose instance for the prompt.</returns>
        public async Task<PromptCompose> GetAsync(string name)
        {
            if (!_initialized)
                await InitializeAsync();

            if (!_promptCache.TryGetValue(name, out var prompt))
                throw new KeyNotFoundException($"Prompt '{name}' not found.");

            return new PromptCompose(prompt);
        }

        /// <summary>
        /// Gets a prompt by name synchronously.
        /// </summary>
        /// <param name="name">The name of the prompt.</param>
        /// <returns>A PromptCompose instance for the prompt.</returns>
        public PromptCompose Get(string name)
        {
            if (!_initialized)
                InitializeAsync().GetAwaiter().GetResult();

            if (!_promptCache.TryGetValue(name, out var prompt))
                throw new KeyNotFoundException($"Prompt '{name}' not found.");

            return new PromptCompose(prompt);
        }

        /// <summary>
        /// Lists all prompts in the store.
        /// </summary>
        /// <returns>A collection of prompt definitions.</returns>
        public async Task<IEnumerable<PromptDefinition>> ListAsync()
        {
            if (!_initialized)
                await InitializeAsync();

            return _promptCache.Values;
        }
    }
}