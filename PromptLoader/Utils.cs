using Microsoft.Extensions.Configuration;
using PromptLoader.Models;

namespace PromptLoader.Utils
{
    public static class PathUtils
    {
        // Helper to resolve prompt folder path for dev, test, and published scenarios
        public static string ResolvePromptPath(string relativePath)
        {
            var exeDir = AppContext.BaseDirectory;
            var exePath = Path.GetFullPath(Path.Combine(exeDir, relativePath));
            if (Directory.Exists(exePath)) return exePath;

            // TODO: This is from CoPilot. Vet this logic.

            var dir = exeDir;
            for (int i = 0; i < 5; i++)
            {
                var candidate = Path.GetFullPath(Path.Combine(dir, relativePath));
                if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetFullPath(Path.Combine(dir, ".."));
            }
            return exePath;
        }

        public static string ResolvePromptsFolder(PromptLoaderOptions? options, IConfiguration? config = null)
        {
            return ResolvePromptPath(
                options?.PromptsFolder
                ?? config?["PromptsFolder"]
                ?? "Prompts");
        }

        public static string ResolvePromptSetFolder(PromptLoaderOptions? options, IConfiguration? config = null)
        {
            return ResolvePromptPath(
                options?.PromptSetFolder
                ?? config?["PromptSetFolder"]
                ?? "PromptSets");
        }

        public static string[] GetSupportedPromptExtensions(PromptLoaderOptions? options, IConfiguration? config = null)
        {
            if (options?.SupportedPromptExtensions != null && options.SupportedPromptExtensions.Length > 0)
                return options.SupportedPromptExtensions;
            var exts = config?.GetSection("SupportedPromptExtensions").Get<string[]>();
            if (exts != null && exts.Length > 0)
                return exts;
            return new[] { ".txt", ".prompt", ".yml", ".jinja", ".jinja2", ".prompt.md", ".md" };
        }
    }

    public static class PromptSetDictionaryExtensions
    {
        public static PromptSet Root(this Dictionary<string, PromptSet> sets)
            => sets.TryGetValue("Root", out var root) ? root : throw new InvalidOperationException("No Root set found.");
    }
}