using Microsoft.Extensions.Configuration;
using PromptLoader.Models;
using PromptLoader.Utils;

namespace PromptLoader.Services
{
    public interface IPromptService
    {
        Dictionary<string, Prompt> Prompts { get; }
        Dictionary<string, Dictionary<string, PromptSet>> PromptSets { get; }
        Task<Dictionary<string, Prompt>> LoadPromptsAsync(bool cascadeOverride = true, string? promptsFolder = null);
        Task<Dictionary<string, Dictionary<string, PromptSet>>> LoadPromptSetsAsync(bool cascadeOverride = true, string? promptSetFolder = null);
        string GetCombinedPrompts(Dictionary<string, PromptSet> promptSets, string setName, string? separator = null);
        string GetCombinedPrompts(PromptSet promptSet, PromptSet? rootSet = null, string? separator = null);
        Task<Prompt?> LoadPromptAsync(string filePath);
    }

    /// <summary>
    /// Provides high-level operations for loading prompts and prompt sets using configuration.
    /// </summary>
    public class PromptService : IPromptService
    {
        private readonly IConfiguration _config;
        private string[] _supportedExtensions = Array.Empty<string>();
        private bool _extensionsLoaded = false;
        public Dictionary<string, Prompt> Prompts { get; private set; } = new();
        public Dictionary<string, Dictionary<string, PromptSet>> PromptSets { get; private set; } = new();

        public PromptService(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Loads all prompts from the configured prompts folder or a specified folder asynchronously.
        /// </summary>
        public async Task<Dictionary<string, Prompt>> LoadPromptsAsync(bool cascadeOverride = true, string? promptsFolder = null)
        {
            var folder = promptsFolder ?? PathUtils.ResolvePromptPath(_config["PromptsFolder"] ?? "Prompts");
            EnsureSupportedExtensionsLoaded();
            Prompts = await LoadPromptsInternalAsync(folder, cascadeOverride);
            return Prompts;
        }

        /// <summary>
        /// Loads all prompt sets from the configured prompt set folder or a specified folder asynchronously.
        /// </summary>
        public async Task<Dictionary<string, Dictionary<string, PromptSet>>> LoadPromptSetsAsync(bool cascadeOverride = true, string? promptSetFolder = null)
        {
            var folder = promptSetFolder ?? PathUtils.ResolvePromptPath(_config["PromptSetFolder"] ?? "PromptSets");
            EnsureSupportedExtensionsLoaded();
            PromptSets = await LoadPromptSetsInternalAsync(folder, cascadeOverride);
            return PromptSets;
        }

        /// <summary>
        /// Combines prompts in a set according to PromptList in config, with optional separator.
        /// </summary>
        /// <param name="promptSets">Dictionary of prompt sets (e.g., from LoadPromptSets).</param>
        /// <param name="setName">The name of the prompt set to combine.</param>
        /// <param name="separator">Optional separator string. If null, uses PromptSeparator from config or newline.</param>
        /// <returns>Concatenated prompt text for the set, using the specified or configured separator.</returns>
        public string GetCombinedPrompts(Dictionary<string, PromptSet> promptSets, string setName, string? separator = null)
        {
            PromptSet? rootSet = null;
            if (promptSets.TryGetValue("Root", out var rootPromptSet))
                rootSet = rootPromptSet;

            if (!promptSets.TryGetValue(setName, out var promptSet))
                throw new KeyNotFoundException($"Prompt set '{setName}' not found.");
            return GetCombinedPrompts(promptSet, rootSet, separator);
        }

        /// <summary>
        /// Combines prompts in a PromptSet according to PromptListType, with optional separator.
        /// </summary>
        /// <param name="promptSet">The prompt set to combine.</param>
        /// <param name="rootSet">Optional root set for fallback prompt lookup.</param>
        /// <param name="separator">Optional separator string. If null, uses PromptSeparator from config or newline.</param>
        /// <returns>Concatenated prompt text for the set, using the specified or configured separator.</returns>
        public string GetCombinedPrompts(PromptSet promptSet, PromptSet? rootSet = null, string? separator = null)
        {
            var sepTemplate = separator ?? _config["PromptSeparator"] ?? Environment.NewLine;
            var builder = new System.Text.StringBuilder();
            var promptList = _config.GetSection("PromptList").Get<string[]>() ?? promptSet.Prompts.Keys.ToArray();
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

        // Helper to convert a string to PascalCase
        private static string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return char.ToUpperInvariant(input[0]) + input.Substring(1).ToLowerInvariant();
        }

        /// <summary>
        /// Loads a single prompt from a file asynchronously.
        /// </summary>
        public async Task<Prompt?> LoadPromptAsync(string filePath)
        {
            EnsureSupportedExtensionsLoaded();
            if (!File.Exists(filePath))
                return null;
            var ext = Path.GetExtension(filePath);
            if (!_supportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                return null;
            var content = await File.ReadAllTextAsync(filePath);
            var format = GetFormatFromExtension(ext);
            return new Prompt(content, format);
        }

        private void EnsureSupportedExtensionsLoaded()
        {
            if (!_extensionsLoaded)
            {
                var exts = _config.GetSection("SupportedPromptExtensions").Get<string[]>();
                if (exts != null && exts.Length > 0)
                {
                    _supportedExtensions = exts;
                }
                else
                {
                    _supportedExtensions = new[] { ".txt", ".prompt", ".yml", ".jinja", ".jinja2", ".prompt.md", ".md" };
                }
                _extensionsLoaded = true;
            }
        }

        private async Task<Dictionary<string, Prompt>> LoadPromptsInternalAsync(string folderPath, bool cascadeOverride = true)
        {
            if (!Directory.Exists(folderPath))
                return new Dictionary<string, Prompt>(StringComparer.OrdinalIgnoreCase);

            var promptFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(f => _supportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f.Count(c => c == Path.DirectorySeparatorChar));

            var prompts = new Dictionary<string, Prompt>(StringComparer.OrdinalIgnoreCase);

            // Constrain prompt list logic
            bool constrain = _config.GetValue("ConstrainPromptList", false);
            var allowedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (constrain)
            {
                var promptList = _config.GetSection("PromptList").Get<string[]>();
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

        private async Task<Dictionary<string, Dictionary<string, PromptSet>>> LoadPromptSetsInternalAsync(string rootFolder, bool cascadeOverride = true)
        {
            if (!Directory.Exists(rootFolder))
                return new Dictionary<string, Dictionary<string, PromptSet>>(StringComparer.OrdinalIgnoreCase);

            var result = new Dictionary<string, Dictionary<string, PromptSet>>(StringComparer.OrdinalIgnoreCase);

            // Constrain prompt list logic
            bool constrain = _config.GetValue("ConstrainPromptList", false);
            var allowedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (constrain)
            {
                var promptList = _config.GetSection("PromptList").Get<string[]>();
                if (promptList != null)
                    allowedNames.UnionWith(promptList);
            }

            // Add Root set for the rootFolder itself (e.g., /PromptSets)
            var rootLevelPrompts = new Dictionary<string, Prompt>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(rootFolder, "*.*", SearchOption.TopDirectoryOnly))
            {
                var ext = Path.GetExtension(file);
                if (!_supportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;
                var name = Path.GetFileNameWithoutExtension(file);
                if (constrain && allowedNames.Count > 0 && !allowedNames.Contains(name))
                    continue;
                var content = await File.ReadAllTextAsync(file);
                var format = GetFormatFromExtension(ext);
                rootLevelPrompts[name] = new Prompt(content, format);
            }
            if (rootLevelPrompts.Count > 0)
            {
                result["Root"] = new Dictionary<string, PromptSet>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Root", new PromptSet { Name = "Root", Prompts = rootLevelPrompts } }
                };
            }

            foreach (var topLevelDir in Directory.GetDirectories(rootFolder))
            {
                var topLevelName = Path.GetFileName(topLevelDir);
                var subSets = new Dictionary<string, PromptSet>(StringComparer.OrdinalIgnoreCase);

                // Only include files found directly in the top-level folder in the "Root" PromptSet
                var rootPrompts = new Dictionary<string, Prompt>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in Directory.GetFiles(topLevelDir, "*.*", SearchOption.TopDirectoryOnly))
                {
                    var ext = Path.GetExtension(file);
                    if (!_supportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (constrain && allowedNames.Count > 0 && !allowedNames.Contains(name))
                        continue;
                    var content = await File.ReadAllTextAsync(file);
                    var format = GetFormatFromExtension(ext);
                    rootPrompts[name] = new Prompt(content, format);
                }
                if (rootPrompts.Count > 0)
                {
                    subSets["Root"] = new PromptSet { Name = "Root", Prompts = rootPrompts };
                }

                // Subfolders as sub prompt sets, with prompt inheritance logic
                foreach (var subDir in Directory.GetDirectories(topLevelDir))
                {
                    var subName = Path.GetFileName(subDir);
                    var subPrompts = new Dictionary<string, Prompt>(StringComparer.OrdinalIgnoreCase);
                    // Load prompts from subfolder
                    foreach (var file in Directory.GetFiles(subDir, "*.*", SearchOption.TopDirectoryOnly))
                    {
                        var ext = Path.GetExtension(file);
                        if (!_supportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;
                        var name = Path.GetFileNameWithoutExtension(file);
                        if (constrain && allowedNames.Count > 0 && !allowedNames.Contains(name))
                            continue;
                        var content = await File.ReadAllTextAsync(file);
                        var format = GetFormatFromExtension(ext);
                        subPrompts[name] = new Prompt(content, format);
                    }
                    // Inherit from parent if cascadeOverride is true or false
                    foreach (var parentPrompt in rootPrompts)
                    {
                        if (!subPrompts.ContainsKey(parentPrompt.Key))
                        {
                            subPrompts[parentPrompt.Key] = parentPrompt.Value;
                        }
                        else if (!cascadeOverride)
                        {
                            // If cascadeOverride is false, use parent value if present
                            subPrompts[parentPrompt.Key] = parentPrompt.Value;
                        }
                        // If cascadeOverride is true, subfolder value already takes precedence
                    }
                    subSets[subName] = new PromptSet { Name = subName, Prompts = subPrompts };
                }
                result[topLevelName] = subSets;
            }
            return result;
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
    }
}
