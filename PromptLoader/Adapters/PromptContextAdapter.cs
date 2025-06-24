using Microsoft.Extensions.Configuration;
using PromptLoader.Core;
using PromptLoader.Fluent;
using PromptLoader.Models;
using PromptLoader.Models.MCP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PromptLoader.Adapters
{
    /// <summary>
    /// Adapter to bridge the legacy PromptContext to the new PromptStore.
    /// </summary>
    public class PromptContextAdapter
    {
        private readonly PromptContext _legacyContext;
        private PromptStore? _store;

        /// <summary>
        /// Creates a new instance of the PromptContextAdapter class.
        /// </summary>
        /// <param name="legacyContext">The legacy PromptContext to adapt.</param>
        public PromptContextAdapter(PromptContext legacyContext)
        {
            _legacyContext = legacyContext;
        }

        /// <summary>
        /// Creates a new instance of the PromptContextAdapter class from a file.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="config">The configuration.</param>
        /// <returns>A task that returns a new PromptContextAdapter instance.</returns>
        public static async Task<PromptContextAdapter> FromFileAsync(string filePath, IConfiguration config)
        {
            var context = await PromptContext
                .FromFile(filePath)
                .WithConfig(config)
                .LoadAsync();

            return new PromptContextAdapter(context);
        }

        /// <summary>
        /// Creates a new instance of the PromptContextAdapter class from a folder.
        /// </summary>
        /// <param name="folderPath">The folder path.</param>
        /// <param name="config">The configuration.</param>
        /// <returns>A task that returns a new PromptContextAdapter instance.</returns>
        public static async Task<PromptContextAdapter> FromFolderAsync(string folderPath, IConfiguration config)
        {
            var context = await PromptContext
                .FromFolder(folderPath)
                .WithConfig(config)
                .LoadAsync();

            return new PromptContextAdapter(context);
        }

        /// <summary>
        /// Gets a prompt store from the legacy context.
        /// </summary>
        /// <returns>A PromptStore instance.</returns>
        public async Task<PromptStore> GetStoreAsync()
        {
            if (_store != null)
                return _store;

            var roots = new List<IRoot>();

            // Convert legacy prompt sets to roots
            foreach (var setKvp in _legacyContext.PromptSets)
            {
                var setName = setKvp.Key;
                if (setName == "Root") continue; // Skip root, it's handled differently

                var path = Path.Combine(AppContext.BaseDirectory, "PromptSets", setName);
                if (Directory.Exists(path))
                {
                    roots.Add(PromptRoot.FromFile(path, setName));
                }
            }

            // Create the store
            _store = new PromptStore(roots);
            await _store.InitializeAsync();
            return _store;
        }

        /// <summary>
        /// Converts a legacy Prompt to a PromptDefinition.
        /// </summary>
        /// <param name="prompt">The legacy prompt.</param>
        /// <param name="name">The name of the prompt.</param>
        /// <returns>A new PromptDefinition.</returns>
        private PromptDefinition ConvertToPromptDefinition(Prompt prompt, string name)
        {
            return new PromptDefinition
            {
                Name = name,
                Description = $"Converted from legacy prompt: {name}",
                Arguments = new List<PromptArgument>()
            };
        }

        /// <summary>
        /// Gets a prompt composer for a specific prompt.
        /// </summary>
        /// <param name="path">The path to the prompt.</param>
        /// <returns>A task that returns a PromptCompose instance.</returns>
        public async Task<PromptCompose> GetPromptComposerAsync(string path)
        {
            var store = await GetStoreAsync();

            // Legacy path parsing logic
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            string promptName;

            if (parts.Length == 1)
            {
                promptName = parts[0];
            }
            else if (parts.Length == 2)
            {
                var set = parts[0];
                var name = parts[1];
                promptName = $"{set}_{name}";
            }
            else if (parts.Length == 3)
            {
                var set = parts[0];
                var subset = parts[1];
                var name = parts[2];
                promptName = $"{set}_{subset}_{name}";
            }
            else
            {
                throw new InvalidOperationException("Path must be in the format 'set', 'set/subset', or 'set/subset/prompt'.");
            }

            try
            {
                return await store.GetAsync(promptName);
            }
            catch (KeyNotFoundException)
            {
                // If the prompt doesn't exist in the new store, create a temporary one from the legacy context
                var legacyPrompt = _legacyContext.Get(path).AsString();
                var tempDef = new PromptDefinition
                {
                    Name = promptName,
                    Description = $"Temporary definition for legacy prompt: {path}"
                };

                return new PromptCompose(tempDef)
                    .WithArgument("text", legacyPrompt);
            }
        }

        /// <summary>
        /// Composes a prompt from the legacy context.
        /// </summary>
        /// <param name="path">The path to the prompt.</param>
        /// <param name="separator">The separator to use when combining prompts.</param>
        /// <returns>A task that returns a GetPromptResult.</returns>
        public Task<GetPromptResult> ComposePromptAsync(string path, string? separator = null)
        {
            // Get the legacy prompt text
            string promptText;
            
            if (path.Contains("/"))
            {
                var legacyContext = _legacyContext.Get(path);
                if (separator != null)
                {
                    legacyContext.SeparateWith(separator);
                }
                promptText = legacyContext.CombineWithRoot().AsString();
            }
            else
            {
                promptText = _legacyContext.Get(path).AsString();
            }

            // Create a temporary PromptDefinition
            var tempDef = new PromptDefinition
            {
                Name = path.Replace("/", "_"),
                Description = $"Converted from legacy prompt: {path}"
            };

            // Compose the prompt
            var result = new PromptCompose(tempDef)
                .WithArgument("text", promptText)
                .Compose();
                
            return Task.FromResult(result);
        }
    }
}