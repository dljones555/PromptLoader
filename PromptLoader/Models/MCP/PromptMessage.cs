using System.Collections.Generic;

namespace PromptLoader.Models.MCP
{
    /// <summary>
    /// Represents a message in a prompt response according to the MCP standard.
    /// </summary>
    public class PromptMessage
    {
        /// <summary>
        /// Gets or sets the role of the message (e.g., "user", "assistant", "system").
        /// </summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the content of the message.
        /// </summary>
        public MessageContent Content { get; set; } = null!;

        /// <summary>
        /// Creates a new user message with text content.
        /// </summary>
        /// <param name="text">The text content of the message.</param>
        /// <returns>A new PromptMessage instance.</returns>
        public static PromptMessage User(string text)
        {
            return new PromptMessage
            {
                Role = "user",
                Content = new TextContent { Text = text }
            };
        }

        /// <summary>
        /// Creates a new system message with text content.
        /// </summary>
        /// <param name="text">The text content of the message.</param>
        /// <returns>A new PromptMessage instance.</returns>
        public static PromptMessage System(string text)
        {
            return new PromptMessage
            {
                Role = "system",
                Content = new TextContent { Text = text }
            };
        }

        /// <summary>
        /// Creates a new assistant message with text content.
        /// </summary>
        /// <param name="text">The text content of the message.</param>
        /// <returns>A new PromptMessage instance.</returns>
        public static PromptMessage Assistant(string text)
        {
            return new PromptMessage
            {
                Role = "assistant",
                Content = new TextContent { Text = text }
            };
        }
    }

    /// <summary>
    /// Base class for message content in the MCP standard.
    /// </summary>
    public abstract class MessageContent
    {
        /// <summary>
        /// Gets or sets the type of the content.
        /// </summary>
        public string Type { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents text content in a message.
    /// </summary>
    public class TextContent : MessageContent
    {
        /// <summary>
        /// Creates a new instance of the TextContent class.
        /// </summary>
        public TextContent()
        {
            Type = "text";
        }

        /// <summary>
        /// Gets or sets the text content.
        /// </summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents resource content in a message.
    /// </summary>
    public class ResourceContent : MessageContent
    {
        /// <summary>
        /// Creates a new instance of the ResourceContent class.
        /// </summary>
        public ResourceContent()
        {
            Type = "resource";
        }

        /// <summary>
        /// Gets or sets the resource information.
        /// </summary>
        public Resource Resource { get; set; } = null!;
    }

    /// <summary>
    /// Represents a resource in the MCP standard.
    /// </summary>
    public class Resource
    {
        /// <summary>
        /// Gets or sets the URI of the resource.
        /// </summary>
        public string Uri { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the text content of the resource.
        /// </summary>
        public string? Text { get; set; }

        /// <summary>
        /// Gets or sets the MIME type of the resource.
        /// </summary>
        public string? MimeType { get; set; }
    }

    /// <summary>
    /// Represents the result of a prompt/get request in the MCP standard.
    /// </summary>
    public class GetPromptResult
    {
        /// <summary>
        /// Gets or sets the description of the prompt.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the messages in the prompt.
        /// </summary>
        public List<PromptMessage> Messages { get; set; } = new List<PromptMessage>();
    }
}