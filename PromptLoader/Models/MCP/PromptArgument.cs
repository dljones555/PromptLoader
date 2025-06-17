namespace PromptLoader.Models.MCP
{
    /// <summary>
    /// Represents an argument for a prompt in the Model Context Protocol (MCP) standard.
    /// </summary>
    public class PromptArgument
    {
        /// <summary>
        /// Gets or sets the identifier for the argument.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the argument.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets whether the argument is required.
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Creates a new instance of the PromptArgument class.
        /// </summary>
        public PromptArgument() { }

        /// <summary>
        /// Creates a new instance of the PromptArgument class with the specified name.
        /// </summary>
        /// <param name="name">The name of the argument.</param>
        public PromptArgument(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Creates a new instance of the PromptArgument class with the specified name and description.
        /// </summary>
        /// <param name="name">The name of the argument.</param>
        /// <param name="description">The description of the argument.</param>
        /// <param name="required">Whether the argument is required.</param>
        public PromptArgument(string name, string description, bool required = false)
        {
            Name = name;
            Description = description;
            Required = required;
        }
    }
}