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
        File.WriteAllText(Path.Combine(setDir, "a.prompt"), "Prompt A");

        // Act
        var sets = PromptSetLoader.LoadPromptSets(tempRoot);

        // Assert
        Assert.Single(sets); // Only SetA
        Assert.Contains("SetA", sets.Keys);
        var setA = sets["SetA"];
        Assert.Single(setA); // Only Main
        Assert.Contains("Main", setA.Keys);
        Assert.Equal("Main", setA["Main"].Name);
        Assert.Contains("a", setA["Main"].Prompts.Keys);
        Assert.Equal("Prompt A", setA["Main"].Prompts["a"].Text);

        // Cleanup
        Directory.Delete(tempRoot, true);
    }

    [Fact]
    public void LoadPromptSets_HandlesNestedPromptSets()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempRoot);

        var customerServiceDir = Path.Combine(tempRoot, "CustomerService");
        var refundDir = Path.Combine(customerServiceDir, "Refund");
        var policyDir = Path.Combine(customerServiceDir, "Policy");
        var salesDir = Path.Combine(tempRoot, "Sales");
        Directory.CreateDirectory(refundDir);
        Directory.CreateDirectory(policyDir);
        Directory.CreateDirectory(salesDir);

        // Create dummy prompt files with supported extensions
        File.WriteAllText(Path.Combine(refundDir, "refund.prompt"), "Refund prompt");
        File.WriteAllText(Path.Combine(policyDir, "policy.prompt"), "Policy prompt");
        File.WriteAllText(Path.Combine(salesDir, "examples.prompt"), "Sales example");
        File.WriteAllText(Path.Combine(salesDir, "instructions.prompt"), "Sales instructions");

        // Act
        var sets = PromptSetLoader.LoadPromptSets(tempRoot);

        // Assert
        Assert.Contains("CustomerService", sets.Keys);
        Assert.Contains("Sales", sets.Keys);

        // CustomerService
        var cs = sets["CustomerService"];
        Assert.Contains("Refund", cs.Keys);
        Assert.Contains("Policy", cs.Keys);
        Assert.Equal("Refund", cs["Refund"].Name);
        Assert.Equal("Policy", cs["Policy"].Name);
        Assert.Contains("refund", cs["Refund"].Prompts.Keys);
        Assert.Contains("policy", cs["Policy"].Prompts.Keys);
        Assert.Equal("Refund prompt", cs["Refund"].Prompts["refund"].Text);
        Assert.Equal("Policy prompt", cs["Policy"].Prompts["policy"].Text);

        // Sales
        var sales = sets["Sales"];
        Assert.Contains("Main", sales.Keys); // Prompts directly in Sales
        Assert.Contains("examples", sales["Main"].Prompts.Keys);
        Assert.Contains("instructions", sales["Main"].Prompts.Keys);
        Assert.Equal("Sales example", sales["Main"].Prompts["examples"].Text);
        Assert.Equal("Sales instructions", sales["Main"].Prompts["instructions"].Text);

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
            { "system", new Prompt("system", PromptFormat.Plain) },
            { "instructions", new Prompt("instructions", PromptFormat.Plain) }
        };
        var set = new PromptSet { Name = "Test", Prompts = prompts };
        var sets = new Dictionary<string, PromptSet> { { "set1", set } };

        // Provide PromptOrder in config
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string>("PromptOrder:0", "system"),
            new KeyValuePair<string, string>("PromptOrder:1", "instructions")
        });
        var config = configBuilder.Build();

        // Act
        var result = PromptSetLoader.JoinPrompts(sets, "set1", config);

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
