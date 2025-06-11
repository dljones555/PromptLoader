using Microsoft.Extensions.Configuration;
using PromptLoader.Models;
using PromptLoader.Fluent;
using Xunit;

namespace PromptLoader.Tests;

public class PromptSetLoaderTests
{
    private readonly IConfiguration _config;
    private readonly IPromptContext _promptContext;

    public PromptSetLoaderTests()
    {
        _config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appSettings.json", optional: true, reloadOnChange: false)
            .Build();
        _promptContext = new PromptContext().WithConfig(_config);
    }

    [Fact]
    public async Task LoadPromptSets_ReturnsCorrectPromptSets()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempRoot);

        var setDir = Path.Combine(tempRoot, "SetA");
        Directory.CreateDirectory(setDir);
        await File.WriteAllTextAsync(Path.Combine(setDir, "a.prompt"), "Prompt A");

        // Inject test folder into config
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("PromptSetFolder", tempRoot),
            new KeyValuePair<string, string?>("SupportedPromptExtensions:0", ".prompt")
        });
        var testConfig = configBuilder.Build();
        var promptContext = new PromptContext().WithConfig(testConfig);

        // Act
        var sets = await promptContext.LoadPromptSetsAsync(false, null);

        // Assert
        Assert.Single(sets); // Only SetA
        Assert.Contains("SetA", sets.Keys);
        var setA = sets["SetA"];
        Assert.Single(setA); // Only Root
        Assert.Contains("Root", setA.Keys);
        Assert.Equal("Root", setA["Root"].Name);
        Assert.Contains("a", setA["Root"].Prompts.Keys);
        Assert.Equal("Prompt A", setA["Root"].Prompts["a"].Text);

        // Cleanup
        Directory.Delete(tempRoot, true);
    }

    [Fact]
    public async Task LoadPromptSets_HandlesNestedPromptSets()
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
        await File.WriteAllTextAsync(Path.Combine(refundDir, "refund.prompt"), "Refund prompt");
        await File.WriteAllTextAsync(Path.Combine(policyDir, "policy.prompt"), "Policy prompt");
        await File.WriteAllTextAsync(Path.Combine(salesDir, "examples.prompt"), "Sales example");
        await File.WriteAllTextAsync(Path.Combine(salesDir, "instructions.prompt"), "Sales instructions");

        // Inject test folder into config
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("PromptSetFolder", tempRoot),
            new KeyValuePair<string, string?>("SupportedPromptExtensions:0", ".prompt")
        });
        var testConfig = configBuilder.Build();
        var promptContext = new PromptContext().WithConfig(testConfig);

        // Act
        var sets = await promptContext.LoadPromptSetsAsync(false, null);

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
        Assert.Contains("Root", sales.Keys); // Prompts directly in Sales
        Assert.Contains("examples", sales["Root"].Prompts.Keys);
        Assert.Contains("instructions", sales["Root"].Prompts.Keys);
        Assert.Equal("Sales example", sales["Root"].Prompts["examples"].Text);
        Assert.Equal("Sales instructions", sales["Root"].Prompts["instructions"].Text);

        // Cleanup
        Directory.Delete(tempRoot, true);
    }

    [Fact]
    public async Task LoadPromptSets_LoadsMdFilesInRootPromptSet()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempRoot);
        var salesDir = Path.Combine(tempRoot, "Sales");
        Directory.CreateDirectory(salesDir);
        await File.WriteAllTextAsync(Path.Combine(salesDir, "examples.md"), "Sales example");
        await File.WriteAllTextAsync(Path.Combine(salesDir, "instructions.md"), "Sales instructions");

        // Inject test folder into config
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("PromptSetFolder", tempRoot),
            new KeyValuePair<string, string?>("SupportedPromptExtensions:0", ".md")
        });
        var testConfig = configBuilder.Build();
        var promptContext = new PromptContext().WithConfig(testConfig);

        // Act
        var sets = await promptContext.LoadPromptSetsAsync(false, null);

        // Assert
        Assert.Contains("Sales", sets.Keys);
        var sales = sets["Sales"];
        Assert.Contains("Root", sales.Keys);
        Assert.Contains("examples", sales["Root"].Prompts.Keys);
        Assert.Contains("instructions", sales["Root"].Prompts.Keys);
        Assert.Equal("Sales example", sales["Root"].Prompts["examples"].Text);
        Assert.Equal("Sales instructions", sales["Root"].Prompts["instructions"].Text);

        // Cleanup
        Directory.Delete(tempRoot, true);
    }

    [Fact]
    public async Task LoadPromptSets_LoadsFromCustomFolder()
    {
        // Arrange
        var customDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(customDir);
        var setDir = Path.Combine(customDir, "CustomSet");
        Directory.CreateDirectory(setDir);
        await File.WriteAllTextAsync(Path.Combine(setDir, "a.prompt"), "Prompt A");
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("SupportedPromptExtensions:0", ".prompt")
        });
        var config = configBuilder.Build();
        var promptContext = new PromptContext().WithConfig(config);

        // Act
        var sets = await promptContext.LoadPromptSetsAsync(false, customDir);

        // Assert
        Assert.Single(sets); // Only CustomSet
        Assert.Contains("CustomSet", sets.Keys);
        var setA = sets["CustomSet"];
        Assert.Single(setA); // Only Root
        Assert.Contains("Root", setA.Keys);
        Assert.Contains("a", setA["Root"].Prompts.Keys);
        Assert.Equal("Prompt A", setA["Root"].Prompts["a"].Text);

        // Cleanup
        Directory.Delete(customDir, true);
    }

    [Fact]
    public async Task LoadPromptSets_NonExistentFolder_HandledGracefully()
    {
        // Arrange
        var nonExistentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("SupportedPromptExtensions:0", ".prompt")
        });
        var config = configBuilder.Build();
        var promptContext = new PromptContext().WithConfig(config);

        // Act & Assert
        var ex = await Record.ExceptionAsync(() => promptContext.LoadPromptSetsAsync(false, nonExistentDir));
        Assert.Null(ex); // Should not throw
        var sets = await promptContext.LoadPromptSetsAsync(false, nonExistentDir);
        Assert.Empty(sets);
    }

    [Fact]
    public void GetCombinedPrompts_ThrowsIfSetNotFound()
    {
        // Arrange
        var sets = new Dictionary<string, PromptSet>();
        var config = new ConfigurationBuilder().Build();
        var promptContext = new PromptContext().WithConfig(config);

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() =>
            promptContext.GetCombinedPrompts(sets, "missing"));
    }

    [Fact]
    public void GetCombinedPrompts_UsesPromptListFromConfig()
    {
        // Arrange
        var prompts = new Dictionary<string, Prompt>
        {
            { "system", new Prompt("system", PromptFormat.Plain) },
            { "instructions", new Prompt("instructions", PromptFormat.Plain) }
        };
        var set = new PromptSet { Name = "Test", Prompts = prompts };
        var sets = new Dictionary<string, PromptSet> { { "set1", set } };

        // Provide PromptList in config
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("PromptList:0", "system"),
            new KeyValuePair<string, string?>("PromptList:1", "instructions")
        });
        var config = configBuilder.Build();
        var promptContext = new PromptContext().WithConfig(config);

        // Act
        var result = promptContext.GetCombinedPrompts(sets, "set1");

        // Assert
        Assert.Equal("system" + Environment.NewLine + "instructions", result);
    }

    [Fact]
    public void GetCombinedPrompts_FallsBackToDefaultOrder()
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
        var promptContext = new PromptContext().WithConfig(config);

        // Act
        var result = promptContext.GetCombinedPrompts(sets, "set1");

        // Assert
        var expected1 = "First" + Environment.NewLine + "Second";
        var expected2 = "Second" + Environment.NewLine + "First";
        Assert.Contains(result, new[] { expected1, expected2 });
    }

    [Fact]
    public void GetCombinedPrompts_Overload_JoinsPromptSetCorrectly()
    {
        // Arrange
        var prompts = new Dictionary<string, Prompt>
        {
            { "system", new Prompt("System text", PromptFormat.Plain) },
            { "instructions", new Prompt("Instructions text", PromptFormat.Plain) },
            { "examples", new Prompt("Examples text", PromptFormat.Plain) }
        };
        var promptSet = new PromptSet { Name = "Main", Prompts = prompts };

        // Provide PromptList in config
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("PromptList:0", "system"),
            new KeyValuePair<string, string?>("PromptList:1", "instructions"),
            new KeyValuePair<string, string?>("PromptList:2", "examples")
        });
        var config = configBuilder.Build();
        var promptContext = new PromptContext().WithConfig(config);

        // Act
        var result = promptContext.GetCombinedPrompts(promptSet);

        // Assert
        Assert.Equal("System text\nInstructions text\nExamples text".Replace("\n", Environment.NewLine), result);
    }

    [Fact]
    public async Task GetCombinedPrompts_UsesRootPromptIfMissingInSubdir()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllTextAsync(Path.Combine(tempRoot, "system.md"), "Root System");
        var salesDir = Path.Combine(tempRoot, "Sales");
        Directory.CreateDirectory(salesDir);
        await File.WriteAllTextAsync(Path.Combine(salesDir, "instructions.md"), "Sales Instructions");

        // Inject test folder and PromptList into config
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("PromptSetFolder", tempRoot),
            new KeyValuePair<string, string?>("SupportedPromptExtensions:0", ".md"),
            new KeyValuePair<string, string?>("PromptList:0", "system"),
            new KeyValuePair<string, string?>("PromptList:1", "instructions")
        });
        var testConfig = configBuilder.Build();
        var promptContext = new PromptContext().WithConfig(testConfig);

        // Act
        var sets = await promptContext.LoadPromptSetsAsync(false, null);
        var salesSet = sets["Sales"]["Root"];
        var rootSet = sets["Root"]["Root"];
        var combined = promptContext.GetCombinedPrompts(salesSet, rootSet);

        // Assert
        Assert.Contains("Root System", combined);
        Assert.Contains("Sales Instructions", combined);
        // system should come before instructions
        var idxSystem = combined.IndexOf("Root System");
        var idxInstructions = combined.IndexOf("Sales Instructions");
        Assert.True(idxSystem < idxInstructions);

        // Cleanup
        Directory.Delete(tempRoot, true);
    }

    [Fact]
    public async Task GetCombinedPrompts_SalesSet_InheritsSystemFromRoot()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllTextAsync(Path.Combine(tempRoot, "system.md"), "Root System");
        var salesDir = Path.Combine(tempRoot, "Sales");
        Directory.CreateDirectory(salesDir);
        await File.WriteAllTextAsync(Path.Combine(salesDir, "examples.md"), "Sales Example");
        await File.WriteAllTextAsync(Path.Combine(salesDir, "instructions.md"), "Sales Instructions");

        // Inject test folder and PromptList into config
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("PromptSetFolder", tempRoot),
            new KeyValuePair<string, string?>("SupportedPromptExtensions:0", ".md"),
            new KeyValuePair<string, string?>("PromptList:0", "system"),
            new KeyValuePair<string, string?>("PromptList:1", "examples"),
            new KeyValuePair<string, string?>("PromptList:2", "instructions")
        });
        var testConfig = configBuilder.Build();
        var promptContext = new PromptContext().WithConfig(testConfig);

        // Act
        var sets = await promptContext.LoadPromptSetsAsync(false, null);
        var salesSet = sets["Sales"]["Root"];
        var rootSet = sets["Root"]["Root"];
        var combined = promptContext.GetCombinedPrompts(salesSet, rootSet);

        // Assert
        Assert.Contains("Root System", combined);
        Assert.Contains("Sales Example", combined);
        Assert.Contains("Sales Instructions", combined);
        // Check order
        var idxSystem = combined.IndexOf("Root System");
        var idxExamples = combined.IndexOf("Sales Example");
        var idxInstructions = combined.IndexOf("Sales Instructions");
        Assert.True(idxSystem < idxExamples && idxExamples < idxInstructions);

        // Cleanup
        Directory.Delete(tempRoot, true);
    }

    [Fact]
    public async Task LoadPromptSets_ConstrainPromptList_OnlyLoadsPromptListFiles()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempRoot);
        var setDir = Path.Combine(tempRoot, "SetA");
        Directory.CreateDirectory(setDir);
        await File.WriteAllTextAsync(Path.Combine(setDir, "system.prompt"), "System Prompt");
        await File.WriteAllTextAsync(Path.Combine(setDir, "instructions.prompt"), "Instructions Prompt");
        await File.WriteAllTextAsync(Path.Combine(setDir, "other.prompt"), "Other Prompt");
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("PromptLoader:PromptSetFolder", tempRoot),
            new KeyValuePair<string, string?>("PromptLoader:SupportedPromptExtensions:0", ".prompt"),
            new KeyValuePair<string, string?>("PromptLoader:PromptList:0", "system"),
            new KeyValuePair<string, string?>("PromptLoader:PromptList:1", "instructions"),
            new KeyValuePair<string, string?>("PromptLoader:ConstrainPromptList", "true")
        });
        var config = configBuilder.Build();
        var promptContext = new PromptContext().WithConfig(config);

        // Act
        var sets = await promptContext.LoadPromptSetsAsync(false, null);
        var setA = sets["SetA"]["Root"];

        // Assert
        Assert.Equal(2, setA.Prompts.Count);
        Assert.Contains("system", setA.Prompts.Keys);
        Assert.Contains("instructions", setA.Prompts.Keys);
        Assert.DoesNotContain("other", setA.Prompts.Keys);

        // Cleanup
        Directory.Delete(tempRoot, true);
    }

    [Fact]
    public async Task LoadPromptSets_ConstrainPromptList_False_LoadsAllSupportedFiles()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempRoot);
        var setDir = Path.Combine(tempRoot, "SetA");
        Directory.CreateDirectory(setDir);
        await File.WriteAllTextAsync(Path.Combine(setDir, "system.prompt"), "System Prompt");
        await File.WriteAllTextAsync(Path.Combine(setDir, "instructions.prompt"), "Instructions Prompt");
        await File.WriteAllTextAsync(Path.Combine(setDir, "other.prompt"), "Other Prompt");
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("PromptSetFolder", tempRoot),
            new KeyValuePair<string, string?>("SupportedPromptExtensions:0", ".prompt"),
            new KeyValuePair<string, string?>("PromptList:0", "system"),
            new KeyValuePair<string, string?>("PromptList:1", "instructions"),
            new KeyValuePair<string, string?>("ConstrainPromptList", "false")
        });
        var config = configBuilder.Build();
        var promptContext = new PromptContext().WithConfig(config);

        // Act
        var sets = await promptContext.LoadPromptSetsAsync(false, null);
        var setA = sets["SetA"]["Root"];

        // Assert
        Assert.Equal(3, setA.Prompts.Count);
        Assert.Contains("system", setA.Prompts.Keys);
        Assert.Contains("instructions", setA.Prompts.Keys);
        Assert.Contains("other", setA.Prompts.Keys);

        // Cleanup
        Directory.Delete(tempRoot, true);
    }

    [Fact]
    public void GetCombinedPrompts_PrependsFilenameHeaderOnFirstEntry()
    {
        var prompts = new Dictionary<string, Prompt>
        {
            { "system", new Prompt("You are a helpful agent.", PromptFormat.Plain) },
            { "instructions", new Prompt("Follow the user’s request.", PromptFormat.Plain) }
        };
        var set = new PromptSet { Name = "Test", Prompts = prompts };
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string>("PromptList:0", "system"),
            new KeyValuePair<string, string>("PromptList:1", "instructions"),
            new KeyValuePair<string, string>("PromptSeparator", "\n  {filename}:  \n")
        });
        var config = configBuilder.Build();
        var promptContext = new PromptContext().WithConfig(config);

        var result = promptContext.GetCombinedPrompts(set);

        // Should start with the header for the first prompt, trimmed
        Assert.StartsWith("System:  \nYou are a helpful agent.", result);
        // Should contain the header for the second prompt, trimmed
        Assert.Contains("\n  Instructions:  \nFollow the user’s request.", result);
        // Should not have extra whitespace in the header
        Assert.DoesNotContain("  System:  ", result);
    }
}
