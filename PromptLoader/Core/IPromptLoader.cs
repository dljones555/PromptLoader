using PromptLoader.Models.MCP;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PromptLoader.Core
{
    /// <summary>
    /// Interface for loading prompts from different sources.
    /// </summary>
    public interface IPromptLoader
    {
        /// <summary>
        /// Loads prompts from a root.
        /// </summary>
        /// <param name="root">The root to load prompts from.</param>
        /// <returns>A collection of prompt definitions.</returns>
        Task<IEnumerable<PromptDefinition>> LoadAsync(IRoot root);
    }
}