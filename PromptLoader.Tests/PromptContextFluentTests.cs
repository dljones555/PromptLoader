using Microsoft.Extensions.Configuration;
using PromptLoader.Fluent;

namespace PromptLoader.Tests;

public class PromptContextFluentTests : IAsyncLifetime, IDisposable
{
    private readonly string _testDir;
    private readonly IConfiguration _config;

    public PromptContextFluentTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);

        _config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appSettings.json", optional: true, reloadOnChange: false)
                .AddInMemoryCollection(new[]
                 {           
                    new KeyValuePair<string, string>("PromptLoader:PromptsFolder", _testDir),
                    new KeyValuePair<string, string>("PromptLoader:PromptSetFolder", _testDir)
                 })
                .Build();
    }

    [Fact]
    public async Task LoadPrompt_FromFile_Fluent_Works()
    {
        var filePath = Path.Combine(_testDir, "single.prompt");
        await File.WriteAllTextAsync(filePath, "Single Prompt Content");

        var ctx = await PromptContext
            .FromFile(filePath)
            .WithConfig(_config)
            .LoadAsync();

        var result = ctx.Get("single").AsString();
        Assert.Equal("Single Prompt Content", result);
    }

    [Fact]
    public async Task LoadPrompts_FromFolder_Fluent_Works()
    {
        // Arrange
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string>("PromptLoader:PromptsFolder", _testDir),
            new KeyValuePair<string, string>("PromptLoader:PromptExtensions:0", ".prompt"),
            new KeyValuePair<string, string>("PromptLoader:PromptExtensions:1", ".txt"),
            new KeyValuePair<string, string>("PromptLoader:ConstrainPromptList", "false")
        });
        var config = configBuilder.Build();
        var promptContext = new PromptContext().WithConfig(config);

        await File.WriteAllTextAsync(Path.Combine(_testDir, "a.prompt"), "Prompt A");
        await File.WriteAllTextAsync(Path.Combine(_testDir, "b.txt"), "Prompt B");

        var ctx = await PromptContext
            .FromFolder(_testDir)
            .WithConfig(config)
            .LoadAsync();

        var a = ctx.Get("Root/a").AsString();
        var b = ctx.Get("Root/b").AsString();
        Assert.Equal("Prompt A", a);
        Assert.Equal("Prompt B", b);
    }

    [Fact]
    public async Task CombinePrompts_Fluent_Works()
    {
        await File.WriteAllTextAsync(Path.Combine(_testDir, "system.prompt"), "System");
        await File.WriteAllTextAsync(Path.Combine(_testDir, "instructions.prompt"), "Instructions");

        var ctx = await PromptContext
            .FromFolder(_testDir)
            .WithConfig(_config)
            .LoadAsync();

        var combined = ctx.Get("Root").SeparateWith("\n").CombineWithRoot().AsString();
        Assert.Contains("System", combined);
        Assert.Contains("Instructions", combined);
    }

    [Fact]
    public async Task LoadPrompt_NonExistentFile_Fluent_HandledGracefully()
    {
        var filePath = Path.Combine(_testDir, "missing.prompt");
        var ctx = await PromptContext
            .FromFile(filePath)
            .WithConfig(_config)
            .LoadAsync();
        var result = ctx.Get("missing").AsString();
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task LoadPrompts_ConstrainPromptList_Fluent_Works()
    {
        await File.WriteAllTextAsync(Path.Combine(_testDir, "system.prompt"), "System Prompt");
        await File.WriteAllTextAsync(Path.Combine(_testDir, "instructions.prompt"), "Instructions Prompt");
        await File.WriteAllTextAsync(Path.Combine(_testDir, "other.prompt"), "Other Prompt");

        var ctx = await PromptContext
            .FromFolder(_testDir)
            .WithConfig(_config)
            .LoadAsync();

        var combined = ctx.Get("Root").SeparateWith("\n").CombineWithRoot().AsString();
        Assert.Contains("System Prompt", combined);
        Assert.Contains("Instructions Prompt", combined);
        Assert.DoesNotContain("Other Prompt", combined);
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