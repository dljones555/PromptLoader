using PromptLoader.Models.MCP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PromptLoader.Core
{
    /// <summary>
    /// Loads prompts from file system roots.
    /// </summary>
    public class FilePromptLoader : IPromptLoader
    {
        private static readonly Regex ArgumentPlaceholderRegex = new(@"\{\{([^{}]+)\}\}", RegexOptions.Compiled);

        /// <summary>
        /// Loads prompts from a root.
        /// </summary>
        /// <param name="root">The root to load prompts from.</param>
        /// <returns>A collection of prompt definitions.</returns>
        public async Task<IEnumerable<PromptDefinition>> LoadAsync(IRoot root)
        {
            if (root is not FileRoot fileRoot)
                throw new ArgumentException("Root must be a FileRoot", nameof(root));

            var path = fileRoot.GetLocalPath();
            var result = new List<PromptDefinition>();

            if (!Directory.Exists(path))
                return result;

            // Look for .yml, .yaml, and .json files
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

            // If no prompts were found in YAML/JSON format, look for text files
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

                        // Create a simple prompt definition with the text content as a user message
                        var prompt = new PromptDefinition
                        {
                            Name = name,
                            Description = $"Prompt loaded from {Path.GetFileName(file)}"
                        };

                        // Add a default message with the file content
                        var message = new PromptMessage
                        {
                            Role = "user",
                            Content = new TextContent { Text = content }
                        };

                        prompt.Messages = new List<PromptMessage> { message };
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

        /// <summary>
        /// Replaces argument placeholders in the content with actual values.
        /// </summary>
        /// <param name="content">The content with placeholders.</param>
        /// <param name="arguments">The arguments to use for replacement.</param>
        /// <returns>The content with placeholders replaced.</returns>
        public static string ReplaceArgumentPlaceholders(string content, Dictionary<string, string> arguments)
        {
            if (string.IsNullOrEmpty(content) || arguments == null || arguments.Count == 0)
                return content;

            return ArgumentPlaceholderRegex.Replace(content, match =>
            {
                var argName = match.Groups[1].Value.Trim();
                return arguments.TryGetValue(argName, out var value) ? value : match.Value;
            });
        }
    }
}