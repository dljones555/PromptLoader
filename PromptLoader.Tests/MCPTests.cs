using Microsoft.Extensions.Configuration;
using PromptLoader.Core;
using PromptLoader.Models.MCP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PromptLoader.Tests
{
    public class MCPTests : IAsyncLifetime
    {
        private readonly string _testDir;
        private readonly IConfiguration _config;

        public MCPTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "PromptLoaderTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDir);

            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("PromptsFolder", _testDir),
                new KeyValuePair<string, string?>("PromptSetFolder", _testDir),
                new KeyValuePair<string, string?>("ConstrainPromptList", "false")
            });
            _config = configBuilder.Build();
        }

        [Fact]
        public async Task PromptRoot_LoadsYamlPrompt()
        {
            // Arrange
            var promptYaml = @"
name: test-prompt
description: A test prompt
arguments:
  - name: input
    description: The input text
    required: true
model: gpt-4
";
            await File.WriteAllTextAsync(Path.Combine(_testDir, "test-prompt.yml"), promptYaml);

            // Act
            var root = PromptRoot.FromFile(_testDir);
            var prompts = await root.LoadAsync();
            var promptsList = prompts.ToList();

            // Assert
            Assert.Single(promptsList);
            Assert.Equal("test-prompt", promptsList[0].Name);
            Assert.Equal("A test prompt", promptsList[0].Description);
            Assert.NotNull(promptsList[0].Arguments);
            Assert.Single(promptsList[0].Arguments!);
            Assert.Equal("input", promptsList[0].Arguments![0].Name);
            Assert.True(promptsList[0].Arguments![0].Required);
            Assert.Equal("gpt-4", promptsList[0].Model);
        }

        [Fact]
        public async Task PromptStore_GetsPrompt()
        {
            // Arrange
            var promptYaml = @"
name: summarize-error
description: Summarizes an error log
arguments:
  - name: log
    description: The error log to summarize
    required: true
model: gpt-4
";
            await File.WriteAllTextAsync(Path.Combine(_testDir, "summarize-error.yml"), promptYaml);

            var root = PromptRoot.FromFile(_testDir);
            var store = new PromptStore(root);
            await store.InitializeAsync();

            // Act
            var prompt = await store.GetAsync("summarize-error");
            var result = prompt
                .WithArgument("log", "Error: Connection timeout in network.py:127")
                .Compose();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Summarizes an error log", result.Description);
            Assert.NotEmpty(result.Messages);
        }

        [Fact]
        public async Task PromptKit_UsesFluentApi()
        {
            // Arrange
            var promptYaml = @"
name: summarize-error
description: Summarizes an error log
arguments:
  - name: log
    description: The error log to summarize
    required: true
model: gpt-4
";
            await File.WriteAllTextAsync(Path.Combine(_testDir, "summarize-error.yml"), promptYaml);

            // Act
            var result = await PromptKit
                .UseRoot(_testDir)
                .Prompt("summarize-error")
                .WithInput("log", "Error: Connection timeout in network.py:127")
                .InLanguage("en-US")
                .RunAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Summarizes an error log", result.Description);
            Assert.NotEmpty(result.Messages);
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            });
        }
    }
}