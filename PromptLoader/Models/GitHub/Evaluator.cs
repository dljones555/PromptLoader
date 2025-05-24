namespace PromptLoader.Models
{
    public class Evaluator
    {
        public required string Name { get; set; }
        public EvaluatorString? String { get; set; }
    }

    public class EvaluatorString
    {
        public string? StartsWith { get; set; }
    }
}
