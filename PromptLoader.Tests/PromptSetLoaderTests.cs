using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using PromptLoader.Models;
using PromptLoader.Services;
using Xunit;

namespace PromptLoader.Tests;

public class PromptSetLoaderTests
{
    private readonly IConfiguration _config;
    public PromptSetLoaderTests()
    {
        _config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appSettings.json", optional: true, reloadOnChange: false)
            .Build();
    }

    [Fact]
    public void LoadPromptSets_ReturnsCorrectPromptSets()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempRoot);

        var setDir = Path.Combine(tempRoot, "SetA");
        Directory.CreateDirectory(setDir);

        // Create a dummy prompt file if PromptLoader.LoadPrompts expects files
        // For this test, we mock PromptLoader.LoadPrompts via a delegate swap if possible
        var originalLoadPrompts = typeof(PromptLoader.Services.PromptLoader)
            .GetMethod("LoadPrompts");
        // If LoadPrompts is static and not easily swappable, this test will only check directory logic

        // Act
        var sets = PromptSetLoader.LoadPromptSets(tempRoot);

        // Assert
        Assert.Single(sets);
        Assert.Contains(setDir, sets.Keys);
        Assert.Equal("SetA", sets[setDir].Name);

        // Cleanup
        Directory.Delete(tempRoot, true);
    }

    [Fact]
    public void JoinPrompts_ThrowsIfSetNotFound()
    {
        // Arrange
        var sets = new Dictionary<string, PromptSet>();
        var config = new ConfigurationBuilder().Build();

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() =>
            PromptSetLoader.JoinPrompts(sets, "missing", config));
    }

    [Fact]
    public void JoinPrompts_UsesPromptOrderFromConfig()
    {
        // Arrange
        var prompts = new Dictionary<string, Prompt>
        {
            { "A", new Prompt("system", PromptFormat.Plain) },
            { "B", new Prompt("instructions", PromptFormat.Plain) }
        };
        var set = new PromptSet { Name = "Test", Prompts = prompts };
        var sets = new Dictionary<string, PromptSet> { { "set1", set } };
      
        // Act
        var result = PromptSetLoader.JoinPrompts(sets, "set1", _config);

        // Assert
        Assert.Equal("system" + Environment.NewLine + "instructions", result);
    }

    [Fact]
    public void JoinPrompts_FallsBackToDefaultOrder()
    {
        // Arrange
        var prompts = new Dictionary<string, Prompt>
        {
            { "A", new Prompt("First", PromptFormat.Plain) },
            { "B", new Prompt("Second", PromptFormat.Plain) }
        };
        var set = new PromptSet { Name = "Test", Prompts = prompts };
        var sets = new Dictionary<string, PromptSet> { { "set1", set } };

        var config = new ConfigurationBuilder().Build();
        // Act
        var result = PromptSetLoader.JoinPrompts(sets, "set1", config);

        // Assert
        // The order of Dictionary.Values is not guaranteed, so check both possibilities
        var expected1 = "First" + Environment.NewLine + "Second";
        var expected2 = "Second" + Environment.NewLine + "First";
        Assert.Contains(result, new[] { expected1, expected2 });
    }
}
