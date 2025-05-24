using PromptLoader.Models;

namespace PromptLoader.Services
{

    public static class PromptLoader
    {
        private static readonly string[] SupportedExtensions =
            { ".txt", ".prompt", ".yml", ".jinja", ".jinja2", ".prompt.md" };

        public static Dictionary<string, Prompt> LoadPrompts(string folderPath, bool cascadeOverride = true)
        {
            var promptFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f.Count(c => c == Path.DirectorySeparatorChar)); // Shallowest first

            var prompts = new Dictionary<string, Prompt>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in promptFiles)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var content = File.ReadAllText(file);
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
