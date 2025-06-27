using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PromptLoader.Models;
using PromptLoader.Utils;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace PromptLoader.Fluent
{
    public class PromptContext : IPromptContext
    {
        private readonly PromptLoaderOptions _options;
        private IConfiguration? _config;
        public PromptListType PromptListType { get; private set; }
        private readonly IEnumerable<IPromptSource>? _promptSources;

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

        // Constructor 1: With IOptions
        public PromptContext(IOptions<PromptLoaderOptions> options)
        {
            _options = options.Value;
            _config = null;
            _promptSources = null;
            LoadOptions();
        }

        // Constructor 2: With IConfiguration
        public PromptContext(IConfiguration config)
        {
            _config = config;
            _options = config.GetSection("PromptLoader").Get<PromptLoaderOptions>() ?? new PromptLoaderOptions();
            _promptSources = null;
            LoadOptions();
        }

        // Constructor 3: With PromptLoaderOptions
        public PromptContext(PromptLoaderOptions options)
        {
            _options = options;
            _config = null;
            _promptSources = null;
            LoadOptions();
        }

        // Constructor 4: No DI, No config, No options, defaults
        public PromptContext()
        {
            _options = new PromptLoaderOptions();
            _config = null;
            _promptSources = null;
            LoadOptions();
        }

        // Constructor 5: Accepts multiple prompt sources (roots)
        public PromptContext(IEnumerable<IPromptSource> promptSources)
        {
            _promptSources = promptSources;
            _options = new PromptLoaderOptions();
            _config = null;
            LoadOptions();
        }

        private void LoadOptions()
        {
            PromptListType parsedType;
            if (_config != null && !Enum.TryParse(_config["PromptListType"], true, out parsedType))
            {
                parsedType = PromptListType.Named;
            }
            else if (_options != null && !string.IsNullOrEmpty(_options.PromptListType) && Enum.TryParse(_options.PromptListType, true, out parsedType))
            {
                // use parsedType
            }
            else
            {
                parsedType = PromptListType.Named;
            }
            PromptListType = parsedType;
        }

        // Static factory methods for fluent API
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
            var options = config.GetSection("PromptLoader").Get<PromptLoaderOptions>() ?? new PromptLoaderOptions();
            _options.PromptsFolder = options.PromptsFolder;
            _options.PromptSetFolder = options.PromptSetFolder;
            _options.PromptListType = options.PromptListType;
            _options.PromptList = options.PromptList;
            _options.ConstrainPromptList = options.ConstrainPromptList;
            _options.SupportedPromptExtensions = options.SupportedPromptExtensions;
            _options.PromptSeparator = options.PromptSeparator;
            _options.CascadeOverride = options.CascadeOverride;
            LoadOptions();
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
            if (_promptSources != null)
            {
                _prompts.Clear();
                _promptSets.Clear();
                foreach (var source in _promptSources)
                {
                    var prompts = await source.LoadPromptsAsync();
                    foreach (var kvp in prompts)
                        _prompts[kvp.Key] = kvp.Value;

                    var promptSets = await source.LoadPromptSetsAsync();
                    foreach (var setKvp in promptSets)
                        _promptSets[setKvp.Key] = setKvp.Value;
                }
                return this;
            }

            if (_config == null && _options == null)
                throw new InvalidOperationException("Configuration or options are not set. Call WithConfig or use a constructor with options/config.");

            if (!string.IsNullOrEmpty(_file))
            {
                var prompt = await LoadPromptAsync(_file);
                if (prompt != null)
                {
                    _prompts[Path.GetFileNameWithoutExtension(_file)] = prompt;
                }
            }
            else if (!string.IsNullOrEmpty(_folder))
            {
                _promptSets = await LoadPromptSetsAsync(_cascadeOverride, _folder);
            }
            else
            {
                _promptSets = await LoadPromptSetsAsync(_cascadeOverride);
            }
            return this;
        }

        public PromptContext Get(string path)
        {
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
            }
            else if (parts.Length == 2)
            {
                var set = parts[0];
                var subOrPrompt = parts[1];
                _currentSet = set;
                if (_promptSets.TryGetValue(set, out var subSets))
                {
                    if (subSets.ContainsKey(subOrPrompt))
                    {
                        _currentSubSet = subOrPrompt;
                    }
                    else if (subSets.TryGetValue("Root", out var rootSubset) && rootSubset.Prompts.ContainsKey(subOrPrompt))
                    {
                        _currentPrompt = subOrPrompt;
                    }
                    else
                    {
                        _currentPrompt = subOrPrompt;
                    }
                }
                else
                {
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
            if (_config == null && _options == null)
                throw new InvalidOperationException("Configuration or options are not set. Call WithConfig or use a constructor with options/config.");

            if (!string.IsNullOrEmpty(_currentPrompt) && _prompts.TryGetValue(_currentPrompt, out var prompt))
            {
                return prompt.Text;
            }

            if (!string.IsNullOrWhiteSpace(_currentSet))
            {
                if (!_combineWithRoot)
                {
                    var promptSet = _promptSets[_currentSet][_currentSubSet ?? "Root"];
                    if (!string.IsNullOrEmpty(_currentPrompt) && promptSet.Prompts.TryGetValue(_currentPrompt, out var singlePrompt))
                    {
                        return singlePrompt.Text;
                    }
                }
                else
                {
                    var setKey = string.IsNullOrEmpty(_currentSet) ? "Root" : _currentSet;
                    if (!_promptSets.TryGetValue(setKey, out var subSets))
                        return string.Empty;
                    var subSetKey = string.IsNullOrEmpty(_currentSubSet) ? "Root" : _currentSubSet;
                    if (!subSets.TryGetValue(subSetKey, out var promptSet))
                        return string.Empty;
                    PromptSet? rootSet = null;
                    if (_promptSets.TryGetValue("Root", out var rootDict) && rootDict.TryGetValue("Root", out var root))
                        rootSet = root;
                    return GetCombinedPrompts(promptSet, rootSet, _separator);
                }
            }

            return string.Empty;
        }

        // IPromptContext methods (moved from PromptService)
        public Dictionary<string, Prompt> Prompts => _prompts;
        public Dictionary<string, Dictionary<string, PromptSet>> PromptSets => _promptSets;

        public async Task<Dictionary<string, Prompt>> LoadPromptsAsync(bool cascadeOverride = true, string? promptsFolder = null)
        {
            var folder = promptsFolder
                ?? PathUtils.ResolvePromptPath(_options?.PromptsFolder
                ?? _config?["PromptsFolder"]
                ?? "Prompts");
            var supportedExtensions = PathUtils.GetSupportedPromptExtensions(_options, _config);
            _prompts = await LoadPromptsInternalAsync(folder, cascadeOverride, supportedExtensions);
            return _prompts;
        }

        public async Task<Dictionary<string, Dictionary<string, PromptSet>>> LoadPromptSetsAsync(bool cascadeOverride = true, string? promptSetFolder = null)
        {
            var folder = promptSetFolder
                ?? PathUtils.ResolvePromptPath(_options?.PromptSetFolder
                ?? _config?["PromptSetFolder"]
                ?? "PromptSets");
            var supportedExtensions = GetSupportedExtensions();
            _promptSets = await LoadPromptSetsInternalAsync(folder, cascadeOverride, supportedExtensions);
            return _promptSets;
        }

        private string[] GetSupportedExtensions()
        {
            return PathUtils.GetSupportedPromptExtensions(_options);
        }

        public string GetCombinedPrompts(Dictionary<string, PromptSet> promptSets, string setName, string? separator = null)
        {
            PromptSet? rootSet = null;
            if (promptSets.TryGetValue("Root", out var rootPromptSet))
                rootSet = rootPromptSet;

            if (!promptSets.TryGetValue(setName, out var promptSet))
                throw new KeyNotFoundException($"Prompt set '{setName}' not found.");
            return GetCombinedPrompts(promptSet, rootSet, separator);
        }

        public string GetCombinedPrompts(PromptSet promptSet, PromptSet? rootSet = null, string? separator = null)
        {
            var sepTemplate = separator
                ?? _options?.PromptSeparator
                ?? _config?["PromptSeparator"]
                ?? Environment.NewLine;
            var builder = new System.Text.StringBuilder();
            var promptList = _options?.PromptList
                ?? _config?.GetSection("PromptList").Get<string[]>()
                ?? promptSet.Prompts.Keys.ToArray();
            var allKeys = new List<string>();

            foreach (var key in promptList)
            {
                if (promptSet.Prompts.ContainsKey(key) || (rootSet != null && rootSet.Prompts.ContainsKey(key)))
                    allKeys.Add(key);
            }
            foreach (var kvp in promptSet.Prompts)
            {
                if (!promptList.Contains(kvp.Key))
                    allKeys.Add(kvp.Key);
            }
            for (int i = 0; i < allKeys.Count; i++)
            {
                var key = allKeys[i];
                Prompt? prompt = null;
                if (promptSet.Prompts.TryGetValue(key, out var p))
                {
                    prompt = p;
                }
                else if (rootSet != null && rootSet.Prompts.TryGetValue(key, out var rp))
                {
                    prompt = rp;
                }

                if (prompt == null)
                    continue;
                string pascal = ToPascalCase(key.Split('.')[0]);
                bool useFilenameAsHeader = sepTemplate.Contains("{filename}");
                string sep = useFilenameAsHeader ? sepTemplate.Replace("{filename}", pascal) : sepTemplate;
                if (i == 0 && useFilenameAsHeader)
                {
                    builder.Append(sep.TrimStart());

                    if (!sep.EndsWith("\n")) builder.AppendLine();
                    builder.Append(prompt.Text);
                }
                else if (i > 0 && useFilenameAsHeader)
                {
                    builder.Append(sep);
                    if (!sep.EndsWith("\n")) builder.AppendLine();
                    builder.Append(prompt.Text);
                }
                else
                {
                    if (i > 0) builder.Append(sep);
                    builder.Append(prompt.Text);
                }
            }
            return builder.ToString().TrimEnd();
        }

        public async Task<Prompt?> LoadPromptAsync(string filePath)
        {
            var supportedExtensions = GetSupportedExtensions();
            if (!File.Exists(filePath))
                return null;
            var ext = Path.GetExtension(filePath);
            if (!supportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                return null;
            var content = await File.ReadAllTextAsync(filePath);
            var format = GetFormatFromExtension(ext);
            return new Prompt(content, format);
        }

        // Helpers
        private static string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return char.ToUpperInvariant(input[0]) + input.Substring(1).ToLowerInvariant();
        }

        private static PromptFormat GetFormatFromExtension(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jinja" or ".jinja2" => PromptFormat.Jinja,
                ".yml" => PromptFormat.Yaml,
                ".prompt.md" or ".md" => PromptFormat.Markdown,
                ".txt" or ".prompt" => PromptFormat.Plain,
                _ => PromptFormat.Unknown
            };
        }

        private async Task<Dictionary<string, Prompt>> LoadPromptsInternalAsync(string folderPath, bool cascadeOverride, string[] supportedExtensions)
        {
            if (!Directory.Exists(folderPath))
                return new Dictionary<string, Prompt>(StringComparer.OrdinalIgnoreCase);

            var promptFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(f => supportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f.Count(c => c == Path.DirectorySeparatorChar));

            var prompts = new Dictionary<string, Prompt>(StringComparer.OrdinalIgnoreCase);

            bool constrain = _options?.ConstrainPromptList
                ?? _config?.GetValue("ConstrainPromptList", false)
                ?? false;
            var allowedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (constrain)
            {
                var promptList = _options?.PromptList
                    ?? _config?.GetSection("PromptList").Get<string[]>();
                if (promptList != null)
                    allowedNames.UnionWith(promptList);
            }

            foreach (var file in promptFiles)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (constrain && allowedNames.Count > 0 && !allowedNames.Contains(name))
                    continue;
                var content = await File.ReadAllTextAsync(file);
                var format = GetFormatFromExtension(Path.GetExtension(file));
                var prompt = new Prompt(content, format);
                if (cascadeOverride && prompts.ContainsKey(name))
                {
                    prompts[name] = prompt;
                }
                else if (!prompts.ContainsKey(name))
                {
                    prompts.Add(name, prompt);
                }
            }

            return prompts;
        }

        private async Task<Dictionary<string, Dictionary<string, PromptSet>>> LoadPromptSetsInternalAsync(string rootFolder, bool cascadeOverride, string[] supportedExtensions)
        {
            if (!Directory.Exists(rootFolder))
                return new Dictionary<string, Dictionary<string, PromptSet>>(StringComparer.OrdinalIgnoreCase);

            var result = new Dictionary<string, Dictionary<string, PromptSet>>(StringComparer.OrdinalIgnoreCase);
            var allowedNames = GetAllowedPromptNames();

            // Process root level prompts
            var rootLevelPrompts = await LoadPromptsFromDirectoryAsync(rootFolder, supportedExtensions, allowedNames);
            if (rootLevelPrompts.Count > 0)
            {
                result["Root"] = new Dictionary<string, PromptSet>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Root", new PromptSet { Name = "Root", Prompts = rootLevelPrompts } }
                };
            }

            // Process subdirectories (top level)
            foreach (var topLevelDir in Directory.GetDirectories(rootFolder))
            {
                var topLevelName = Path.GetFileName(topLevelDir);
                var subSets = new Dictionary<string, PromptSet>(StringComparer.OrdinalIgnoreCase);

                // Load prompts from the top level directory
                var rootPrompts = await LoadPromptsFromDirectoryAsync(topLevelDir, supportedExtensions, allowedNames);
                if (rootPrompts.Count > 0)
                {
                    subSets["Root"] = new PromptSet { Name = "Root", Prompts = rootPrompts };
                }

                // Process subdirectories of the top level directory
                await ProcessSubdirectoriesAsync(topLevelDir, rootPrompts, subSets, supportedExtensions, allowedNames, cascadeOverride);

                if (subSets.Count > 0)
                {
                    result[topLevelName] = subSets;
                }
            }

            return result;
        }

        private HashSet<string> GetAllowedPromptNames()
        {
            var allowedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool constrain = _options?.ConstrainPromptList
                ?? _config?.GetValue("ConstrainPromptList", false)
                ?? false;

            if (constrain)
            {
                var promptList = _options?.PromptList
                    ?? _config?.GetSection("PromptList").Get<string[]>();
                if (promptList != null)
                    allowedNames.UnionWith(promptList);
            }

            return allowedNames;
        }

        private async Task<Dictionary<string, Prompt>> LoadPromptsFromDirectoryAsync(
            string directory,
            string[] supportedExtensions,
            HashSet<string> allowedNames)
        {
            var prompts = new Dictionary<string, Prompt>(StringComparer.OrdinalIgnoreCase);

            var files = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => supportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));

            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (allowedNames.Count > 0 && !allowedNames.Contains(name))
                    continue;

                var ext = Path.GetExtension(file);
                var content = await File.ReadAllTextAsync(file);
                var format = GetFormatFromExtension(ext);
                prompts[name] = new Prompt(content, format);
            }

            return prompts;
        }

        private async Task ProcessSubdirectoriesAsync(
            string parentDir,
            Dictionary<string, Prompt> parentPrompts,
            Dictionary<string, PromptSet> subSets,
            string[] supportedExtensions,
            HashSet<string> allowedNames,
            bool cascadeOverride)
        {
            foreach (var subDir in Directory.GetDirectories(parentDir))
            {
                var subName = Path.GetFileName(subDir);
                var subPrompts = await LoadPromptsFromDirectoryAsync(subDir, supportedExtensions, allowedNames);

                // Apply inheritance from parent prompts
                foreach (var parentPrompt in parentPrompts)
                {
                    if (!subPrompts.ContainsKey(parentPrompt.Key))
                    {
                        subPrompts[parentPrompt.Key] = parentPrompt.Value;
                    }
                    else if (!cascadeOverride)
                    {
                        subPrompts[parentPrompt.Key] = parentPrompt.Value;
                    }
                }

                subSets[subName] = new PromptSet { Name = subName, Prompts = subPrompts };
            }
        }
    }
}