using Microsoft.Extensions.Caching.Memory;
using PromptLoader.Models.MCP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PromptLoader.Core
{
    /// <summary>
    /// Registry for managing and caching prompts.
    /// </summary>
    public class PromptRegistry
    {
        private readonly MemoryCache _cache;
        private readonly MemoryCacheEntryOptions _cacheOptions;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);
        private readonly List<PromptRoot> _roots = new();
        private readonly Dictionary<string, List<PromptDefinition>> _versionedPrompts = new();
        private bool _initialized = false;

        /// <summary>
        /// Creates a new instance of the PromptRegistry class.
        /// </summary>
        public PromptRegistry()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
            _cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(30))
                .SetSize(1);
        }

        /// <summary>
        /// Adds a root to the registry.
        /// </summary>
        /// <param name="root">The root to add.</param>
        public void AddRoot(PromptRoot root)
        {
            _roots.Add(root);
            _initialized = false;
        }

        /// <summary>
        /// Initializes the registry by loading prompts from all roots.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task InitializeAsync()
        {
            if (_initialized)
                return;

            await _cacheLock.WaitAsync();
            try
            {
                if (_initialized)
                    return;

                _cache.Clear();
                _versionedPrompts.Clear();

                foreach (var root in _roots)
                {
                    var prompts = await root.LoadPromptsAsync();
                    foreach (var prompt in prompts)
                    {
                        var key = prompt.Name.ToLowerInvariant();
                        
                        // Cache the prompt
                        _cache.Set(key, prompt, _cacheOptions);
                        
                        // Store versioned prompts
                        if (!string.IsNullOrEmpty(prompt.Version))
                        {
                            var versionedKey = $"{key}@{prompt.Version}";
                            _cache.Set(versionedKey, prompt, _cacheOptions);
                        }
                        
                        // Add to versioned collection
                        if (!_versionedPrompts.TryGetValue(key, out var versions))
                        {
                            versions = new List<PromptDefinition>();
                            _versionedPrompts[key] = versions;
                        }
                        
                        versions.Add(prompt);
                    }
                }

                _initialized = true;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <summary>
        /// Gets a prompt by name.
        /// </summary>
        /// <param name="name">The name of the prompt.</param>
        /// <returns>The prompt definition.</returns>
        public async Task<PromptDefinition> GetPromptAsync(string name)
        {
            if (!_initialized)
                await InitializeAsync();

            var key = name.ToLowerInvariant();
            
            // Check if this is a versioned request
            string promptKey = key;
            string? version = null;
            
            if (key.Contains('@'))
            {
                var parts = key.Split('@', 2);
                promptKey = parts[0];
                version = parts[1];
            }

            // Try to get from cache first
            if (_cache.TryGetValue(key, out PromptDefinition? cachedPrompt) && cachedPrompt != null)
            {
                return cachedPrompt;
            }

            // If specific version requested
            if (version != null && _versionedPrompts.TryGetValue(promptKey, out var versions))
            {
                var versionedPrompt = versions.FirstOrDefault(p => p.Version == version);
                if (versionedPrompt != null)
                {
                    return versionedPrompt;
                }
                throw new KeyNotFoundException($"Prompt '{promptKey}' with version '{version}' not found.");
            }

            // Look up in the versioned prompts for the latest
            if (_versionedPrompts.TryGetValue(promptKey, out var allVersions) && allVersions.Count > 0)
            {
                // Sort by version and get the latest (if versions are valid semver)
                try
                {
                    var latest = allVersions
                        .Where(p => !string.IsNullOrEmpty(p.Version) && !p.IsDraft)
                        .OrderByDescending(p => p.Version)
                        .FirstOrDefault();
                    
                    if (latest != null)
                    {
                        return latest;
                    }
                }
                catch
                {
                    // If version sorting fails, return the first non-draft one
                    var first = allVersions.FirstOrDefault(p => !p.IsDraft);
                    if (first != null)
                    {
                        return first;
                    }
                }
            }

            throw new KeyNotFoundException($"Prompt '{name}' not found.");
        }

        /// <summary>
        /// Lists all prompts in the registry.
        /// </summary>
        /// <param name="includeVersions">Whether to include all versions or just the latest.</param>
        /// <param name="includeDrafts">Whether to include drafts.</param>
        /// <param name="category">Filter by category.</param>
        /// <param name="tags">Filter by tags.</param>
        /// <returns>A list of prompt definitions.</returns>
        public async Task<List<PromptDefinition>> ListPromptsAsync(
            bool includeVersions = false, 
            bool includeDrafts = false,
            string? category = null,
            List<string>? tags = null)
        {
            if (!_initialized)
                await InitializeAsync();

            var result = new List<PromptDefinition>();

            foreach (var entry in _versionedPrompts)
            {
                var prompts = entry.Value;
                
                if (includeVersions)
                {
                    // Add all versions that match filters
                    foreach (var prompt in prompts)
                    {
                        if (!includeDrafts && prompt.IsDraft)
                            continue;
                            
                        if (category != null && prompt.Category != category)
                            continue;
                            
                        if (tags != null && tags.Count > 0)
                        {
                            if (prompt.Tags == null || !tags.Any(t => prompt.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)))
                                continue;
                        }
                        
                        result.Add(prompt);
                    }
                }
                else
                {
                    // Just add the latest version that matches filters
                    var filteredPrompts = prompts
                        .Where(p => includeDrafts || !p.IsDraft)
                        .Where(p => category == null || p.Category == category)
                        .Where(p => tags == null || tags.Count == 0 || 
                               (p.Tags != null && tags.Any(t => p.Tags.Contains(t, StringComparer.OrdinalIgnoreCase))))
                        .ToList();
                        
                    if (filteredPrompts.Count > 0)
                    {
                        try
                        {
                            // Try to sort by version and get latest
                            var latest = filteredPrompts
                                .Where(p => !string.IsNullOrEmpty(p.Version))
                                .OrderByDescending(p => p.Version)
                                .FirstOrDefault();
                                
                            if (latest != null)
                            {
                                result.Add(latest);
                                continue;
                            }
                        }
                        catch
                        {
                            // If version sorting fails, just add the first one
                        }
                        
                        // Fallback to first prompt in the filtered list
                        result.Add(filteredPrompts[0]);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Invalidates the cache for a specific prompt.
        /// </summary>
        /// <param name="name">The name of the prompt.</param>
        public void InvalidatePrompt(string name)
        {
            var key = name.ToLowerInvariant();
            _cache.Remove(key);
            
            // Also remove any versioned entries
            if (_versionedPrompts.TryGetValue(key, out var versions))
            {
                foreach (var prompt in versions)
                {
                    if (!string.IsNullOrEmpty(prompt.Version))
                    {
                        var versionedKey = $"{key}@{prompt.Version}";
                        _cache.Remove(versionedKey);
                    }
                }
                
                _versionedPrompts.Remove(key);
            }
        }

        /// <summary>
        /// Invalidates the entire cache.
        /// </summary>
        public void InvalidateCache()
        {
            _cache.Clear();
            _versionedPrompts.Clear();
            _initialized = false;
        }
    }
}