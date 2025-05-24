using System.IO;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PromptLoader.Models
{
    public class Prompt
    {
        public string Text { get; }
        public PromptFormat Format { get; }

        public Prompt(string text, PromptFormat format)
        {
            Text = text;
            Format = format;
        }

        public bool HasVariables()
        {
            return Regex.IsMatch(Text, @"\{[\w\d_]+\}") || Regex.IsMatch(Text, @"\{\{[\w\d_]+\}\}");
        }

        public string ToYaml()
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            return serializer.Serialize(new { text = Text, format = Format.ToString() });
        }

        public PromptYml ToPromptYml()
        {
            var deserializer = new DeserializerBuilder()
                                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                                .Build();
    
            if (Format != PromptFormat.Yaml)
            {
                throw new InvalidOperationException("Cannot convert to PromptYml from this format.");
            }
            return deserializer.Deserialize<PromptYml>(this.Text);
        }

        public static PromptYml LoadPromptYml(string filePath)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            var yaml = File.ReadAllText(filePath);
            return deserializer.Deserialize<PromptYml>(yaml);
        }
    }
   
}
