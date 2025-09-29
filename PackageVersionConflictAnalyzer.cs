using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

public class PackageVersionConflictAnalyzer
{
    public class PackageVersionConflict
    {
        public string PackageName { get; set; } = string.Empty;
        public List<VersionInfo> Versions { get; set; } = new List<VersionInfo>();
    }

    public class VersionInfo
    {
        public string Version { get; set; } = string.Empty;
        public List<string> Projects { get; set; } = new List<string>();
    }

    public List<PackageVersionConflict> AnalyzeConflicts(List<ProjectReferenceParser.ProjectInfo> projects)
    {
        var conflicts = new List<PackageVersionConflict>();
        var packageVersions = new Dictionary<string, HashSet<string>>();
        var packageProjects = new Dictionary<string, Dictionary<string, List<string>>>();

        // Собираем информацию о версиях пакетов и проектах, которые их используют
        foreach (var project in projects)
        {
            var projectName = Path.GetFileNameWithoutExtension(project.ProjectPath);
            
            foreach (var package in project.PackageReferences)
            {
                // Добавляем версию пакета в список версий
                if (!packageVersions.ContainsKey(package.Name))
                {
                    packageVersions[package.Name] = new HashSet<string>();
                    packageProjects[package.Name] = new Dictionary<string, List<string>>();
                }
                packageVersions[package.Name].Add(package.Version);
                
                // Добавляем проект в список проектов для данной версии пакета
                if (!packageProjects[package.Name].ContainsKey(package.Version))
                {
                    packageProjects[package.Name][package.Version] = new List<string>();
                }
                
                // Добавляем информацию о том, является ли пакет транзитивным
                var packageSource = package.IsTransitive 
                    ? $"{projectName} (через {package.ParentPackage})" 
                    : projectName;
                    
                packageProjects[package.Name][package.Version].Add(packageSource);
            }
        }

        // Находим пакеты с несколькими версиями
        foreach (var package in packageVersions)
        {
            if (package.Value.Count > 1)
            {
                var conflict = new PackageVersionConflict
                {
                    PackageName = package.Key,
                    Versions = new List<VersionInfo>()
                };

                foreach (var version in package.Value)
                {
                    conflict.Versions.Add(new VersionInfo
                    {
                        Version = version,
                        Projects = packageProjects[package.Key][version]
                    });
                }

                conflicts.Add(conflict);
            }
        }

        return conflicts;
    }

    public string GenerateConflictReport(List<PackageVersionConflict> conflicts)
    {
        if (!conflicts.Any())
            return "Конфликтов версий пакетов не обнаружено.";

        var report = new StringBuilder();
        report.AppendLine("Обнаружены конфликты версий пакетов:");
        report.AppendLine();

        foreach (var conflict in conflicts)
        {
            report.AppendLine($"Пакет: {conflict.PackageName}");
            foreach (var versionInfo in conflict.Versions)
            {
                report.AppendLine($"  Версия {versionInfo.Version} используется в проектах:");
                foreach (var project in versionInfo.Projects)
                {
                    report.AppendLine($"    - {project}");
                }
            }
            report.AppendLine();
        }

        return report.ToString();
    }
} 