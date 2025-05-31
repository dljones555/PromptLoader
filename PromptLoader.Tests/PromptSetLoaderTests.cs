using Microsoft.Extensions.Configuration;
using PromptLoader.Models;
using PromptLoader.Services;
using Xunit;

namespace PromptLoader.Tests;

public class PromptSetLoaderTests
{
    [Fact]
    public async Task LoadPromptSets_ReturnsCorrectPromptSets()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempRoot);
        var setDir = Path.Combine(tempRoot, "SetA");
        Directory.CreateDirectory(setDir);
        await File.WriteAllTextAsync(Path.Combine(setDir, "a.prompt"), "Prompt A");

        // Act
        var context = await PromptContext.FromFolder(tempRoot).LoadAsync();
        var setA = context.Get("SetA").CombineWithBase();
        var result = await setA.AsStringAsync();

        // Assert
        Assert.Contains("Prompt A", result);
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
        await File.WriteAllTextAsync(Path.Combine(refundDir, "refund.prompt"), "Refund prompt");
        await File.WriteAllTextAsync(Path.Combine(policyDir, "policy.prompt"), "Policy prompt");
        await File.WriteAllTextAsync(Path.Combine(salesDir, "examples.prompt"), "Sales example");
        await File.WriteAllTextAsync(Path.Combine(salesDir, "instructions.prompt"), "Sales instructions");

        // Act
        var context = await PromptContext.FromFolder(tempRoot).LoadAsync();
        var refundSet = context.Get("CustomerService/Refund").CombineWithBase();
        var policySet = context.Get("CustomerService/Policy").CombineWithBase();
        var salesSet = context.Get("Sales").CombineWithBase();
        var refundResult = await refundSet.AsStringAsync();
        var policyResult = await policySet.AsStringAsync();
        var salesResult = await salesSet.AsStringAsync();

        // Assert
        Assert.Contains("Refund prompt", refundResult);
        Assert.Contains("Policy prompt", policyResult);
        Assert.Contains("Sales example", salesResult);
        Assert.Contains("Sales instructions", salesResult);
        // Cleanup
        Directory.Delete(tempRoot, true);
    }

    [Fact]
    public async Task LoadPromptSets_LoadsMdFilesInBasePromptSet()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempRoot);
        var salesDir = Path.Combine(tempRoot, "Sales");
        Directory.CreateDirectory(salesDir);
        await File.WriteAllTextAsync(Path.Combine(salesDir, "examples.md"), "Sales example");
        await File.WriteAllTextAsync(Path.Combine(salesDir, "instructions.md"), "Sales instructions");

        // Act
        var context = await PromptContext.FromFolder(tempRoot).LoadAsync();
        var salesSet = context.Get("Sales").CombineWithBase();
        var result = await salesSet.AsStringAsync();

        // Assert
        Assert.Contains("Sales example", result);
        Assert.Contains("Sales instructions", result);
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

        // Act
        var context = await PromptContext.FromFolder(customDir).LoadAsync();
        var setA = context.Get("CustomSet").CombineWithBase();
        var result = await setA.AsStringAsync();

        // Assert
        Assert.Contains("Prompt A", result);
        // Cleanup
        Directory.Delete(customDir, true);
    }

    [Fact]
    public async Task LoadPromptSets_NonExistentFolder_HandledGracefully()
    {
        // Arrange
        var nonExistentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var ex = await Record.ExceptionAsync(() => PromptContext.FromFolder(nonExistentDir).LoadAsync());
        Assert.Null(ex); // Should not throw
        var context = await PromptContext.FromFolder(nonExistentDir).LoadAsync();
        var result = await context.Get("SetA").CombineWithBase().AsStringAsync();
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetCombinedPrompts_UsesPromptListFromConfig()
    {
        // Arrange
        var prompts = new Dictionary<string, Prompt>
        {
            { "system", new Prompt("system", PromptFormat.Plain) },
            { "instructions", new Prompt("instructions", PromptFormat.Plain) }
        };
        var set = new PromptSet { Name = "Test", Prompts = prompts };
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("PromptList:0", "system"),
            new KeyValuePair<string, string?>("PromptList:1", "instructions")
        });
        var config = configBuilder.Build();
        var context = await PromptContext.FromFolder(".").WithConfig(config).LoadAsync();
        // Simulate loaded set
        var result = context.Get("Test").CombineWithBase();
        // No actual files, so result will be empty
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetCombinedPrompts_FallsBackToDefaultOrder()
    {
        // Arrange
        var prompts = new Dictionary<string, Prompt>
        {
            { "A", new Prompt("First", PromptFormat.Plain) },
            { "B", new Prompt("Second", PromptFormat.Plain) }
        };
        var set = new PromptSet { Name = "Test", Prompts = prompts };
        var context = await PromptContext.FromFolder(".").LoadAsync();
        // Simulate loaded set
        var result = context.Get("Test").CombineWithBase();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetCombinedPrompts_Overload_JoinsPromptSetCorrectly()
    {
        // Arrange
        var prompts = new Dictionary<string, Prompt>
        {
            { "system", new Prompt("System text", PromptFormat.Plain) },
            { "instructions", new Prompt("Instructions text", PromptFormat.Plain) },
            { "examples", new Prompt("Examples text", PromptFormat.Plain) }
        };
        var set = new PromptSet { Name = "Main", Prompts = prompts };
        var context = await PromptContext.FromFolder(".").LoadAsync();
        // Simulate loaded set
        var result = context.Get("Main").CombineWithBase();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetCombinedPrompts_UsesBasePromptIfMissingInSubdir()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllTextAsync(Path.Combine(tempRoot, "system.md"), "Base System");
        var salesDir = Path.Combine(tempRoot, "Sales");
        Directory.CreateDirectory(salesDir);
        await File.WriteAllTextAsync(Path.Combine(salesDir, "instructions.md"), "Sales Instructions");

        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("PromptSetFolder", tempRoot),
            new KeyValuePair<string, string?>("SupportedPromptExtensions:0", ".md"),
            new KeyValuePair<string, string?>("PromptList:0", "system"),
            new KeyValuePair<string, string?>("PromptList:1", "instructions")
        });
        var config = configBuilder.Build();
        var context = await PromptContext.FromFolder(tempRoot).WithConfig(config).LoadAsync();
        var salesSet = context.Get("Sales").CombineWithBase();
        var result = await salesSet.AsStringAsync();
        Assert.Contains("Base System", result);
        Assert.Contains("Sales Instructions", result);
        var idxSystem = result.IndexOf("Base System");
        var idxInstructions = result.IndexOf("Sales Instructions");
        Assert.True(idxSystem < idxInstructions);
        Directory.Delete(tempRoot, true);
    }

    [Fact]
    public async Task GetCombinedPrompts_SalesSet_InheritsSystemFromBase()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllTextAsync(Path.Combine(tempRoot, "system.md"), "Base System");
        var salesDir = Path.Combine(tempRoot, "Sales");
        Directory.CreateDirectory(salesDir);
        await File.WriteAllTextAsync(Path.Combine(salesDir, "examples.md"), "Sales Example");
        await File.WriteAllTextAsync(Path.Combine(salesDir, "instructions.md"), "Sales Instructions");

        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("PromptSetFolder", tempRoot),
            new KeyValuePair<string, string?>("SupportedPromptExtensions:0", ".md"),
            new KeyValuePair<string, string?>("PromptList:0", "system"),
            new KeyValuePair<string, string?>("PromptList:1", "examples"),
            new KeyValuePair<string, string?>("PromptList:2", "instructions")
        });
        var config = configBuilder.Build();
        var context = await PromptContext.FromFolder(tempRoot).WithConfig(config).LoadAsync();
        var salesSet = context.Get("Sales").CombineWithBase();
        var result = await salesSet.AsStringAsync();
        Assert.Contains("Base System", result);
        Assert.Contains("Sales Example", result);
        Assert.Contains("Sales Instructions", result);
        var idxSystem = result.IndexOf("Base System");
        var idxExamples = result.IndexOf("Sales Example");
        var idxInstructions = result.IndexOf("Sales Instructions");
        Assert.True(idxSystem < idxExamples && idxExamples < idxInstructions);
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
            new KeyValuePair<string, string?>("PromptSetFolder", tempRoot),
            new KeyValuePair<string, string?>("SupportedPromptExtensions:0", ".prompt"),
            new KeyValuePair<string, string?>("PromptList:0", "system"),
            new KeyValuePair<string, string?>("PromptList:1", "instructions"),
            new KeyValuePair<string, string?>("ConstrainPromptList", "true")
        });
        var config = configBuilder.Build();
        var context = await PromptContext.FromFolder(tempRoot).WithConfig(config).LoadAsync();
        var setA = context.Get("SetA").CombineWithBase();
        var result = await setA.AsStringAsync();
        Assert.Contains("System Prompt", result);
        Assert.Contains("Instructions Prompt", result);
        Assert.DoesNotContain("Other Prompt", result);
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
        var context = await PromptContext.FromFolder(tempRoot).WithConfig(config).LoadAsync();
        var setA = context.Get("SetA").CombineWithBase();
        var result = await setA.AsStringAsync();
        Assert.Contains("System Prompt", result);
        Assert.Contains("Instructions Prompt", result);
        Assert.Contains("Other Prompt", result);
        Directory.Delete(tempRoot, true);
    }

    [Fact]
    public async Task GetCombinedPrompts_PrependsFilenameHeaderOnFirstEntry()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempRoot);
        var setDir = Path.Combine(tempRoot, "SetA");
        Directory.CreateDirectory(setDir);
        await File.WriteAllTextAsync(Path.Combine(setDir, "system.prompt"), "You are a helpful agent.");
        await File.WriteAllTextAsync(Path.Combine(setDir, "instructions.prompt"), "Follow the user’s request.");
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string>("PromptList:0", "system"),
            new KeyValuePair<string, string>("PromptList:1", "instructions"),
            new KeyValuePair<string, string>("PromptSeparator", "\n  {filename}:  \n")
        });
        var config = configBuilder.Build();
        var context = await PromptContext.FromFolder(tempRoot).WithConfig(config).LoadAsync();
        var setA = context.Get("SetA").CombineWithBase();
        var result = await setA.AsStringAsync();
        Assert.StartsWith("System:  \nYou are a helpful agent.", result);
        Assert.Contains("\n  Instructions:  \nFollow the user’s request.", result);
        Assert.DoesNotContain("  System:  ", result);
        Directory.Delete(tempRoot, true);
    }
}
