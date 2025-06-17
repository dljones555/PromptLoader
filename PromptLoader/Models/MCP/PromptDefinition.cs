using System.Collections.Generic;

namespace PromptLoader.Models.MCP
{
    /// <summary>
    /// Represents a prompt definition according to the Model Context Protocol (MCP) standard.
    /// </summary>
    public class PromptDefinition
    {
        /// <summary>
        /// Gets or sets the unique identifier for the prompt.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable description of the prompt.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the list of arguments that the prompt accepts.
        /// </summary>
        public List<PromptArgument>? Arguments { get; set; }

        /// <summary>
        /// Gets or sets the model to use with this prompt.
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Gets or sets additional parameters for the model.
        /// </summary>
        public Dictionary<string, object>? ModelParameters { get; set; }
    }
}