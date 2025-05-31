using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using PromptLoader.Models;
using PromptLoader.Utils;

namespace PromptLoader.Services
{
    /// <summary>
    /// Represents a context for managing prompts.
    /// </summary>
    public class PromptContext
    {
        private string? _folder;
        private string? _file;
        private IConfiguration? _config;
        private bool _combineWithBase;
        private string? _getPath;
        private Dictionary<string, Prompt> _prompts = new();
        private Dictionary<string, PromptSet> _promptSets = new();
        private PromptSet? _selectedSet;
        private Prompt? _selectedPrompt;
        private string[] _supportedExtensions = new[] { ".txt", ".prompt", ".yml", ".jinja", ".jinja2", ".prompt.md", ".md" };

        private PromptContext() { }
        private PromptContext(string folder) { _folder = folder; }
        private PromptContext(string file, bool isFile) { _file = file; }

        public static PromptContext FromFolder(string folder) => new PromptContext(folder);
        public static PromptContext FromFile(string file) => new PromptContext(file, true);

        public PromptContext WithConfig(string configPath)
        {
            _config = new ConfigurationBuilder().AddJsonFile(configPath, optional: false, reloadOnChange: true).Build();
            return this;
        }
        public PromptContext WithConfig(IConfiguration config)
        {
            _config = config;
            return this;
        }

        public async Task<PromptContext> LoadAsync()
        {
            if (_folder != null)
            {
                await LoadFromFolderAsync(_folder);
            }
            else if (_file != null)
            {
                await LoadFromFileAsync(_file);
            }
            return this;
        }

        public PromptContext Get(string path)
        {
            _getPath = path;
            if (Path.HasExtension(path))
            {
                _selectedPrompt = _prompts.TryGetValue(NormalizePath(path), out var p) ? p : null;
                _selectedSet = null;
            }
            else
            {
                _selectedSet = _promptSets.TryGetValue(NormalizePath(path), out var s) ? s : null;
                _selectedPrompt = null;
            }
            return this;
        }

        public PromptContext CombineWithBase()
        {
            _combineWithBase = true;
            return this;
        }

        public async Task<string> AsStringAsync()
        {
            await Task.CompletedTask;
            if (_selectedPrompt != null)
                return _selectedPrompt.Text;
            if (_selectedSet != null)
            {
                PromptSet? baseSet = null;
                if (_combineWithBase && _promptSets.TryGetValue("Base", out var b))
                    baseSet = b;
                return CombinePrompts(_selectedSet, baseSet);
            }
            return string.Empty;
        }

        // --- Internal helpers ---
        private async Task LoadFromFolderAsync(string folder)
        {
            if (!Directory.Exists(folder))
            {
                _prompts.Clear();
                _promptSets.Clear();
                return;
            }
            // Constrain prompt list logic
            bool constrain = _config?.GetValue("ConstrainPromptList", false) ?? false;
            var allowedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (constrain)
            {
                var promptList = _config?.GetSection("PromptList").Get<string[]>();
                if (promptList != null)
                    allowedNames.UnionWith(promptList);
            }
            // Recursively load all prompts and sets
            var promptFiles = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
                .Where(f => _supportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
            foreach (var file in promptFiles)
            {
                var relPath = NormalizePath(Path.GetRelativePath(folder, file));
                var name = Path.GetFileNameWithoutExtension(file);
                if (constrain && allowedNames.Count > 0 && !allowedNames.Contains(name))
                    continue;
                var content = await File.ReadAllTextAsync(file);
                var format = GetFormatFromExtension(Path.GetExtension(file));
                var prompt = new Prompt(content, format);
                _prompts[relPath] = prompt;
            }
            // Load sets: each directory is a set, key is relative path
            foreach (var dir in Directory.GetDirectories(folder, "*", SearchOption.AllDirectories).Append(folder))
            {
                var relDir = NormalizePath(Path.GetRelativePath(folder, dir));
                if (relDir == ".") relDir = "Base";
                var setPrompts = new Dictionary<string, Prompt>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
                {
                    if (!_supportedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) continue;
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (constrain && allowedNames.Count > 0 && !allowedNames.Contains(name))
                        continue;
                    var content = await File.ReadAllTextAsync(file);
                    var format = GetFormatFromExtension(Path.GetExtension(file));
                    setPrompts[name] = new Prompt(content, format);
                }
                _promptSets[relDir] = new PromptSet { Name = relDir, Prompts = setPrompts };
            }
        }

        private async Task LoadFromFileAsync(string file)
        {
            if (!File.Exists(file)) return;
            var content = await File.ReadAllTextAsync(file);
            var format = GetFormatFromExtension(Path.GetExtension(file));
            var name = Path.GetFileNameWithoutExtension(file);
            _prompts[NormalizePath(file)] = new Prompt(content, format);
        }

        // Combine prompts from set, falling back to Base for missing keys
        private string CombinePrompts(PromptSet set, PromptSet? baseSet)
        {
            var promptList = _config?.GetSection("PromptList").Get<string[]>() ?? set.Prompts.Keys.ToArray();
            var allKeys = new List<string>();
            foreach (var key in promptList)
            {
                if (set.Prompts.ContainsKey(key) || (baseSet != null && baseSet.Prompts.ContainsKey(key)))
                    allKeys.Add(key);
            }
            foreach (var kvp in set.Prompts)
            {
                if (!promptList.Contains(kvp.Key))
                    allKeys.Add(kvp.Key);
            }
            var sepTemplate = _config?["PromptSeparator"] ?? Environment.NewLine;
            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < allKeys.Count; i++)
            {
                var key = allKeys[i];
                Prompt? prompt = null;
                if (set.Prompts.TryGetValue(key, out var p))
                    prompt = p;
                else if (baseSet != null && baseSet.Prompts.TryGetValue(key, out var bp))
                    prompt = bp;
                if (prompt == null) continue;
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

        private static string NormalizePath(string path)
        {
            return path.Replace("\\", "/").TrimStart('.', '/');
        }

        // Get parent path for a set (e.g. CustomerService/Refund -> CustomerService)
        private static string? GetParentPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 1) return null;
            return string.Join('/', parts.Take(parts.Length - 1));
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
        private static string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return char.ToUpperInvariant(input[0]) + input.Substring(1).ToLowerInvariant();
        }
    }
}
