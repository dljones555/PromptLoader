using PromptLoader.Models.MCP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PromptLoader.Core
{
    /// <summary>
    /// Represents a source of prompts in the MCP standard.
    /// </summary>
    public class PromptRoot
    {
        private readonly Root _root;

        /// <summary>
        /// Gets the root information.
        /// </summary>
        public Root Root => _root;

        /// <summary>
        /// Creates a new instance of the PromptRoot class with the specified root.
        /// </summary>
        /// <param name="root">The root information.</param>
        private PromptRoot(Root root)
        {
            _root = root;
        }

        /// <summary>
        /// Creates a PromptRoot from a file path.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="name">The name of the root.</param>
        /// <returns>A new PromptRoot instance.</returns>
        public static PromptRoot FromFile(string path, string? name = null)
        {
            var normalizedPath = Path.GetFullPath(path);
            var uri = $"file://{normalizedPath}";
            var rootName = name ?? Path.GetFileName(normalizedPath);
            
            return new PromptRoot(new Root(uri, rootName));
        }

        /// <summary>
        /// Creates a PromptRoot from a URI.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <param name="name">The name of the root.</param>
        /// <returns>A new PromptRoot instance.</returns>
        public static PromptRoot FromUri(string uri, string name)
        {
            return new PromptRoot(new Root(uri, name));
        }

        /// <summary>
        /// Creates a PromptRoot from a Git repository.
        /// </summary>
        /// <param name="repoUrl">The Git repository URL.</param>
        /// <param name="name">The name of the root.</param>
        /// <returns>A new PromptRoot instance.</returns>
        public static PromptRoot FromGit(string repoUrl, string? name = null)
        {
            var rootName = name ?? Path.GetFileNameWithoutExtension(repoUrl);
            return new PromptRoot(new Root(repoUrl, rootName));
        }

        /// <summary>
        /// Loads prompts from this root.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task<IEnumerable<PromptDefinition>> LoadPromptsAsync()
        {
            if (_root.IsFileUri())
            {
                return await LoadPromptsFromFileSystemAsync();
            }
            else if (_root.IsHttpUri())
            {
                return await LoadPromptsFromHttpAsync();
            }
            else if (_root.IsGitUri())
            {
                return await LoadPromptsFromGitAsync();
            }
            else
            {
                throw new NotSupportedException($"Unsupported root URI: {_root.Uri}");
            }
        }

        private async Task<IEnumerable<PromptDefinition>> LoadPromptsFromFileSystemAsync()
        {
            var path = _root.GetLocalPath();
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

                        result.Add(new PromptDefinition
                        {
                            Name = name,
                            Description = $"Prompt loaded from {Path.GetFileName(file)}"
                        });
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

        private async Task<IEnumerable<PromptDefinition>> LoadPromptsFromHttpAsync()
        {
            // Implementation would load prompts from an HTTP endpoint
            // This is a placeholder for now
            await Task.CompletedTask;
            return Array.Empty<PromptDefinition>();
        }

        private async Task<IEnumerable<PromptDefinition>> LoadPromptsFromGitAsync()
        {
            // Implementation would load prompts from a Git repository
            // This is a placeholder for now
            await Task.CompletedTask;
            return Array.Empty<PromptDefinition>();
        }
    }
}