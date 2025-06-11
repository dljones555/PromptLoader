using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using PromptLoader.Fluent;

namespace PromptLoader
{
    public static class PromptLoaderServiceCollectionExtensions
    {
        public static IServiceCollection AddPromptLoader(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IConfiguration>(configuration);
            services.AddTransient<PromptContext>();
            // Register other prompt sources here in the future
            return services;
        }
    }
}
