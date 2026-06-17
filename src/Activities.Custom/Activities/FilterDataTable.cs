using Activities.Custom.Models;
using BR.Core;
using BR.Core.Attributes;
using System.ComponentModel;
using System.Data;

namespace Activities.Custom.Activities;

public enum FilterAction
{
    Keep,
    Remove
}

public enum FilterOperator
{
    Equals,
    NotEquals
}

[ScreenName("Отфильтровать таблицу")]
[Representation("[Table] -> [ResultTable]")]
[BR.Core.Attributes.Path("Custom activities")]
public class FilterDataTable : Activity
{
    [Category("Входные данные")]
    [DisplayName("Целевая таблица")]
    [IsRequired]
    public DataTable Table { get; set; } = new();

    [Category("Фильтрация строк")]
    [DisplayName("Действие для строк")]
    public FilterAction RowAction { get; set; } = FilterAction.Keep;

    [Category("Фильтрация строк")]
    [DisplayName("Условия для строк")]
    public List<FilterRowCondition> RowConditions { get; set; } = new();

    [Category("Фильтрация столбцов")]
    [DisplayName("Действие для столбцов")]
    public FilterAction ColumnAction { get; set; } = FilterAction.Keep;

    [Category("Фильтрация столбцов")]
    [DisplayName("Список столбцов")]
    public List<string> ColumnsList { get; set; } = new();

    [Category("Выходные данные")]
    [DisplayName("Результирующая таблица")]
    [IsOut]
    public DataTable ResultTable { get; set; } = new();

    public override void Execute(int? optionID)
    {
        ArgumentNullException.ThrowIfNull(Table);

        ValidateConfiguration();

        var result = Table.Copy();

        if (RowConditions.Count > 0)
        {
            result = ProcessRowsFilter(result);
        }

        if (ColumnsList.Count > 0)
        {
            result = ProcessColumnsFilter(result);
        }

        ResultTable = result;
    }

    private void ValidateConfiguration()
    {
        foreach (var condition in RowConditions)
        {
            if (string.IsNullOrWhiteSpace(condition.ColumnName))
            {
                throw new ArgumentException(
                    "Для одного из условий фильтрации не указано имя столбца.");
            }

            if (!Table.Columns.Contains(condition.ColumnName))
            {
                throw new ArgumentException(
                    $"Таблица не содержит столбец '{condition.ColumnName}'.");
            }
        }

        foreach (var columnName in ColumnsList)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new ArgumentException(
                    "Список столбцов содержит пустое имя.");
            }

            if (!Table.Columns.Contains(columnName))
            {
                throw new ArgumentException(
                    $"Таблица не содержит столбец '{columnName}'.");
            }
        }
    }

    private DataTable ProcessRowsFilter(DataTable table)
    {
        var rows = table.AsEnumerable()
            .Where(ShouldKeepRow)
            .ToList();

        if (rows.Count == 0)
        {
            return table.Clone();
        }

        return rows.CopyToDataTable();
    }

    private bool ShouldKeepRow(DataRow row)
    {
        bool matches = EvaluateRow(row);

        return RowAction switch
        {
            FilterAction.Keep => matches,
            FilterAction.Remove => !matches,
            _ => throw new ArgumentOutOfRangeException(nameof(RowAction))
        };
    }

    private bool EvaluateRow(DataRow row)
    {
        foreach (var condition in RowConditions)
        {
            var cellValue = row[condition.ColumnName];

            if (!EvaluateCondition(cellValue, condition))
            {
                return false;
            }
        }

        return true;
    }

    private bool EvaluateCondition(
        object cellValue,
        FilterRowCondition condition)
    {
        return EvaluateOperator(
            NormalizeValue(cellValue),
            condition.Operator,
            NormalizeValue(condition.Value));
    }

    private bool EvaluateOperator(
        object? actualValue,
        FilterOperator op,
        object? expectedValue)
    {
        switch (op)
        {
            case FilterOperator.Equals:
                return AreEqual(actualValue, expectedValue);

            case FilterOperator.NotEquals:
                return !AreEqual(actualValue, expectedValue);

            default:
                throw new NotSupportedException(
                    $"Оператор '{op}' не поддерживается.");
        }
    }

    private static object? NormalizeValue(object? value)
    {
        if (value == DBNull.Value)
        {
            return null;
        }

        return value;
    }

    private static bool AreEqual(
        object? actualValue,
        object? expectedValue)
    {
        if (actualValue == null && expectedValue == null)
        {
            return true;
        }

        if (actualValue == null || expectedValue == null)
        {
            return false;
        }

        return string.Equals(
            actualValue.ToString(),
            expectedValue.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    private DataTable ProcessColumnsFilter(DataTable table)
    {
        var columnsToRemove = table.Columns
            .Cast<DataColumn>()
            .Where(column =>
            {
                bool isListed = ColumnsList.Contains(
                    column.ColumnName,
                    StringComparer.OrdinalIgnoreCase);

                return ColumnAction switch
                {
                    FilterAction.Keep => !isListed,
                    FilterAction.Remove => isListed,
                    _ => throw new ArgumentOutOfRangeException(nameof(ColumnAction))
                };
            })
            .ToList();

        foreach (var column in columnsToRemove)
        {
            table.Columns.Remove(column);
        }

        return table;
    }
}