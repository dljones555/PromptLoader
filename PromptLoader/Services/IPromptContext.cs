using Microsoft.Extensions.Configuration;

namespace PromptLoader.Fluent
{
    public interface IPromptContext
    {
        static abstract PromptContext FromFile(string file = "", bool cascadeOverride = true);
        static abstract PromptContext FromFolder(string folder = "", bool cascadeOverride = true);
        string AsString();
        PromptContext CombineWithBase();
        PromptContext SeparateWith(string separator = "");
        PromptContext Get(string path);
        Task<PromptContext> LoadAsync();
        PromptContext WithConfig(IConfiguration config);
        PromptContext WithConfig(string configPath);
    }
}