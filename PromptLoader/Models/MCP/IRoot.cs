using System.Collections.Generic;
using System.Threading.Tasks;

namespace PromptLoader.Models.MCP
{
    /// <summary>
    /// Interface for a root in the MCP standard.
    /// </summary>
    public interface IRoot
    {
        /// <summary>
        /// Gets the URI of the root.
        /// </summary>
        string Uri { get; }

        /// <summary>
        /// Gets the name of the root.
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Loads prompts from this root.
        /// </summary>
        /// <returns>A collection of prompt definitions.</returns>
        Task<IEnumerable<PromptDefinition>> LoadAsync();
    }
}