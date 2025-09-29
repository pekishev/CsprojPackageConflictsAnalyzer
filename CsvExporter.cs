using System.Text;

public class CsvExporter
{
    private static readonly string CsvSeparator = ";";
    
    public static async Task ExportToCsvAsync(string filePath, List<string> headers, List<List<string>> rows)
    {
        var sb = new StringBuilder();
        
        // Записываем заголовки
        sb.AppendLine(string.Join(CsvSeparator, headers.Select(EscapeCsvField)));
        
        // Записываем данные
        foreach (var row in rows)
        {
            // Форматируем версию (второй столбец) специальным образом
            var formattedRow = row.Select((field, index) => 
                index == 1 ? FormatVersionField(field) : EscapeCsvField(field));
            sb.AppendLine(string.Join(CsvSeparator, formattedRow));
        }
        
        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }
    
    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;
        
        // Если поле содержит кавычки, разделители или переносы строк - обрамляем кавычками
        if (field.Contains(CsvSeparator) || field.Contains("\"") || field.Contains("\n"))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        
        return field;
    }

    private static string FormatVersionField(string version)
    {
        // Добавляем префикс =", чтобы Excel воспринимал значение как текст
        return $"=\"{version}\"";
    }
} 