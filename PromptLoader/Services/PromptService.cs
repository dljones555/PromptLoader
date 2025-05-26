using Microsoft.Extensions.Configuration;
using PromptLoader.Models;
using PromptLoader.Utils;
using System.Collections.Generic;

namespace PromptLoader.Services
{
    public enum PromptOrderType
    {
        Named,
        Numeric,
        None
    }

    public interface IPromptService
    {
        Dictionary<string, Prompt> Prompts { get; }
        Dictionary<string, Dictionary<string, PromptSet>> PromptSets { get; }
        PromptOrderType PromptOrderType { get; } // Now enum
        Dictionary<string, Prompt> LoadPrompts(bool cascadeOverride = true);
        Dictionary<string, Dictionary<string, PromptSet>> LoadPromptSets(bool cascadeOverride = true);
        string JoinPrompts(Dictionary<string, PromptSet> promptSets, string setName);
        string JoinPrompts(PromptSet promptSet);
    }

    /// <summary>
    /// Provides high-level operations for loading prompts and prompt sets using configuration.
    /// </summary>
    public class PromptService : IPromptService
    {
        private readonly IConfiguration _config;
        private string[] _supportedExtensions = System.Array.Empty<string>();
        private bool _extensionsLoaded = false;
        public Dictionary<string, Prompt> Prompts { get; private set; } = new();
        public Dictionary<string, Dictionary<string, PromptSet>> PromptSets { get; private set; } = new();
        public PromptOrderType PromptOrderType { get; private set; } = PromptOrderType.Named;

        public PromptService(IConfiguration config)
        {
            _config = config;
            if (!Enum.TryParse(config["PromptOrderType"], true, out PromptOrderType parsedType))
                parsedType = PromptOrderType.Named;
            PromptOrderType = parsedType;
            if (config.GetValue<bool>("AutoLoadPrompts"))
            {
                LoadPrompts();
                LoadPromptSets();
            }
        }

        /// <summary>
        /// Loads all prompts from the configured prompts folder.
        /// </summary>
        public Dictionary<string, Prompt> LoadPrompts(bool cascadeOverride = true)
        {
            var promptsFolder = PathUtils.ResolvePromptPath(_config["PromptsFolder"] ?? "Prompts");
            EnsureSupportedExtensionsLoaded();
            Prompts = LoadPromptsInternal(promptsFolder, cascadeOverride);
            return Prompts;
        }

        /// <summary>
        /// Loads all prompt sets from the configured prompt set folder.
        /// </summary>
        public Dictionary<string, Dictionary<string, PromptSet>> LoadPromptSets(bool cascadeOverride = true)
        {
            var promptSetFolder = PathUtils.ResolvePromptPath(_config["PromptSetFolder"] ?? "PromptSets");
            EnsureSupportedExtensionsLoaded();
            PromptSets = LoadPromptSetsInternal(promptSetFolder, cascadeOverride);
            return PromptSets;
        }

        /// <summary>
        /// Joins prompts in a set according to PromptOrder in config.
        /// </summary>
        public string JoinPrompts(Dictionary<string, PromptSet> promptSets, string setName)
        {
            if (!promptSets.TryGetValue(setName, out var promptSet))
                throw new KeyNotFoundException($"Prompt set '{setName}' not found.");
            return JoinPrompts(promptSet);
        }

        /// <summary>
        /// Joins prompts in a PromptSet according to PromptOrderType.
        /// </summary>
        public string JoinPrompts(PromptSet promptSet)
        {
            switch (PromptOrderType)
            {
                case PromptOrderType.Named:
                    var promptOrder = _config.GetSection("PromptOrder").Get<string[]>();
                    if (promptOrder != null && promptOrder.Length > 0)
                    {
                        var ordered = new List<string>();
                        foreach (var key in promptOrder)
                        {
                            if (promptSet.Prompts.TryGetValue(key, out var prompt))
                                ordered.Add(prompt.Text);
                        }
                        // Add any remaining prompts not in PromptOrder
                        foreach (var kvp in promptSet.Prompts)
                        {
                            if (!promptOrder.Contains(kvp.Key))
                                ordered.Add(kvp.Value.Text);
                        }
                        return string.Join(System.Environment.NewLine, ordered);
                    }
                    // Fallback: join all prompts in default order
                    return string.Join(System.Environment.NewLine, promptSet.Prompts.Values.Select(x => x.Text));
                case PromptOrderType.Numeric:
                    var numericOrdered = promptSet.Prompts
                        .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(kvp => kvp.Value.Text);
                    return string.Join(System.Environment.NewLine, numericOrdered);
                case PromptOrderType.None:
                default:
                    return string.Join(System.Environment.NewLine, promptSet.Prompts.Values.Select(x => x.Text));
            }
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

        private Dictionary<string, Prompt> LoadPromptsInternal(string folderPath, bool cascadeOverride = true)
        {
            var promptFiles = System.IO.Directory.GetFiles(folderPath, "*.*", System.IO.SearchOption.AllDirectories)
                .Where(f => _supportedExtensions.Contains(System.IO.Path.GetExtension(f), System.StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f.Count(c => c == System.IO.Path.DirectorySeparatorChar));

            var prompts = new Dictionary<string, Prompt>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var file in promptFiles)
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(file);
                var content = System.IO.File.ReadAllText(file);
                var format = GetFormatFromExtension(System.IO.Path.GetExtension(file));

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

        private Dictionary<string, Dictionary<string, PromptSet>> LoadPromptSetsInternal(string rootFolder, bool cascadeOverride = true)
        {
            var result = new Dictionary<string, Dictionary<string, PromptSet>>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var topLevelDir in System.IO.Directory.GetDirectories(rootFolder))
            {
                var topLevelName = System.IO.Path.GetFileName(topLevelDir);
                var subSets = new Dictionary<string, PromptSet>(System.StringComparer.OrdinalIgnoreCase);

                // Only include files found directly in the top-level folder in the "Root" PromptSet
                var rootPrompts = new Dictionary<string, Prompt>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var file in System.IO.Directory.GetFiles(topLevelDir, "*.*", System.IO.SearchOption.TopDirectoryOnly))
                {
                    var ext = System.IO.Path.GetExtension(file);
                    if (!_supportedExtensions.Contains(ext, System.StringComparer.OrdinalIgnoreCase)) continue;
                    var name = System.IO.Path.GetFileNameWithoutExtension(file);
                    var content = System.IO.File.ReadAllText(file);
                    var format = GetFormatFromExtension(ext);
                    rootPrompts[name] = new Prompt(content, format);
                }
                if (rootPrompts.Count > 0)
                {
                    subSets["Root"] = new PromptSet { Name = "Root", Prompts = rootPrompts };
                }

                // Subfolders as sub prompt sets, with prompt inheritance logic
                foreach (var subDir in System.IO.Directory.GetDirectories(topLevelDir))
                {
                    var subName = System.IO.Path.GetFileName(subDir);
                    var subPrompts = new Dictionary<string, Prompt>(System.StringComparer.OrdinalIgnoreCase);
                    // Load prompts from subfolder
                    foreach (var file in System.IO.Directory.GetFiles(subDir, "*.*", System.IO.SearchOption.TopDirectoryOnly))
                    {
                        var ext = System.IO.Path.GetExtension(file);
                        if (!_supportedExtensions.Contains(ext, System.StringComparer.OrdinalIgnoreCase)) continue;
                        var name = System.IO.Path.GetFileNameWithoutExtension(file);
                        var content = System.IO.File.ReadAllText(file);
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
