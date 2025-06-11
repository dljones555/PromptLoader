using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using PromptLoader.Fluent;
using PromptLoader.Models;
using Microsoft.Extensions.Configuration;

namespace PromptLoader.Tests;
public class PromptLoaderTests : IAsyncLifetime, IDisposable
{
    private readonly string _testDir;
    private readonly IPromptContext _promptContext;
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
        _promptContext = new PromptContext().WithConfig(_config);
    }

    [Fact]
    public async Task LoadPrompts_LoadsSupportedFiles()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testDir, "a.prompt"), "Prompt A");
        await File.WriteAllTextAsync(Path.Combine(_testDir, "b.txt"), "Prompt B");
        await File.WriteAllTextAsync(Path.Combine(_testDir, "c.unsupported"), "Should be ignored");

        // Act
        var prompts = await _promptContext.LoadPromptsAsync();

        // Assert
        Assert.NotNull(prompts);
        Assert.Equal(2, prompts.Count);
        Assert.Contains("a", prompts.Keys);
        Assert.Contains("b", prompts.Keys);
    }

    [Fact]
    public async Task LoadPrompts_CascadeOverride_True_OverridesDeeperPrompts()
    {
        // Arrange
        var subDir = Path.Combine(_testDir, "sub");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(_testDir, "d.prompt"), "Root");
        await File.WriteAllTextAsync(Path.Combine(subDir, "d.prompt"), "Sub");

        // Act
        var prompts = await _promptContext.LoadPromptsAsync(cascadeOverride: true);

        // Assert
        Assert.Equal("Sub", prompts["d"].Text);
    }

    [Fact]
    public async Task LoadPrompts_CascadeOverride_False_KeepsFirstFound()
    {
        // Arrange
        var subDir = Path.Combine(_testDir, "sub");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(_testDir, "e.prompt"), "Root");
        await File.WriteAllTextAsync(Path.Combine(subDir, "e.prompt"), "Sub");

        // Act
        var prompts = await _promptContext.LoadPromptsAsync(cascadeOverride: false);

        // Assert
        Assert.Equal("Root", prompts["e"].Text);
    }

    [Fact]
    public async Task LoadPrompts_LoadsFromCustomFolder()
    {
        // Arrange
        var customDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(customDir);
        await File.WriteAllTextAsync(Path.Combine(customDir, "custom.prompt"), "Custom Prompt");
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string>("SupportedPromptExtensions:0", ".prompt")
        });
        var config = configBuilder.Build();
        var promptContext = new PromptContext().WithConfig(config);

        // Act
        var prompts = await promptContext.LoadPromptsAsync(promptsFolder: customDir);

        // Assert
        Assert.Single(prompts);
        Assert.Contains("custom", prompts.Keys);
        Assert.Equal("Custom Prompt", prompts["custom"].Text);

        // Cleanup
        Directory.Delete(customDir, true);
    }

    [Fact]
    public async Task LoadPrompts_NonExistentFolder_HandledGracefully()
    {
        // Arrange
        var nonExistentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string>("SupportedPromptExtensions:0", ".prompt")
        });
        var config = configBuilder.Build();
        var promptContext = new PromptContext().WithConfig(config);

        // Act & Assert
        var ex = await Record.ExceptionAsync(() => promptContext.LoadPromptsAsync(promptsFolder: nonExistentDir));
        Assert.Null(ex); // Should not throw
        var prompts = await promptContext.LoadPromptsAsync(promptsFolder: nonExistentDir);
        Assert.Empty(prompts);
    }

    [Fact]
    public async Task LoadPrompt_LoadsSinglePromptFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDir, "single.prompt");
        await File.WriteAllTextAsync(filePath, "Single Prompt Content");

        // Act
        var prompt = await _promptContext.LoadPromptAsync(filePath);

        // Assert
        Assert.NotNull(prompt);
        Assert.Equal("Single Prompt Content", prompt.Text);
        Assert.Equal(PromptFormat.Plain, prompt.Format);
    }

    [Fact]
    public async Task LoadPrompt_ReturnsNullForUnsupportedOrMissingFile()
    {
        // Arrange
        var unsupportedFile = Path.Combine(_testDir, "unsupported.unsupported");
        var missingFile = Path.Combine(_testDir, "missing.prompt");
        await File.WriteAllTextAsync(unsupportedFile, "Should be ignored");

        // Act
        var prompt1 = await _promptContext.LoadPromptAsync(unsupportedFile);
        var prompt2 = await _promptContext.LoadPromptAsync(missingFile);

        // Assert
        Assert.Null(prompt1);
        Assert.Null(prompt2);
    }

    [Fact]
    public async Task LoadPrompts_ConstrainPromptList_OnlyLoadsPromptListFiles()
    {
        // Arrange
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string>("PromptLoader:PromptsFolder", _testDir),
            new KeyValuePair<string, string>("PromptLoader:SupportedPromptExtensions:0", ".prompt"),
            new KeyValuePair<string, string>("PromptLoader:PromptList:0", "system"),
            new KeyValuePair<string, string>("PromptLoader:PromptList:1", "instructions"),
            new KeyValuePair<string, string>("PromptLoader:ConstrainPromptList", "true")
        });
        var config = configBuilder.Build();
        var promptContext = new PromptContext().WithConfig(config);
        await File.WriteAllTextAsync(Path.Combine(_testDir, "system.prompt"), "System Prompt");
        await File.WriteAllTextAsync(Path.Combine(_testDir, "instructions.prompt"), "Instructions Prompt");
        await File.WriteAllTextAsync(Path.Combine(_testDir, "other.prompt"), "Other Prompt");

        // Act
        var prompts = await promptContext.LoadPromptsAsync();

        // Assert
        Assert.Equal(2, prompts.Count);
        Assert.Contains("system", prompts.Keys);
        Assert.Contains("instructions", prompts.Keys);
        Assert.DoesNotContain("other", prompts.Keys);
    }

    [Fact]
    public async Task LoadPrompts_ConstrainPromptList_False_LoadsAllSupportedFiles()
    {
        // Arrange
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string>("PromptsFolder", _testDir),
            new KeyValuePair<string, string>("SupportedPromptExtensions:0", ".prompt"),
            new KeyValuePair<string, string>("PromptList:0", "system"),
            new KeyValuePair<string, string>("PromptList:1", "instructions"),
            new KeyValuePair<string, string>("ConstrainPromptList", "false")
        });
        var config = configBuilder.Build();
        var promptContext = new PromptContext().WithConfig(config);
        await File.WriteAllTextAsync(Path.Combine(_testDir, "system.prompt"), "System Prompt");
        await File.WriteAllTextAsync(Path.Combine(_testDir, "instructions.prompt"), "Instructions Prompt");
        await File.WriteAllTextAsync(Path.Combine(_testDir, "other.prompt"), "Other Prompt");

        // Act
        var prompts = await promptContext.LoadPromptsAsync();

        // Assert
        Assert.Equal(3, prompts.Count);
        Assert.Contains("system", prompts.Keys);
        Assert.Contains("instructions", prompts.Keys);
        Assert.Contains("other", prompts.Keys);
    }

    public async Task InitializeAsync() => await Task.CompletedTask;

    public async Task DisposeAsync() => await Task.Run(() =>
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    });

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }
}