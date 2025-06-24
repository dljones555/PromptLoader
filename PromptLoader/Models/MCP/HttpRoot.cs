using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace PromptLoader.Models.MCP
{
    /// <summary>
    /// Represents an HTTP-based root in the MCP standard.
    /// </summary>
    public class HttpRoot : IRoot
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        
        /// <summary>
        /// Gets the URI of the root.
        /// </summary>
        public string Uri { get; }

        /// <summary>
        /// Gets the name of the root.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Creates a new instance of the HttpRoot class.
        /// </summary>
        /// <param name="url">The HTTP URL.</param>
        /// <param name="name">The name of the root.</param>
        public HttpRoot(string url, string name)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL cannot be null or empty", nameof(url));
            
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be null or empty", nameof(name));

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = $"https://{url}";
            }

            Uri = url;
            Name = name;
        }
        
        /// <summary>
        /// Loads prompts from this root.
        /// </summary>
        /// <returns>A collection of prompt definitions.</returns>
        public async Task<IEnumerable<PromptDefinition>> LoadAsync()
        {
            try
            {
                // Request the list of prompts from the HTTP endpoint
                var response = await _httpClient.GetAsync($"{Uri}/prompts/list");
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<PromptsListResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result?.Prompts ?? [];
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error loading prompts from HTTP endpoint {Uri}: {ex.Message}");
                return Array.Empty<PromptDefinition>();
            }
        }
        
        // Helper class for deserializing the prompts list response
        private class PromptsListResponse
        {
            public List<PromptDefinition> Prompts { get; set; } = new();
        }
    }
}