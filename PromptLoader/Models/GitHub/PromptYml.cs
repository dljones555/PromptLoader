using System.Collections.Generic;

namespace PromptLoader.Models
{
    public class PromptYml
    {
        public required string Name { get; set; }
        public required string Description { get; set; }
        public required string Model { get; set; }
        public Dictionary<string, object>? ModelParameters { get; set; }
        public List<Message>? Messages { get; set; }
        public List<TestData>? TestData { get; set; }
        public List<Evaluator>? Evaluators { get; set; }
    }
}
