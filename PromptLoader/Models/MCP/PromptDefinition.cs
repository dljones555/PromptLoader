using System.Collections.Generic;
using System.Text.Json.Serialization;

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

        /// <summary>
        /// Gets or sets the template string for the prompt.
        /// </summary>
        public string? Template { get; set; }

        /// <summary>
        /// Gets or sets the messages for the prompt.
        /// </summary>
        public List<PromptMessage>? Messages { get; set; }

        /// <summary>
        /// Gets or sets the version of the prompt.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Gets or sets the author of the prompt.
        /// </summary>
        public string? Author { get; set; }

        /// <summary>
        /// Gets or sets the tags for the prompt.
        /// </summary>
        public List<string>? Tags { get; set; }

        /// <summary>
        /// Gets or sets the language of the prompt.
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Gets or sets the category of the prompt.
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Gets or sets whether the prompt is a draft.
        /// </summary>
        public bool IsDraft { get; set; }

        /// <summary>
        /// Gets or sets parent prompts that this prompt extends or inherits from.
        /// </summary>
        public List<string>? Extends { get; set; }

        /// <summary>
        /// Gets or sets examples of prompt usage.
        /// </summary>
        public List<PromptExample>? Examples { get; set; }

        /// <summary>
        /// Gets or sets the metadata for the prompt.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Represents an example of prompt usage.
    /// </summary>
    public class PromptExample
    {
        /// <summary>
        /// Gets or sets the name of the example.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the example.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the arguments for the example.
        /// </summary>
        public Dictionary<string, object>? Arguments { get; set; }

        /// <summary>
        /// Gets or sets the expected result of the example.
        /// </summary>
        public string? ExpectedResult { get; set; }
    }
}