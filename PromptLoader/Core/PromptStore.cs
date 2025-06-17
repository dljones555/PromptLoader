using PromptLoader.Models.MCP;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PromptLoader.Core
{
    /// <summary>
    /// Manages prompts from one or more roots.
    /// </summary>
    public class PromptStore
    {
        private readonly List<PromptRoot> _roots = new();
        private readonly Dictionary<string, PromptDefinition> _promptCache = new(StringComparer.OrdinalIgnoreCase);
        private bool _initialized = false;

        /// <summary>
        /// Creates a new instance of the PromptStore class.
        /// </summary>
        public PromptStore()
        {
        }

        /// <summary>
        /// Creates a new instance of the PromptStore class with the specified root.
        /// </summary>
        /// <param name="root">The root to use.</param>
        public PromptStore(PromptRoot root)
        {
            _roots.Add(root);
        }

        /// <summary>
        /// Creates a new instance of the PromptStore class with the specified roots.
        /// </summary>
        /// <param name="roots">The roots to use.</param>
        public PromptStore(IEnumerable<PromptRoot> roots)
        {
            _roots.AddRange(roots);
        }

        /// <summary>
        /// Adds a root to the store.
        /// </summary>
        /// <param name="root">The root to add.</param>
        public void AddRoot(PromptRoot root)
        {
            _roots.Add(root);
            _initialized = false;
        }

        /// <summary>
        /// Initializes the prompt store by loading prompts from all roots.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task InitializeAsync()
        {
            if (_initialized)
                return;

            _promptCache.Clear();
            foreach (var root in _roots)
            {
                var prompts = await root.LoadPromptsAsync();
                foreach (var prompt in prompts)
                {
                    _promptCache[prompt.Name] = prompt;
                }
            }

            _initialized = true;
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
        /// Gets a prompt by name.
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