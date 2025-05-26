using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using PromptLoader.Services;
using Microsoft.Extensions.Configuration;

namespace PromptLoader.Tests;
public class PromptLoaderTests : IDisposable
{
    private readonly string _testDir;
    private readonly IPromptService _promptService;
    private readonly IConfiguration _config;

    public PromptLoaderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string>("PromptsFolder", _testDir),
            new KeyValuePair<string, string>("SupportedPromptExtensions:0", ".txt"),
            new KeyValuePair<string, string>("SupportedPromptExtensions:1", ".prompt")
        });
        _config = configBuilder.Build();
        _promptService = new PromptService(_config);
    }

    [Fact]
    public void LoadPrompts_LoadsSupportedFiles()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDir, "a.prompt"), "Prompt A");
        File.WriteAllText(Path.Combine(_testDir, "b.txt"), "Prompt B");
        File.WriteAllText(Path.Combine(_testDir, "c.unsupported"), "Should be ignored");

        // Act
        var prompts = _promptService.LoadPrompts();

        // Assert
        Assert.NotNull(prompts);
        Assert.Equal(2, prompts.Count);
        Assert.Contains("a", prompts.Keys);
        Assert.Contains("b", prompts.Keys);
    }

    [Fact]
    public void LoadPrompts_CascadeOverride_True_OverridesDeeperPrompts()
    {
        // Arrange
        var subDir = Path.Combine(_testDir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(_testDir, "d.prompt"), "Root");
        File.WriteAllText(Path.Combine(subDir, "d.prompt"), "Sub");

        // Act
        var prompts = _promptService.LoadPrompts(cascadeOverride: true);

        // Assert
        Assert.Equal("Sub", prompts["d"].Text);
    }

    [Fact]
    public void LoadPrompts_CascadeOverride_False_KeepsFirstFound()
    {
        // Arrange
        var subDir = Path.Combine(_testDir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(_testDir, "e.prompt"), "Root");
        File.WriteAllText(Path.Combine(subDir, "e.prompt"), "Sub");

        // Act
        var prompts = _promptService.LoadPrompts(cascadeOverride: false);

        // Assert
        Assert.Equal("Root", prompts["e"].Text);
    }

    [Fact]
    public void LoadPrompts_LoadsFromCustomFolder()
    {
        // Arrange
        var customDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(customDir);
        File.WriteAllText(Path.Combine(customDir, "custom.prompt"), "Custom Prompt");
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string>("SupportedPromptExtensions:0", ".prompt")
        });
        var config = configBuilder.Build();
        var promptService = new PromptService(config);

        // Act
        var prompts = promptService.LoadPrompts(promptsFolder: customDir);

        // Assert
        Assert.Single(prompts);
        Assert.Contains("custom", prompts.Keys);
        Assert.Equal("Custom Prompt", prompts["custom"].Text);

        // Cleanup
        Directory.Delete(customDir, true);
    }

    [Fact]
    public void LoadPrompts_NonExistentFolder_HandledGracefully()
    {
        // Arrange
        var nonExistentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string>("SupportedPromptExtensions:0", ".prompt")
        });
        var config = configBuilder.Build();
        var promptService = new PromptService(config);

        // Act & Assert
        var ex = Record.Exception(() => promptService.LoadPrompts(promptsFolder: nonExistentDir));
        Assert.Null(ex); // Should not throw
        var prompts = promptService.LoadPrompts(promptsFolder: nonExistentDir);
        Assert.Empty(prompts);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }
}
