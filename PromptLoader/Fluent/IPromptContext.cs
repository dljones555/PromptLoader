using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;
using PromptLoader.Models;

namespace PromptLoader.Fluent
{
    public interface IPromptContext
    {
        static abstract PromptContext FromFile(string file = "", bool cascadeOverride = true);
        static abstract PromptContext FromFolder(string folder = "", bool cascadeOverride = true);
        string AsString();
        PromptContext CombineWithRoot();
        PromptContext SeparateWith(string separator = "");
        PromptContext Get(string path);
        Task<PromptContext> LoadAsync();
        PromptContext WithConfig(IConfiguration config);
        PromptContext WithConfig(string configPath);

        // Added from IPromptService
        Dictionary<string, Prompt> Prompts { get; }
        Dictionary<string, Dictionary<string, PromptSet>> PromptSets { get; }
        PromptListType PromptListType { get; }
        Task<Dictionary<string, Prompt>> LoadPromptsAsync(bool cascadeOverride = true, string? promptsFolder = null);
        Task<Dictionary<string, Dictionary<string, PromptSet>>> LoadPromptSetsAsync(bool cascadeOverride = true, string? promptSetFolder = null);
        string GetCombinedPrompts(Dictionary<string, PromptSet> promptSets, string setName, string? separator = null);
        string GetCombinedPrompts(PromptSet promptSet, PromptSet? rootSet = null, string? separator = null);
        Task<Prompt?> LoadPromptAsync(string filePath);
    }
}