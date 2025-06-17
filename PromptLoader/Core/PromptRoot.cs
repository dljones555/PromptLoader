using PromptLoader.Models.MCP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        private readonly PromptRootOptions _options;

        /// <summary>
        /// Gets the root information.
        /// </summary>
        public Root Root => _root;

        /// <summary>
        /// Creates a new instance of the PromptRoot class with the specified root.
        /// </summary>
        /// <param name="root">The root information.</param>
        /// <param name="options">Options for the root.</param>
        private PromptRoot(Root root, PromptRootOptions? options = null)
        {
            _root = root;
            _options = options ?? new PromptRootOptions();
        }

        /// <summary>
        /// Creates a PromptRoot from a file path.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="name">The name of the root.</param>
        /// <param name="options">Options for the root.</param>
        /// <returns>A new PromptRoot instance.</returns>
        public static PromptRoot FromFile(string path, string? name = null, PromptRootOptions? options = null)
        {
            var normalizedPath = Path.GetFullPath(path);
            var uri = $"file://{normalizedPath}";
            var rootName = name ?? Path.GetFileName(normalizedPath);
            
            return new PromptRoot(new Root(uri, rootName), options);
        }

        /// <summary>
        /// Creates a PromptRoot from a URI.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <param name="name">The name of the root.</param>
        /// <param name="options">Options for the root.</param>
        /// <returns>A new PromptRoot instance.</returns>
        public static PromptRoot FromUri(string uri, string name, PromptRootOptions? options = null)
        {
            return new PromptRoot(new Root(uri, name), options);
        }

        /// <summary>
        /// Creates a PromptRoot from a Git repository.
        /// </summary>
        /// <param name="repoUrl">The Git repository URL.</param>
        /// <param name="name">The name of the root.</param>
        /// <param name="options">Options for the root.</param>
        /// <returns>A new PromptRoot instance.</returns>
        public static PromptRoot FromGit(string repoUrl, string? name = null, PromptRootOptions? options = null)
        {
            var rootName = name ?? Path.GetFileNameWithoutExtension(repoUrl);
            return new PromptRoot(new Root(repoUrl, rootName), options);
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

            // Look for YAML, JSON, and other structured formats
            var structuredFiles = new List<string>();
            
            // Add standard MCP prompt formats
            structuredFiles.AddRange(Directory.GetFiles(path, "*.yml", SearchOption.AllDirectories));
            structuredFiles.AddRange(Directory.GetFiles(path, "*.yaml", SearchOption.AllDirectories));
            structuredFiles.AddRange(Directory.GetFiles(path, "*.json", SearchOption.AllDirectories));
            
            // Add prompt directories that contain a metadata file and message files
            foreach (var dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
            {
                var metadataFile = Path.Combine(dir, "prompt.yml");
                if (File.Exists(metadataFile))
                {
                    structuredFiles.Add(metadataFile);
                }
                else
                {
                    metadataFile = Path.Combine(dir, "prompt.yaml");
                    if (File.Exists(metadataFile))
                    {
                        structuredFiles.Add(metadataFile);
                    }
                    else
                    {
                        metadataFile = Path.Combine(dir, "prompt.json");
                        if (File.Exists(metadataFile))
                        {
                            structuredFiles.Add(metadataFile);
                        }
                    }
                }
            }

            foreach (var file in structuredFiles)
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
                        // If no name is specified in the file, use the filename or directory name
                        if (string.IsNullOrEmpty(prompt.Name))
                        {
                            var fileName = Path.GetFileNameWithoutExtension(file);
                            if (fileName.Equals("prompt", StringComparison.OrdinalIgnoreCase))
                            {
                                // If the file is called "prompt.yml", use the directory name
                                var dirName = Path.GetFileName(Path.GetDirectoryName(file) ?? string.Empty);
                                prompt.Name = dirName;
                            }
                            else
                            {
                                prompt.Name = fileName;
                            }
                        }

                        // Look for message files in the same directory
                        await LoadMessageFilesAsync(prompt, file);
                        
                        // If we still don't have a template or messages, try to extract from text files
                        if (string.IsNullOrEmpty(prompt.Template) && (prompt.Messages == null || prompt.Messages.Count == 0))
                        {
                            await TryExtractTemplateFromDirectoryAsync(prompt, Path.GetDirectoryName(file) ?? path);
                        }

                        result.Add(prompt);
                    }
                }
                catch (Exception ex)
                {
                    if (_options.ThrowOnError)
                    {
                        throw;
                    }
                    
                    // Log error or handle gracefully
                    Console.Error.WriteLine($"Error loading prompt from {file}: {ex.Message}");
                }
            }

            // If no prompts were found in YAML/JSON format and we're allowed to create from text files,
            // look for text files and create simple prompts
            if (result.Count == 0 && _options.CreateFromTextFiles)
            {
                var textFiles = new List<string>();
                foreach (var ext in _options.TextFileExtensions)
                {
                    textFiles.AddRange(Directory.GetFiles(path, $"*{ext}", SearchOption.AllDirectories));
                }

                foreach (var file in textFiles)
                {
                    try
                    {
                        var name = Path.GetFileNameWithoutExtension(file);
                        var content = await File.ReadAllTextAsync(file);

                        // Try to extract arguments from the text content
                        var arguments = ExtractArgumentsFromTemplate(content);
                        
                        var prompt = new PromptDefinition
                        {
                            Name = name,
                            Description = $"Prompt loaded from {Path.GetFileName(file)}",
                            Template = content,
                            Arguments = arguments
                        };
                        
                        result.Add(prompt);
                    }
                    catch (Exception ex)
                    {
                        if (_options.ThrowOnError)
                        {
                            throw;
                        }
                        
                        // Log error or handle gracefully
                        Console.Error.WriteLine($"Error loading prompt from {file}: {ex.Message}");
                    }
                }
            }

            return result;
        }

        private async Task LoadMessageFilesAsync(PromptDefinition prompt, string metadataFile)
        {
            var dir = Path.GetDirectoryName(metadataFile);
            if (dir == null)
                return;
                
            var messagesDir = Path.Combine(dir, "messages");
            if (!Directory.Exists(messagesDir))
                return;
                
            prompt.Messages ??= new List<PromptMessage>();
            
            // Look for message files that follow a pattern like "01-system.txt", "02-user.md", etc.
            var messageFiles = Directory.GetFiles(messagesDir, "*.*", SearchOption.TopDirectoryOnly);
            var orderedMessages = new List<(int Order, string File)>();
            
            foreach (var file in messageFiles)
            {
                var fileName = Path.GetFileName(file);
                var match = Regex.Match(fileName, @"^(\d+)-(\w+)\.");
                
                if (match.Success)
                {
                    var order = int.Parse(match.Groups[1].Value);
                    var role = match.Groups[2].Value.ToLowerInvariant();
                    
                    if (role is "system" or "user" or "assistant")
                    {
                        orderedMessages.Add((order, file));
                    }
                }
            }
            
            // Sort by order and create messages
            foreach (var (_, file) in orderedMessages.OrderBy(m => m.Order))
            {
                var fileName = Path.GetFileName(file);
                var match = Regex.Match(fileName, @"^(\d+)-(\w+)\.");
                var role = match.Groups[2].Value.ToLowerInvariant();
                var content = await File.ReadAllTextAsync(file);
                
                PromptMessage message;
                switch (role)
                {
                    case "system":
                        message = PromptMessage.System(content);
                        break;
                    case "user":
                        message = PromptMessage.User(content);
                        break;
                    case "assistant":
                        message = PromptMessage.Assistant(content);
                        break;
                    default:
                        continue;
                }
                
                prompt.Messages.Add(message);
            }
        }

        private async Task TryExtractTemplateFromDirectoryAsync(PromptDefinition prompt, string directory)
        {
            // Look for a template.txt or similar file
            var templateFile = Path.Combine(directory, "template.txt");
            if (File.Exists(templateFile))
            {
                prompt.Template = await File.ReadAllTextAsync(templateFile);
                return;
            }
            
            templateFile = Path.Combine(directory, "prompt.txt");
            if (File.Exists(templateFile))
            {
                prompt.Template = await File.ReadAllTextAsync(templateFile);
                return;
            }
            
            // Try looking for any text file with the same name as the prompt
            templateFile = Path.Combine(directory, $"{prompt.Name}.txt");
            if (File.Exists(templateFile))
            {
                prompt.Template = await File.ReadAllTextAsync(templateFile);
                return;
            }
        }

        private List<PromptArgument> ExtractArgumentsFromTemplate(string template)
        {
            var arguments = new List<PromptArgument>();
            var matches = Regex.Matches(template, @"\{\{\s*([a-zA-Z0-9_]+)(?:\s*\|\s*[^}]+)?\s*\}\}");
            
            foreach (Match match in matches)
            {
                var argName = match.Groups[1].Value;
                if (!arguments.Any(a => a.Name == argName))
                {
                    arguments.Add(new PromptArgument
                    {
                        Name = argName,
                        Description = $"Argument '{argName}' used in template",
                        Required = true
                    });
                }
            }
            
            return arguments;
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
    
    /// <summary>
    /// Options for prompt roots.
    /// </summary>
    public class PromptRootOptions
    {
        /// <summary>
        /// Gets or sets whether to throw exceptions on errors.
        /// </summary>
        public bool ThrowOnError { get; set; } = false;
        
        /// <summary>
        /// Gets or sets whether to create prompts from text files when no structured files are found.
        /// </summary>
        public bool CreateFromTextFiles { get; set; } = true;
        
        /// <summary>
        /// Gets or sets the extensions considered as text files.
        /// </summary>
        public string[] TextFileExtensions { get; set; } = new[] { ".txt", ".md", ".prompt" };
        
        /// <summary>
        /// Gets or sets whether to recursively process directories.
        /// </summary>
        public bool Recursive { get; set; } = true;
    }
}