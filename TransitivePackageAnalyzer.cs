using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

public class TransitivePackageAnalyzer
{
    private readonly string _nugetCachePath;

    public class TransitivePackageInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public List<string> DependencyPath { get; set; } = new List<string>();
    }

    public class DirectPackageInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public List<TransitivePackageInfo> TransitivePackages { get; set; } = new List<TransitivePackageInfo>();
    }

    public class ProjectTransitiveInfo
    {
        public string ProjectPath { get; set; }
        public List<DirectPackageInfo> DirectPackages { get; set; } = new List<DirectPackageInfo>();
    }

    public class TransitiveConflict
    {
        public string PackageName { get; set; }
        public List<ConflictVersion> Versions { get; set; } = new List<ConflictVersion>();
    }

    public class ConflictVersion
    {
        public string Version { get; set; }
        public List<string> SourcePackages { get; set; } = new List<string>();
    }

    public TransitivePackageAnalyzer()
    {
        // Определяем путь к кэшу NuGet
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _nugetCachePath = Path.Combine(userProfile, ".nuget", "packages");
        
        if (!Directory.Exists(_nugetCachePath))
        {
            Console.WriteLine($"Предупреждение: Локальный кэш NuGet не найден по пути: {_nugetCachePath}");
            Console.WriteLine("Анализ транзитивных зависимостей может быть неполным.");
        }
    }

    public async Task<List<ProjectReferenceParser.ProjectInfo>> EnrichWithTransitivePackagesAsync(
        List<ProjectReferenceParser.ProjectInfo> projects)
    {
        foreach (var project in projects)
        {
            var directPackages = project.PackageReferences.ToList();
            var transitivePackages = new List<ProjectReferenceParser.PackageReference>();
            var existingPackages = new Dictionary<string, HashSet<string>>(); // ключ - имя:версия, значение - пути зависимостей
            
            // Добавляем существующие пакеты в словарь
            foreach (var package in directPackages)
            {
                var packageKey = $"{package.Name}:{package.Version}";
                existingPackages[packageKey] = new HashSet<string> { $"{package.Name} {package.Version}" };
            }
            
            foreach (var package in directPackages)
            {
                try
                {
                    // Получаем транзитивные зависимости для пакета с путями
                    var dependencies = GetTransitivePackagesFromCache(package.Name, package.Version, new List<string> { $"{package.Name} {package.Version}" });
                    
                    foreach (var dependency in dependencies)
                    {
                        var packageKey = $"{dependency.Name}:{dependency.Version}";
                        var dependencyPath = string.Join(" -> ", dependency.DependencyPath);

                        // Если пакет уже существует, добавляем новый путь зависимости
                        if (existingPackages.ContainsKey(packageKey))
                        {
                            existingPackages[packageKey].Add(dependencyPath);
                            continue;
                        }

                        // Добавляем новый транзитивный пакет
                        transitivePackages.Add(new ProjectReferenceParser.PackageReference
                        {
                            Name = dependency.Name,
                            Version = dependency.Version,
                            IsTransitive = true,
                            ParentPackage = dependencyPath
                        });

                        existingPackages[packageKey] = new HashSet<string> { dependencyPath };
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при получении транзитивных зависимостей для {package.Name} {package.Version}: {ex.Message}");
                }
            }
            
            // Добавляем транзитивные пакеты к списку пакетов проекта
            project.PackageReferences.AddRange(transitivePackages);
        }
        
        return projects;
    }

    public async Task<List<ProjectTransitiveInfo>> AnalyzeTransitivePackagesAsync(List<ProjectReferenceParser.ProjectInfo> projects)
    {
        var result = new List<ProjectTransitiveInfo>();

        foreach (var project in projects)
        {
            var projectInfo = new ProjectTransitiveInfo
            {
                ProjectPath = project.ProjectPath
            };

            // Получаем только прямые зависимости
            var directPackages = project.PackageReferences.Where(p => !p.IsTransitive).ToList();

            foreach (var package in directPackages)
            {
                var directPackage = new DirectPackageInfo
                {
                    Name = package.Name,
                    Version = package.Version
                };

                try
                {
                    // Получаем транзитивные зависимости для пакета
                    var transitivePackages = GetTransitivePackagesFromCache(package.Name, package.Version, new List<string> { $"{package.Name} {package.Version}" });
                    directPackage.TransitivePackages.AddRange(transitivePackages);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при получении транзитивных зависимостей для {package.Name} {package.Version}: {ex.Message}");
                }

                projectInfo.DirectPackages.Add(directPackage);
            }

            result.Add(projectInfo);
        }

        return result;
    }

    private List<TransitivePackageInfo> GetTransitivePackagesFromCache(string packageName, string packageVersion, List<string> dependencyPath)
    {
        var result = new List<TransitivePackageInfo>();
        
        // Проверяем на циклические зависимости
        var packageKey = $"{packageName} {packageVersion}";
        if (dependencyPath.Count > 1 && dependencyPath.Contains(packageKey))
        {
            return result;
        }
        
        // Ищем пакет в локальном кэше
        var nuspecPath = GetNuspecPathFromCache(packageName, packageVersion);
        
        if (nuspecPath != null && File.Exists(nuspecPath))
        {
            // Если нашли в кэше, парсим локальный файл
            result = ParseNuspecFile(nuspecPath, dependencyPath);
            
            // Рекурсивно получаем зависимости для каждого транзитивного пакета
            var allDependencies = new List<TransitivePackageInfo>(result);
            foreach (var dependency in result)
            {
                var nestedDependencies = GetTransitivePackagesFromCache(
                    dependency.Name, 
                    dependency.Version, 
                    dependency.DependencyPath);
                allDependencies.AddRange(nestedDependencies);
            }
            result = allDependencies;
        }
        else
        {
            Console.WriteLine($"Предупреждение: Не удалось найти .nuspec файл для пакета {packageName} {packageVersion} в локальном кэше.");
        }
        
        return result;
    }
    
    private string GetNuspecPathFromCache(string packageName, string packageVersion)
    {
        try
        {
            var packagePath = Path.Combine(_nugetCachePath, packageName.ToLowerInvariant(), packageVersion.ToLowerInvariant());
            
            if (Directory.Exists(packagePath))
            {
                // Ищем .nuspec файл в директории пакета
                var nuspecFiles = Directory.GetFiles(packagePath, "*.nuspec");
                if (nuspecFiles.Length > 0)
                {
                    return nuspecFiles[0];
                }
                
                // Если не нашли в корне, проверяем в поддиректории
                var nuspecPath = Path.Combine(packagePath, packageName + ".nuspec");
                if (File.Exists(nuspecPath))
                {
                    return nuspecPath;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при поиске .nuspec файла в кэше для {packageName} {packageVersion}: {ex.Message}");
        }
        
        return null;
    }
    
    private List<TransitivePackageInfo> ParseNuspecFile(string nuspecPath, List<string> parentPath)
    {
        var result = new List<TransitivePackageInfo>();
        
        try
        {
            var nuspecXml = XDocument.Load(nuspecPath);
            var ns = nuspecXml.Root.GetDefaultNamespace();
            
            // Ищем группы зависимостей
            var dependencyGroups = nuspecXml.Descendants(ns + "dependencies").Elements();
            
            foreach (var group in dependencyGroups)
            {
                // Проверяем, является ли элемент группой или отдельной зависимостью
                if (group.Name.LocalName == "group")
                {
                    // Это группа зависимостей
                    var dependencies = group.Elements(ns + "dependency");
                    foreach (var dependency in dependencies)
                    {
                        AddDependencyToResult(dependency, result, parentPath);
                    }
                }
                else if (group.Name.LocalName == "dependency")
                {
                    // Это отдельная зависимость
                    AddDependencyToResult(group, result, parentPath);
                }
            }
            
            // Проверяем также прямые зависимости (без групп)
            var directDependencies = nuspecXml.Descendants(ns + "dependencies").Elements(ns + "dependency");
            foreach (var dependency in directDependencies)
            {
                AddDependencyToResult(dependency, result, parentPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при парсинге .nuspec файла {nuspecPath}: {ex.Message}");
        }
        
        return result;
    }
    
    private void AddDependencyToResult(XElement dependency, List<TransitivePackageInfo> result, List<string> parentPath)
    {
        var depId = dependency.Attribute("id")?.Value;
        var depVersion = dependency.Attribute("version")?.Value;
        
        if (!string.IsNullOrEmpty(depId) && !string.IsNullOrEmpty(depVersion))
        {
            // Очищаем версию от спецификаторов диапазона
            var cleanVersion = CleanVersionString(depVersion);
            
            // Создаем новый путь зависимостей, добавляя текущий пакет
            var newDependencyPath = new List<string>(parentPath)
            {
                $"{depId} {cleanVersion}"
            };
            
            result.Add(new TransitivePackageInfo
            {
                Name = depId,
                Version = cleanVersion,
                DependencyPath = newDependencyPath
            });
        }
    }

    private string CleanVersionString(string version)
    {
        // Удаляем спецификаторы диапазона версий
        return version
            .Replace("[", "")
            .Replace("]", "")
            .Replace("(", "")
            .Replace(")", "")
            .Replace(">=", "")
            .Replace("<=", "")
            .Replace(">", "")
            .Replace("<", "")
            .Replace("=", "")
            .Trim();
    }

    public List<TransitiveConflict> AnalyzeTransitiveConflicts(List<ProjectTransitiveInfo> projectsInfo)
    {
        var conflicts = new List<TransitiveConflict>();
        var allTransitivePackages = new Dictionary<string, Dictionary<string, HashSet<string>>>();

        // Собираем все транзитивные пакеты
        foreach (var project in projectsInfo)
        {
            foreach (var directPackage in project.DirectPackages)
            {
                foreach (var transitivePackage in directPackage.TransitivePackages)
                {
                    if (!allTransitivePackages.ContainsKey(transitivePackage.Name))
                    {
                        allTransitivePackages[transitivePackage.Name] = new Dictionary<string, HashSet<string>>();
                    }
                    
                    if (!allTransitivePackages[transitivePackage.Name].ContainsKey(transitivePackage.Version))
                    {
                        allTransitivePackages[transitivePackage.Name][transitivePackage.Version] = new HashSet<string>();
                    }
                    
                    allTransitivePackages[transitivePackage.Name][transitivePackage.Version].Add($"{directPackage.Name} {directPackage.Version}");
                }
            }
        }

        // Находим конфликты (пакеты с разными версиями)
        foreach (var package in allTransitivePackages)
        {
            if (package.Value.Count > 1)
            {
                var conflict = new TransitiveConflict
                {
                    PackageName = package.Key
                };
                
                foreach (var version in package.Value)
                {
                    conflict.Versions.Add(new ConflictVersion
                    {
                        Version = version.Key,
                        SourcePackages = version.Value.ToList()
                    });
                }
                
                conflicts.Add(conflict);
            }
        }

        return conflicts;
    }
} 