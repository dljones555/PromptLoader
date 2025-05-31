using System.Collections.Generic;
using System.Threading.Tasks;
using PromptLoader.Models;

namespace PromptLoader.Services
{
    public interface IPromptService
    {
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
