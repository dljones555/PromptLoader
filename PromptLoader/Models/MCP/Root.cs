using System;

namespace PromptLoader.Models.MCP
{
    /// <summary>
    /// Represents a root in the MCP standard.
    /// </summary>
    public class Root
    {
        /// <summary>
        /// Gets or sets the URI of the root.
        /// </summary>
        public string Uri { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the root.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Creates a new instance of the Root class.
        /// </summary>
        public Root() { }

        /// <summary>
        /// Creates a new instance of the Root class with the specified URI and name.
        /// </summary>
        /// <param name="uri">The URI of the root.</param>
        /// <param name="name">The name of the root.</param>
        public Root(string uri, string name)
        {
            Uri = uri;
            Name = name;
        }

        /// <summary>
        /// Determines whether the URI is a file URI.
        /// </summary>
        /// <returns>True if the URI is a file URI; otherwise, false.</returns>
        public bool IsFileUri()
        {
            return Uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether the URI is an HTTP URI.
        /// </summary>
        /// <returns>True if the URI is an HTTP or HTTPS URI; otherwise, false.</returns>
        public bool IsHttpUri()
        {
            return Uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   Uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether the URI is a Git URI.
        /// </summary>
        /// <returns>True if the URI is a Git URI; otherwise, false.</returns>
        public bool IsGitUri()
        {
            return Uri.StartsWith("git://", StringComparison.OrdinalIgnoreCase) ||
                   Uri.EndsWith(".git", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the local path from a file URI.
        /// </summary>
        /// <returns>The local path.</returns>
        public string GetLocalPath()
        {
            if (!IsFileUri())
            {
                throw new InvalidOperationException("Not a file URI");
            }

            return Uri.Replace("file://", string.Empty).TrimStart('/');
        }
    }
}