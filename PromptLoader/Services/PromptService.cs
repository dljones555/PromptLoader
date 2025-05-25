using Microsoft.Extensions.Configuration;
using PromptLoader.Models;
using PromptLoader.Utils;
using System.Collections.Generic;

namespace PromptLoader.Services
{
    public interface IPromptService
    {
        Dictionary<string, Prompt> Prompts { get; }
        Dictionary<string, Dictionary<string, PromptSet>> PromptSets { get; }
        Dictionary<string, Prompt> LoadPrompts(bool cascadeOverride = true);
        Dictionary<string, Dictionary<string, PromptSet>> LoadPromptSets(bool cascadeOverride = true);
        string JoinPrompts(Dictionary<string, PromptSet> promptSets, string setName);
        string JoinPrompts(PromptSet promptSet); // New overload
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

        public PromptService(IConfiguration config)
        {
            _config = config;
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
        /// Joins prompts in a PromptSet according to PromptOrder in config.
        /// </summary>
        public string JoinPrompts(PromptSet promptSet)
        {
            var promptOrder = _config.GetSection("PromptOrder").Get<string[]>();
            if (promptOrder != null && promptOrder.Length > 0)
            {
                var ordered = new List<string>();
                foreach (var key in promptOrder)
                {
                    if (promptSet.Prompts.TryGetValue(key, out var prompt))
                        ordered.Add(prompt.Text);
                }
                return string.Join(System.Environment.NewLine, ordered);
            }
            // Fallback: join all prompts in default order
            return string.Join(System.Environment.NewLine, promptSet.Prompts.Values.Select(x => x.Text));
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

                // Prompts directly in the top-level folder
                var mainPrompts = LoadPromptsInternal(topLevelDir, cascadeOverride);
                if (mainPrompts.Count > 0)
                {
                    subSets["Main"] = new PromptSet { Name = "Main", Prompts = mainPrompts };
                }

                // Subfolders as sub prompt sets
                foreach (var subDir in System.IO.Directory.GetDirectories(topLevelDir))
                {
                    var subName = System.IO.Path.GetFileName(subDir);
                    var prompts = LoadPromptsInternal(subDir, cascadeOverride);
                    subSets[subName] = new PromptSet { Name = subName, Prompts = prompts };
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
