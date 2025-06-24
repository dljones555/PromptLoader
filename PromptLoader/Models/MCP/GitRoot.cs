
namespace PromptLoader.Models.MCP
{
    public class GitRoot : IRoot
    {
        public GitRoot() { }

        public GitRoot(string uri)
        {
            throw new NotImplementedException();
        }
        public GitRoot(string uri, string name)
        {
            throw new NotImplementedException();
        }
        public string Uri => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public Task<IEnumerable<PromptDefinition>> LoadAsync()
        {
            throw new NotImplementedException();
        }
    }
}