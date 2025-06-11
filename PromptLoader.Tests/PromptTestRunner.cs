using PromptLoader.Models;
using System.Text.RegularExpressions;

public static class PromptTestRunner
{
    public static void RunPromptYmlTests(string promptYmlPath)
    {
        var promptYml = Prompt.LoadPromptYml(promptYmlPath);

        foreach (var test in promptYml.TestData ?? Enumerable.Empty<TestData>())
        {
            // Find the user message template
            var userMsg = promptYml.Messages.FirstOrDefault(m => m.Role == "user")?.Content ?? "";
            // Substitute {{input}} with test input
            var renderedPrompt = Regex.Replace(userMsg, @"\{\{input\}\}", test.Input);

            // Simulate LLM output (replace with actual LLM call in production)
            // For demo, just echo the expected output or rendered prompt
            string llmOutput = test.Expected ?? renderedPrompt;

            // Apply evaluators
            bool passed = true;
            foreach (var eval in promptYml.Evaluators ?? Enumerable.Empty<Evaluator>())
            {
                if (eval.String?.StartsWith != null)
                {
                    if (!llmOutput.StartsWith(eval.String.StartsWith))
                    {
                        passed = false;
                        Console.WriteLine($"Test FAILED: Output does not start with '{eval.String.StartsWith}'");
                    }
                }
            }

            // Compare to expected output if provided
            if (!string.IsNullOrWhiteSpace(test.Expected) && llmOutput != test.Expected)
            {
                passed = false;
                Console.WriteLine($"Test FAILED: Output does not match expected.\nExpected: {test.Expected}\nActual:   {llmOutput}");
            }

            if (passed)
            {
                Console.WriteLine("Test PASSED.");
            }
        }
    }
}
