using Microsoft.Extensions.Configuration;
using PromptLoader.Models;

namespace PromptLoader.Services
{
    public static class PromptSetLoader
    {
        // Returns a two-level dictionary: top-level folder -> (subfolder or "Main") -> PromptSet
        public static Dictionary<string, Dictionary<string, PromptSet>> LoadPromptSets(string rootFolder, bool cascadeOverride = true)
        {
            var result = new Dictionary<string, Dictionary<string, PromptSet>>(StringComparer.OrdinalIgnoreCase);
            foreach (var topLevelDir in Directory.GetDirectories(rootFolder))
            {
                var topLevelName = Path.GetFileName(topLevelDir);
                var subSets = new Dictionary<string, PromptSet>(StringComparer.OrdinalIgnoreCase);

                // Prompts directly in the top-level folder
                var mainPrompts = PromptLoader.LoadPrompts(topLevelDir, cascadeOverride);
                if (mainPrompts.Count > 0)
                {
                    subSets["Main"] = new PromptSet { Name = "Main", Prompts = mainPrompts };
                }

                // Subfolders as sub prompt sets
                foreach (var subDir in Directory.GetDirectories(topLevelDir))
                {
                    var subName = Path.GetFileName(subDir);
                    var prompts = PromptLoader.LoadPrompts(subDir, cascadeOverride);
                    subSets[subName] = new PromptSet { Name = subName, Prompts = prompts };
                }

                result[topLevelName] = subSets;
            }
            return result;
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
