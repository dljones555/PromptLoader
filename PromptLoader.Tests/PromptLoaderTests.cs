using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using PromptLoader.Services;

namespace PromptLoader.Tests;
public class PromptLoaderTests : IDisposable
{
    private readonly string _testDir;

    public PromptLoaderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
    }

    [Fact]
    public void LoadPrompts_LoadsSupportedFiles()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDir, "a.prompt"), "Prompt A");
        File.WriteAllText(Path.Combine(_testDir, "b.txt"), "Prompt B");
        File.WriteAllText(Path.Combine(_testDir, "c.unsupported"), "Should be ignored");

        // Act
        var prompts = PromptLoader.Services.PromptLoader.LoadPrompts(_testDir);

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
        var prompts = PromptLoader.Services.PromptLoader.LoadPrompts(_testDir, cascadeOverride: true);

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
        var prompts = PromptLoader.Services.PromptLoader.LoadPrompts(_testDir, cascadeOverride: false);

        // Assert
        Assert.Equal("Root", prompts["e"].Text);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }
}
