using PromptLoader.Models.MCP;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly PromptRegistry? _registry;

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
        /// Creates a new instance of the PromptStore class with the specified registry.
        /// </summary>
        /// <param name="registry">The registry to use.</param>
        public PromptStore(PromptRegistry registry)
        {
            _registry = registry;
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

            if (_registry != null)
            {
                await _registry.InitializeAsync();
                _initialized = true;
                return;
            }

            _promptCache.Clear();
            foreach (var root in _roots)
            {
                var prompts = await root.LoadPromptsAsync();
                foreach (var definition in prompts)
                {
                    _promptCache[definition.Name] = definition;
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

            if (_registry != null)
            {
                var regDefinition = await _registry.GetPromptAsync(name);
                return new PromptCompose(regDefinition);
            }

            if (!_promptCache.TryGetValue(name, out var definition))
                throw new KeyNotFoundException($"Prompt '{name}' not found.");

            return new PromptCompose(definition);
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

            if (_registry != null)
            {
                var regDefinition = _registry.GetPromptAsync(name).GetAwaiter().GetResult();
                return new PromptCompose(regDefinition);
            }

            if (!_promptCache.TryGetValue(name, out var definition))
                throw new KeyNotFoundException($"Prompt '{name}' not found.");

            return new PromptCompose(definition);
        }

        /// <summary>
        /// Lists all prompts in the store.
        /// </summary>
        /// <returns>A collection of prompt definitions.</returns>
        public async Task<IEnumerable<PromptDefinition>> ListAsync()
        {
            if (!_initialized)
                await InitializeAsync();

            if (_registry != null)
            {
                return await _registry.ListPromptsAsync();
            }

            return _promptCache.Values;
        }

        /// <summary>
        /// Lists all prompts in the store.
        /// </summary>
        /// <returns>A collection of prompt definitions.</returns>
        public IEnumerable<PromptDefinition> ListPrompts()
        {
            if (!_initialized)
                InitializeAsync().GetAwaiter().GetResult();

            if (_registry != null)
            {
                return _registry.ListPromptsAsync().GetAwaiter().GetResult();
            }

            return _promptCache.Values;
        }

        /// <summary>
        /// Invalidates the cache for a specific prompt.
        /// </summary>
        /// <param name="name">The name of the prompt.</param>
        public void InvalidatePrompt(string name)
        {
            if (_registry != null)
            {
                _registry.InvalidatePrompt(name);
                return;
            }

            _promptCache.Remove(name);
        }

        /// <summary>
        /// Invalidates the entire cache.
        /// </summary>
        public void InvalidateCache()
        {
            if (_registry != null)
            {
                _registry.InvalidateCache();
                return;
            }

            _promptCache.Clear();
            _initialized = false;
        }
    }
}