using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PromptLoader.Models;
using PromptLoader.Utils;

namespace PromptLoader.Fluent
{
    public class FileSystemPromptSource : IPromptSource
    {
        private readonly string _folder;
        private readonly bool _cascadeOverride;
        private readonly string[] _supportedExtensions;
        public FileSystemPromptSource(string folder, bool cascadeOverride = true, string[]? supportedExtensions = null)
        {
            _folder = PathUtils.ResolvePromptPath(folder);
            _cascadeOverride = cascadeOverride;
            _supportedExtensions = supportedExtensions ?? PathUtils.GetSupportedPromptExtensions(null, null);
        }

        public async Task<Dictionary<string, Prompt>> LoadPromptsAsync()
        {
            if (!Directory.Exists(_folder))
                return new Dictionary<string, Prompt>(StringComparer.OrdinalIgnoreCase);

            var promptFiles = Directory.GetFiles(_folder, "*.*", SearchOption.AllDirectories)
                .Where(f => _supportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f.Count(c => c == Path.DirectorySeparatorChar));

            var prompts = new Dictionary<string, Prompt>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in promptFiles)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var content = await File.ReadAllTextAsync(file);
                var format = GetFormatFromExtension(Path.GetExtension(file));
                var prompt = new Prompt(content, format);
                if (_cascadeOverride && prompts.ContainsKey(name))
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

        public async Task<Dictionary<string, Dictionary<string, PromptSet>>> LoadPromptSetsAsync()
        {
            if (!Directory.Exists(_folder))
                return new Dictionary<string, Dictionary<string, PromptSet>>(StringComparer.OrdinalIgnoreCase);

            var result = new Dictionary<string, Dictionary<string, PromptSet>>(StringComparer.OrdinalIgnoreCase);

            var rootLevelPrompts = new Dictionary<string, Prompt>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(_folder, "*.*", SearchOption.TopDirectoryOnly))
            {
                var ext = Path.GetExtension(file);
                if (!_supportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;
                var name = Path.GetFileNameWithoutExtension(file);
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

            foreach (var topLevelDir in Directory.GetDirectories(_folder))
            {
                var topLevelName = Path.GetFileName(topLevelDir);
                var subSets = new Dictionary<string, PromptSet>(StringComparer.OrdinalIgnoreCase);

                var rootPrompts = new Dictionary<string, Prompt>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in Directory.GetFiles(topLevelDir, "*.*", SearchOption.TopDirectoryOnly))
                {
                    var ext = Path.GetExtension(file);
                    if (!_supportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;
                    var name = Path.GetFileNameWithoutExtension(file);
                    var content = await File.ReadAllTextAsync(file);
                    var format = GetFormatFromExtension(ext);
                    rootPrompts[name] = new Prompt(content, format);
                }
                if (rootPrompts.Count > 0)
                {
                    subSets["Root"] = new PromptSet { Name = "Root", Prompts = rootPrompts };
                }

                foreach (var subDir in Directory.GetDirectories(topLevelDir))
                {
                    var subName = Path.GetFileName(subDir);
                    var subPrompts = new Dictionary<string, Prompt>(StringComparer.OrdinalIgnoreCase);
                    foreach (var file in Directory.GetFiles(subDir, "*.*", SearchOption.TopDirectoryOnly))
                    {
                        var ext = Path.GetExtension(file);
                        if (!_supportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;
                        var name = Path.GetFileNameWithoutExtension(file);
                        var content = await File.ReadAllTextAsync(file);
                        var format = GetFormatFromExtension(ext);
                        subPrompts[name] = new Prompt(content, format);
                    }
                    foreach (var parentPrompt in rootPrompts)
                    {
                        if (!subPrompts.ContainsKey(parentPrompt.Key))
                        {
                            subPrompts[parentPrompt.Key] = parentPrompt.Value;
                        }
                        else if (!_cascadeOverride)
                        {
                            subPrompts[parentPrompt.Key] = parentPrompt.Value;
                        }
                    }
                    subSets[subName] = new PromptSet { Name = subName, Prompts = subPrompts };
                }
                result[topLevelName] = subSets;
            }
            return result;
        }

        private PromptFormat GetFormatFromExtension(string extension)
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
