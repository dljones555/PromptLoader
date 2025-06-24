using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PromptLoader.Models.MCP
{
    /// <summary>
    /// Represents a file-based root in the MCP standard.
    /// </summary>
    public class FileRoot : IRoot
    {
        /// <summary>
        /// Gets the URI of the root.
        /// </summary>
        public string Uri { get; }

        /// <summary>
        /// Gets the name of the root.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Creates a new instance of the FileRoot class.
        /// </summary>
        /// <param name="path">The file system path.</param>
        /// <param name="name">Optional custom name for the root.</param>
        public FileRoot(string path, string? name = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            var normalizedPath = Path.GetFullPath(path);
            Uri = $"file://{normalizedPath}";
            Name = name ?? Path.GetFileName(normalizedPath);
        }

        /// <summary>
        /// Gets the local file system path from this root's URI.
        /// </summary>
        /// <returns>The local file system path.</returns>
        public string GetLocalPath()
        {
            return Uri.Replace("file://", string.Empty).TrimStart('/');
        }
        
        /// <summary>
        /// Loads prompts from this root.
        /// </summary>
        /// <returns>A collection of prompt definitions.</returns>
        public async Task<IEnumerable<PromptDefinition>> LoadAsync()
        {
            var path = GetLocalPath();
            var result = new List<PromptDefinition>();

            if (!Directory.Exists(path))
            {
                return result;
            }

            // Look for .yml, .yaml, and .json files that might contain prompt definitions
            var files = Directory.GetFiles(path, "*.yml", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(path, "*.yaml", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles(path, "*.json", SearchOption.AllDirectories));

            foreach (var file in files)
            {
                try
                {
                    var extension = Path.GetExtension(file).ToLowerInvariant();
                    PromptDefinition? prompt = null;

                    if (extension == ".yml" || extension == ".yaml")
                    {
                        prompt = await LoadYamlPromptAsync(file);
                    }
                    else if (extension == ".json")
                    {
                        prompt = await LoadJsonPromptAsync(file);
                    }

                    if (prompt != null)
                    {
                        // If no name is specified in the file, use the filename
                        if (string.IsNullOrEmpty(prompt.Name))
                        {
                            prompt.Name = Path.GetFileNameWithoutExtension(file);
                        }

                        result.Add(prompt);
                    }
                }
                catch (Exception ex)
                {
                    // Log error or handle gracefully
                    Console.Error.WriteLine($"Error loading prompt from {file}: {ex.Message}");
                }
            }

            // If no prompts were found in YAML/JSON format, look for text files and create simple prompts
            if (result.Count == 0)
            {
                var textFiles = Directory.GetFiles(path, "*.txt", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(path, "*.md", SearchOption.AllDirectories));

                foreach (var file in textFiles)
                {
                    try
                    {
                        var name = Path.GetFileNameWithoutExtension(file);
                        var content = await File.ReadAllTextAsync(file);

                        // Create a message with the file content
                        var message = new PromptMessage
                        {
                            Role = "user",
                            Content = new TextContent { Text = content }
                        };

                        // Create a prompt definition with the message
                        var prompt = new PromptDefinition
                        {
                            Name = name,
                            Description = $"Prompt loaded from {Path.GetFileName(file)}",
                            Messages = new List<PromptMessage> { message }
                        };

                        result.Add(prompt);
                    }
                    catch (Exception ex)
                    {
                        // Log error or handle gracefully
                        Console.Error.WriteLine($"Error loading prompt from {file}: {ex.Message}");
                    }
                }
            }

            return result;
        }

        private async Task<PromptDefinition?> LoadYamlPromptAsync(string filePath)
        {
            var yaml = await File.ReadAllTextAsync(filePath);

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            return deserializer.Deserialize<PromptDefinition>(yaml);
        }

        private async Task<PromptDefinition?> LoadJsonPromptAsync(string filePath)
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<PromptDefinition>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
    }
}