// Пример использования
var solutionParser = new SolutionParser();
var projectReferenceParser = new ProjectReferenceParser();
var transitivePackageAnalyzer = new TransitivePackageAnalyzer();

try
{
    if (args.Length == 0)
    {
        Console.WriteLine("Пожалуйста, укажите путь к .sln файлу в качестве параметра.");
        Console.WriteLine("Использование: ParseCsProj.exe <путь_к_solution_файлу>");
        return;
    }

    var solutionPath = args[0];
    if (!File.Exists(solutionPath))
    {
        Console.WriteLine($"Файл не найден: {solutionPath}");
        return;
    }

    if (!solutionPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Указанный файл не является файлом решения (.sln)");
        return;
    }

    var projectPaths = await solutionParser.GetProjectPathsAsync(solutionPath);
    
    Console.WriteLine("Найденные проекты в решении:");
    foreach (var path in projectPaths)
    {
        Console.WriteLine($"- {path}");
    }
    Console.WriteLine();

    var allResults = new List<ProjectReferenceParser.ProjectInfo>();
    foreach (var projectPath in projectPaths)
    {
        var results = await projectReferenceParser.ParseProjectReferencesRecursivelyAsync(projectPath);
        allResults.AddRange(results);
    }

    // Удаляем дубликаты проектов, которые могли появиться из-за рекурсивного обхода
    allResults = allResults
        .GroupBy(p => p.ProjectPath)
        .Select(g => g.First())
        .ToList();
        
    // Обогащаем проекты информацией о транзитивных зависимостях
    Console.WriteLine("\nАнализ транзитивных зависимостей...");
    allResults = await transitivePackageAnalyzer.EnrichWithTransitivePackagesAsync(allResults);
    Console.WriteLine("Анализ транзитивных зависимостей завершен.");

    // Собираем информацию о пакетах (включая транзитивные)
    var packageInfo = new Dictionary<string, Dictionary<string, HashSet<string>>>();
    foreach (var project in allResults)
    {
        var projectName = Path.GetFileNameWithoutExtension(project.ProjectPath);
        
        // Группируем пакеты по имени и версии для текущего проекта
        var projectPackages = project.PackageReferences
            .GroupBy(p => new { p.Name, p.Version })
            .Select(g => g.First()) // Берем первый пакет из группы с одинаковым именем и версией
            .ToList();
        
        foreach (var package in projectPackages)
        {
            if (!packageInfo.ContainsKey(package.Name))
            {
                packageInfo[package.Name] = new Dictionary<string, HashSet<string>>();
            }
            if (!packageInfo[package.Name].ContainsKey(package.Version))
            {
                packageInfo[package.Name][package.Version] = new HashSet<string>();
            }
            
            // Добавляем информацию о том, является ли пакет транзитивным
            var packageSource = package.IsTransitive 
                ? $"{projectName} (через {package.ParentPackage})" 
                : projectName;
                
            packageInfo[package.Name][package.Version].Add(packageSource);
        }
    }

    // Подготовка данных для CSV
    var headers = new List<string> { "Пакет", "Версия", "Путь зависимостей" };
    var rows = new List<List<string>>();

    // Собираем все пути для каждого пакета
    var packagePaths = new Dictionary<string, HashSet<string>>(); // ключ - "пакет:версия", значение - множество путей

    foreach (var project in allResults)
    {
        var projectName = Path.GetFileNameWithoutExtension(project.ProjectPath);
        
        // Обрабатываем все пакеты проекта
        foreach (var package in project.PackageReferences)
        {
            var packageKey = $"{package.Name}:{package.Version}";
            
            if (!packagePaths.ContainsKey(packageKey))
            {
                packagePaths[packageKey] = new HashSet<string>();
            }
            
            if (package.IsTransitive)
            {
                // Для транзитивных зависимостей добавляем полный путь
                packagePaths[packageKey].Add($"{projectName} -> {package.ParentPackage}");
            }
            else
            {
                // Для прямых зависимостей добавляем только имя проекта
                packagePaths[packageKey].Add(projectName);
            }
        }
    }

    // Формируем строки для CSV
    foreach (var packageEntry in packagePaths.OrderBy(p => p.Key))
    {
        var packageParts = packageEntry.Key.Split(':');
        var packageName = packageParts[0];
        var packageVersion = packageParts[1];
        
        // Сортируем пути: сначала прямые (без ->), потом транзитивные
        var sortedPaths = packageEntry.Value
            .OrderBy(p => p.Contains("->"))
            .ThenBy(p => p);
        
        rows.Add(new List<string>
        {
            packageName,
            packageVersion,
            string.Join("\n", sortedPaths)
        });
    }

    // Формируем имя файла с текущей датой и временем
    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    var csvFilePath = Path.Combine(
        Path.GetDirectoryName(solutionPath) ?? string.Empty,
        $"packages_report_{timestamp}.csv");

    // Экспортируем данные в CSV
    await CsvExporter.ExportToCsvAsync(csvFilePath, headers, rows);
    Console.WriteLine($"\nОтчет сохранен в файл: {csvFilePath}");

    // Анализ конфликтов версий
    var conflictAnalyzer = new PackageVersionConflictAnalyzer();
    var conflicts = conflictAnalyzer.AnalyzeConflicts(allResults);
    var conflictReport = conflictAnalyzer.GenerateConflictReport(conflicts);
    Console.WriteLine("\nАнализ конфликтов версий:");
    Console.WriteLine(conflictReport);
}
catch (Exception ex)
{
    Console.WriteLine($"Произошла ошибка: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
} 