using Microsoft.Extensions.Configuration;
using PromptLoader.Models;

namespace PromptLoader.Services
{
    public static class PromptSetLoader
    {
        public static Dictionary<string, PromptSet> LoadPromptSets(string rootFolder)
        {
            var sets = new Dictionary<string, PromptSet>(StringComparer.OrdinalIgnoreCase);
            foreach (var setName in Directory.GetDirectories(rootFolder))
            {
                var fileNameAsPrompt = Path.GetFileName(setName);
                var prompts = PromptLoader.LoadPrompts(setName);
                sets[setName] = new PromptSet { Name = fileNameAsPrompt, Prompts = prompts };
            }
            return sets;
        }

        // New overload: uses PromptOrder from configuration
        public static string JoinPrompts(Dictionary<string, PromptSet> promptSets, string setName, IConfiguration config)
        {
            if (!promptSets.TryGetValue(setName, out var promptSet))
                throw new KeyNotFoundException($"Prompt set '{setName}' not found.");

            var promptOrder = config.GetSection("PromptOrder").Get<string[]>(); // 'Get' requires Microsoft.Extensions.Configuration.Binder
            if (promptOrder != null && promptOrder.Length > 0)
            {
                var ordered = new List<string>();
                foreach (var key in promptOrder)
                {
                    if (promptSet.Prompts.TryGetValue(key, out var prompt))
                        ordered.Add(prompt.Text);
                }
                return string.Join(Environment.NewLine, ordered);
            }
            // Fallback: join all prompts in default order
            return string.Join(Environment.NewLine, promptSet.Prompts.Values.Select(x => x.Text));
        }
    }
}
