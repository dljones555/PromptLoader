namespace PromptLoader.Utils
{
    public static class PathUtils
    {
        // Helper to resolve prompt folder path for dev, test, and published scenarios
        public static string ResolvePromptPath(string relativePath)
        {
            var exeDir = AppContext.BaseDirectory;
            var exePath = Path.GetFullPath(Path.Combine(exeDir, relativePath));
            if (Directory.Exists(exePath)) return exePath;


            // TODO: This is from CoPilot. Vet this logic.

            var dir = exeDir;
            for (int i = 0; i < 5; i++)
            {
                var candidate = Path.GetFullPath(Path.Combine(dir, relativePath));
                if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetFullPath(Path.Combine(dir, ".."));
            }
            return exePath;
        }
    }
}