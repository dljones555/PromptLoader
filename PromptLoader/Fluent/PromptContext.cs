using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
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
        private bool _combineWithRoot = false;

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
                /*else
                {
                    _currentSet = "Root";
                    _currentPrompt = key;
                }*/
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

        public PromptContext CombineWithRoot()
        {
            _combineWithRoot = true;
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

            // 1) If there is a _currentPrompt and that's in _prompts, return prompt.Text
            if (!string.IsNullOrEmpty(_currentPrompt) && _prompts.TryGetValue(_currentPrompt, out var prompt))
            {
                return prompt.Text;
            }

            if (!string.IsNullOrWhiteSpace(_currentSet))
            {

                // 2) If flag is false and there is a _currentSet or _currentSubSet, this assumes _currentPrompt is null
                if (!_combineWithRoot)
                {
                    /*if (string.IsNullOrWhiteSpace(_currentSet))
                    {
                        throw new InvalidOperationException("No valid prompt or set found. Ensure you have called Get() with a valid path.");
                    }*/

                    var promptSet = _promptSets[_currentSet][_currentSubSet ?? "Root"];

                    if (!string.IsNullOrEmpty(_currentPrompt) && promptSet.Prompts.TryGetValue(_currentPrompt, out var singlePrompt))
                    {
                        return singlePrompt.Text;
                    }

                }
                else
                {
                    /*if (string.IsNullOrWhiteSpace(_currentSet))
                    {
                        throw new InvalidOperationException("No valid prompt or set found. Ensure you have called Get() with a valid path.");
                    }*/

                    var setKey = string.IsNullOrEmpty(_currentSet) ? "Root" : _currentSet;
                    if (!_promptSets.TryGetValue(setKey, out var subSets))
                        return string.Empty;
                    var subSetKey = string.IsNullOrEmpty(_currentSubSet) ? "Root" : _currentSubSet;
                    if (!subSets.TryGetValue(subSetKey, out var promptSet))
                        return string.Empty;
                    // Combine with root if present
                    PromptSet? rootSet = null;
                    if (_promptSets.TryGetValue("Root", out var rootDict) && rootDict.TryGetValue("Root", out var root))
                        rootSet = root;
                    return _promptService.GetCombinedPrompts(promptSet, rootSet, _separator);
                }
            }

            return string.Empty;
        }
    }
} 