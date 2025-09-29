using System;
using System.Collections.Generic;
using System.Text;

public class TableFormatter
{
    public class Column
    {
        public string Header { get; set; } = string.Empty;
        public int Width { get; set; }
    }

    public static string FormatTable(List<Column> columns, List<List<string>> rows)
    {
        var sb = new StringBuilder();
        var separator = new string('-', columns.Sum(c => c.Width + 3) - 1);

        // Заголовок
        sb.AppendLine(separator);
        foreach (var col in columns)
        {
            sb.Append("| ").Append(col.Header.PadRight(col.Width));
        }
        sb.AppendLine("|");
        sb.AppendLine(separator);

        // Данные
        foreach (var row in rows)
        {
            for (int i = 0; i < columns.Count; i++)
            {
                var value = i < row.Count ? row[i] : string.Empty;
                if (value.Length > columns[i].Width)
                {
                    value = value.Substring(0, columns[i].Width - 3) + "...";
                }
                sb.Append("| ").Append(value.PadRight(columns[i].Width));
            }
            sb.AppendLine("|");
        }
        sb.AppendLine(separator);

        return sb.ToString();
    }
} 