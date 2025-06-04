using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OpenAI.Images;
using PromptLoader.Models;
using PromptLoader.Services;
using PromptLoader.Utils;

namespace PromptLoader.Fluent
{
    public class PromptContext : IPromptContext
    {
        private IConfiguration _config;
        private IPromptService _promptService;
        private Dictionary<string, Dictionary<string, PromptSet>> _promptSets = new();
        private Dictionary<string, Prompt> _prompts = new();
        private string? _currentSet;
        private string? _currentSubSet;
        private string? _currentPrompt;
        private string? _separator;
        private bool _cascadeOverride = true;
        private string? _folder;
        private string? _file;

        // Static factory methods
        public static PromptContext FromFile(string file = "", bool cascadeOverride = true)
        {
            var ctx = new PromptContext();
            ctx._file = file;
            ctx._cascadeOverride = cascadeOverride;
            return ctx;
        }

        public static PromptContext FromFolder(string folder = "", bool cascadeOverride = true)
        {
            var ctx = new PromptContext();
            ctx._folder = folder;
            ctx._cascadeOverride = cascadeOverride;
            return ctx;
        }

        public PromptContext WithConfig(IConfiguration config)
        {
            _config = config;
            _promptService = new PromptService(config); 
            return this;
        }

        public PromptContext WithConfig(string configPath)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile(configPath, optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            return WithConfig(config);
        }

        public async Task<PromptContext> LoadAsync()
        {
            if (_promptService == null)
                throw new InvalidOperationException("PromptService is not configured. Call WithConfig first.");

            if (!string.IsNullOrEmpty(_file))
            {
                var prompt = await _promptService.LoadPromptAsync(_file);
                if (prompt != null)
                {
                    _prompts[Path.GetFileNameWithoutExtension(_file)] = prompt;
                }
            }
            else if (!string.IsNullOrEmpty(_folder))
            {
                _promptSets = await _promptService.LoadPromptSetsAsync(_cascadeOverride, _folder);
            }
            else
            {
                // Default: load from config
                _promptSets = await _promptService.LoadPromptSetsAsync(_cascadeOverride);
            }
            return this;
        }

        /// <summary>
        /// Only support 2 folders depth:
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public PromptContext Get(string path)
        {
            // Path format: Set/SubSet/Prompt or Set/SubSet or Set or Prompt
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            _currentSet = null;
            _currentSubSet = null;
            _currentPrompt = null;

            if (parts.Length == 1)
            {
                var key = parts[0];
                if (_promptSets.ContainsKey(key))
                {
                    _currentSet = key;
                }
                else if (_prompts.ContainsKey(key))
                {
                    _currentPrompt = key;
                }
                else
                {
                    // Set the state anyway, AsString will handle missing
                    _currentSet = key;
                    _currentPrompt = key;
                }
            }
            else if (parts.Length == 2)
            {
                var set = parts[0];
                var subOrPrompt = parts[1];
                _currentSet = set;
                if (_promptSets.TryGetValue(set, out var subSets))
                {
                    // If subOrPrompt is a subset
                    if (subSets.ContainsKey(subOrPrompt))
                    {
                        _currentSubSet = subOrPrompt;
                    }
                    // If subOrPrompt is a prompt in the Root subset
                    else if (subSets.TryGetValue("Root", out var rootSubset) && rootSubset.Prompts.ContainsKey(subOrPrompt))
                    {
                        _currentPrompt = subOrPrompt;
                    }
                    else
                    {
                        // Set as prompt in Root subset anyway, AsString will handle missing
                        _currentPrompt = subOrPrompt;
                    }
                }
                else
                {
                    // Set as prompt in Root subset anyway, AsString will handle missing
                    _currentPrompt = subOrPrompt;
                }
            }
            else if (parts.Length == 3)
            {
                var set = parts[0];
                var subset = parts[1];
                var prompt = parts[2];
                _currentSet = set;
                _currentSubSet = subset;
                _currentPrompt = prompt;
            }
            else
            {
                throw new InvalidOperationException("Path must be in the format 'set', 'set/subset', or 'set/subset/prompt'.");
            }

            return this;
        }

        public PromptContext CombineWithBase()
        {
            // No-op for now, as base/root is handled in GetCombinedPrompts
            return this;
        }

        public PromptContext SeparateWith(string separator = "")
        {
            _separator = separator;
            return this;
        }

        public string AsString()
        {
            if (_promptService == null)
                throw new InvalidOperationException("PromptService is not configured. Call WithConfig first.");

            if (_prompts.Count > 0 && _currentPrompt != null && _prompts.TryGetValue(_currentPrompt, out var prompt))
            {
                return prompt.Text;
            }

            if (_promptSets.Count > 0 && _currentSet != null)
            {
                var setDict = _promptSets;
                if (setDict.TryGetValue(_currentSet, out var subSets))
                {
                    // If _currentPrompt is set and _currentSubSet is null, check Root subset for the prompt
                    if (_currentSubSet == null && _currentPrompt != null)
                    {
                        if (subSets.TryGetValue("Root", out var rootSubset) && rootSubset.Prompts.TryGetValue(_currentPrompt, out var rootPrompt))
                        {
                            return rootPrompt.Text;
                        }
                    }
                    // If both set and subset are specified
                    if (!string.IsNullOrEmpty(_currentSubSet))
                    {
                        // If the subset exists
                        if (subSets.TryGetValue(_currentSubSet, out var promptSet))
                        {
                            // If a specific prompt is requested and exists in the subset, return it
                            if (!string.IsNullOrEmpty(_currentPrompt) && promptSet.Prompts != null && promptSet.Prompts.TryGetValue(_currentPrompt, out var subPrompt))
                            {
                                return subPrompt.Text;
                            }
                            // If the subset name matches a prompt in the subset, return it
                            if (promptSet.Prompts != null && promptSet.Prompts.TryGetValue(_currentSubSet, out var subSetPrompt))
                            {
                                return subSetPrompt.Text;
                            }
                            // Otherwise, combine all prompts in the subset
                            PromptSet? rootSet = null;
                            if (setDict.TryGetValue("Root", out var rootDict) && rootDict.TryGetValue("Root", out var root))
                                rootSet = root;
                            return _promptService.GetCombinedPrompts(promptSet, rootSet, _separator);
                        }
                        // If the subset does not exist, but Root subset exists and has a prompt with the name of _currentSubSet
                        else if (subSets.TryGetValue("Root", out var rootPromptSet) && rootPromptSet.Prompts != null && rootPromptSet.Prompts.TryGetValue(_currentSubSet, out var rootPrompt2))
                        {
                            return rootPrompt2.Text;
                        }
                        // If _currentSubSet is set but not found as a prompt or subset, return empty string
                        else
                        {
                            return string.Empty;
                        }
                    }
                    // Fallback: combine all prompts in Root subset only if _currentSubSet is null or empty
                    if (string.IsNullOrEmpty(_currentSubSet) && subSets.TryGetValue("Root", out var fallbackRootPromptSet))
                    {
                        return _promptService.GetCombinedPrompts(fallbackRootPromptSet, null, _separator);
                    }
                }
            }
            return string.Empty;
        }
    }
} 