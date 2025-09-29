using System.Xml.Linq;

public class ProjectReferenceParser
{
    public class ProjectInfo
    {
        public string ProjectPath { get; set; } = string.Empty;
        public List<PackageReference> PackageReferences { get; set; } = new();
        public List<string> ProjectReferences { get; set; } = new();
    }

    public class PackageReference
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public bool IsTransitive { get; set; } = false;
        public string ParentPackage { get; set; } = null;
    }

    public async Task<List<ProjectInfo>> ParseProjectReferencesRecursivelyAsync(string projectPath)
    {
        var result = new List<ProjectInfo>();
        var processedProjects = new HashSet<string>();
        await ParseProjectInternalAsync(projectPath, result, processedProjects);
        return result;
    }

    private async Task ParseProjectInternalAsync(string projectPath, List<ProjectInfo> results, HashSet<string> processedProjects)
    {
        if (!File.Exists(projectPath) || !processedProjects.Add(projectPath))
            return;

        var projectInfo = new ProjectInfo { ProjectPath = projectPath };
        var projectDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty;
        
        var doc = XDocument.Load(projectPath);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        // Получаем PackageReference
        var packageRefs = doc.Descendants(ns + "PackageReference")
            .Select(x => new PackageReference
            {
                Name = x.Attribute("Include")?.Value ?? string.Empty,
                Version = x.Attribute("Version")?.Value ?? string.Empty
            })
            .ToList();

        projectInfo.PackageReferences.AddRange(packageRefs);

        // Получаем ProjectReference
        var projectRefs = doc.Descendants(ns + "ProjectReference")
            .Select(x => x.Attribute("Include")?.Value ?? string.Empty)
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();

        projectInfo.ProjectReferences.AddRange(projectRefs);
        results.Add(projectInfo);

        // Рекурсивно обрабатываем все ProjectReference
        foreach (var projectRef in projectRefs)
        {
            var fullPath = Path.GetFullPath(Path.Combine(projectDirectory, projectRef));
            await ParseProjectInternalAsync(fullPath, results, processedProjects);
        }
    }
} 