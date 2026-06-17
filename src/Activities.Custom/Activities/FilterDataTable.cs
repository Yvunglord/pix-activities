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
    [DisplayName("Результат")]
    public DataTable ResultTable { get; set; } = new();

    public override void Execute(int? optionID)
    {
        ArgumentNullException.ThrowIfNull(Table);

        var processedTable = Table.Copy();

        if (RowConditions != null && RowConditions.Any())
        {
            processedTable = ProcessRowsFilter(processedTable);
        }

        if (ColumnsList != null && ColumnsList.Any())
        {
            processedTable = ProcessColumnsFilter(processedTable);
        }
    }

    private DataTable ProcessRowsFilter(DataTable table)
    {
        var filteredRows = table.AsEnumerable().Where(row =>
        {
            bool isMatchAll = true;

            foreach (var cond in RowConditions)
            {
                if (!table.Columns.Contains(cond.ColumnName))
                    continue;

                var cellValue = row[cond.ColumnName];

                if (!EvaluateRowCondition(cellValue, cond.Operator, cond.Value))
                {
                    isMatchAll = false; 
                    break;
                }
            }

            return RowAction == FilterAction.Keep ? isMatchAll : !isMatchAll;
        });

        if (filteredRows.Any())
        {
            return filteredRows.CopyToDataTable();
        }
        else
        {
            DataTable emptyTable = table.Clone();
            return emptyTable;
        }
    }

    private bool EvaluateRowCondition(object cellValue, FilterOperator op, object targetValue)
    {
        if (cellValue == DBNull.Value || cellValue == null)
            return targetValue == null || targetValue.ToString() == "";

        string sCell = cellValue.ToString()!;
        string sTarget = targetValue.ToString() ?? "";

        bool isNumericCell = double.TryParse(sCell, out var dcell);
        bool isNumericTarget = double.TryParse(sTarget, out var dtarget);
        bool canCompareNumeric = isNumericCell || isNumericTarget;

        switch (op)
        {
            case FilterOperator.Equals:
                return sCell.Equals(sTarget, StringComparison.OrdinalIgnoreCase);

            case FilterOperator.NotEquals:
                return !sCell.Equals(sTarget, StringComparison.OrdinalIgnoreCase);

            //TODO: add more cases

            default:
                return false;
        }
    }

    private DataTable ProcessColumnsFilter(DataTable table)
    {
        List<DataColumn> columnsToModify = new List<DataColumn>();

        foreach (DataColumn column in table.Columns)
        {
            bool isListed = ColumnsList.Contains(column.ColumnName, StringComparer.OrdinalIgnoreCase);

            if (ColumnAction == FilterAction.Keep && !isListed)
            {
                columnsToModify.Add(column);
            }

            else if (ColumnAction == FilterAction.Remove && isListed)
            {
                columnsToModify.Add(column);
            }
        }

        foreach (var col in columnsToModify)
        {
            table.Columns.Remove(col);
        }

        return table;
    }
}
