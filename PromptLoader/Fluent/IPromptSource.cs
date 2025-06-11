using System.Collections.Generic;
using System.Threading.Tasks;
using PromptLoader.Models;

namespace PromptLoader.Fluent
{
    public interface IPromptSource
    {
        Task<Dictionary<string, Prompt>> LoadPromptsAsync();
        Task<Dictionary<string, Dictionary<string, PromptSet>>> LoadPromptSetsAsync();
    }
}
