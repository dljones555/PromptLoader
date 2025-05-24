using System.Collections.Generic;

namespace PromptLoader.Models
{
    public class PromptSet
    {
        public required string Name { get; set; }
        public Dictionary<string, Prompt> Prompts { get; set; } = new();
    }
}
