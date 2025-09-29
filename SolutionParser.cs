using System.Text.RegularExpressions;

public class SolutionParser
{
    private static readonly Regex ProjectRegex = new(
        "Project\\(\"\\{[\\w-]*\\}\"\\)\\s*=\\s*\"[^\"]*\",\\s*\"([^\"]*)\",\\s*\"\\{[\\w-]*\\}\"",
        RegexOptions.Compiled);

    public async Task<List<string>> GetProjectPathsAsync(string solutionPath)
    {
        if (!File.Exists(solutionPath))
            throw new FileNotFoundException("Solution file not found", solutionPath);

        var solutionDirectory = Path.GetDirectoryName(solutionPath) ?? string.Empty;
        var projectPaths = new List<string>();

        var solutionContent = await File.ReadAllTextAsync(solutionPath);
        var matches = ProjectRegex.Matches(solutionContent);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                var relativePath = match.Groups[1].Value;
                // Преобразуем пути в Windows-стиле в платформо-независимый формат
                relativePath = relativePath.Replace('\\', Path.DirectorySeparatorChar);
                var fullPath = Path.GetFullPath(Path.Combine(solutionDirectory, relativePath));
                
                if (File.Exists(fullPath) && fullPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    projectPaths.Add(fullPath);
                }
            }
        }

        return projectPaths;
    }
} 