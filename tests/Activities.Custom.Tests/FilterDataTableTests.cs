using Activities.Custom.Activities;
using Activities.Custom.Models;
using System.Data;
using Xunit;

namespace Activities.Custom.Tests;

public class FilterDataTableTests
{
    /// <summary>
    /// Если условие ссылается на несуществующий столбец,
    /// активность обязана упасть до начала обработки.
    /// </summary>
    [Fact]
    public void Execute_NonExistingConditionColumn_ThrowsArgumentException()
    {
        var table = new DataTable();
        table.Columns.Add("ID", typeof(int));

        var activity = new FilterDataTable
        {
            Table = table,
            RowConditions = new List<FilterRowCondition>()
            {
                new FilterRowCondition
                {
                    ColumnName = "GhostColumn",
                    Operator = FilterOperator.Equals,
                    Value = 1
                }
            }
        };

        var ex = Assert.Throws<ArgumentException>(() => activity.Execute(null));

        Assert.Contains("GhostColumn", ex.Message);
    }

    /// <summary>
    /// Аналогичная проверка для фильтрации столбцов.
    /// </summary>
    [Fact]
    public void Execute_NonExistingColumnInColumnsList_ThrowsArgumentException()
    {
        var table = new DataTable();
        table.Columns.Add("ID", typeof(int));

        var activity = new FilterDataTable
        {
            Table = table,
            ColumnsList = new List<string>() { "MissingColumn" }
        };

        var ex = Assert.Throws<ArgumentException>(() => activity.Execute(null));

        Assert.Contains("MissingColumn", ex.Message);
    }

    /// <summary>
    /// Проверяет что Keep действительно оставляет только совпавшие строки.
    /// </summary>
    [Fact]
    public void Execute_KeepRows_ReturnsOnlyMatchingRows()
    {
        var table = new DataTable();

        table.Columns.Add("ID", typeof(int));

        table.Rows.Add(1);
        table.Rows.Add(2);
        table.Rows.Add(3);

        var activity = new FilterDataTable
        {
            Table = table,
            RowAction = FilterAction.Keep,
            RowConditions = new List<FilterRowCondition>()
            {
                new FilterRowCondition
                {
                    ColumnName = "ID",
                    Operator = FilterOperator.Equals,
                    Value = 2
                }
            }
        };

        activity.Execute(null);

        Assert.Single(activity.ResultTable.Rows);
        Assert.Equal(2, activity.ResultTable.Rows[0]["ID"]);
    }

    /// <summary>
    /// Проверяет инверсию логики Remove.
    /// </summary>
    [Fact]
    public void Execute_RemoveRows_RemovesMatchingRows()
    {
        var table = new DataTable();

        table.Columns.Add("ID", typeof(int));

        table.Rows.Add(1);
        table.Rows.Add(2);
        table.Rows.Add(3);

        var activity = new FilterDataTable
        {
            Table = table,
            RowAction = FilterAction.Remove,
            RowConditions = new List<FilterRowCondition>()
            {
                new FilterRowCondition
                {
                    ColumnName = "ID",
                    Operator = FilterOperator.Equals,
                    Value = 2
                }
            }
        };

        activity.Execute(null);

        Assert.Equal(2, activity.ResultTable.Rows.Count);

        Assert.DoesNotContain(
            activity.ResultTable.AsEnumerable(),
            r => (int)r["ID"] == 2);
    }

    /// <summary>
    /// Проверка что при отсутствии совпадений возвращается
    /// пустая таблица с сохранённой структурой.
    /// </summary>
    [Fact]
    public void Execute_NoMatches_ReturnsEmptyTableWithOriginalSchema()
    {
        var table = new DataTable();

        table.Columns.Add("ID", typeof(int));
        table.Columns.Add("Name", typeof(string));

        table.Rows.Add(1, "John");

        var activity = new FilterDataTable
        {
            Table = table,
            RowConditions = new List<FilterRowCondition>()
            {
                new FilterRowCondition
                {
                    ColumnName = "ID",
                    Operator = FilterOperator.Equals,
                    Value = 999
                }
            }
        };

        activity.Execute(null);

        Assert.Empty(activity.ResultTable.Rows);

        Assert.Equal(
            table.Columns.Count,
            activity.ResultTable.Columns.Count);

        Assert.Equal(
            typeof(int),
            activity.ResultTable.Columns["ID"]!.DataType);

        Assert.Equal(
            typeof(string),
            activity.ResultTable.Columns["Name"]!.DataType);
    }

    /// <summary>
    /// Проверяет что фильтрация столбцов Keep
    /// удаляет все неуказанные поля.
    /// </summary>
    [Fact]
    public void Execute_KeepColumns_RemovesAllOtherColumns()
    {
        var table = new DataTable();

        table.Columns.Add("ID");
        table.Columns.Add("Name");
        table.Columns.Add("Age");

        table.Rows.Add("1", "John", "30");

        var activity = new FilterDataTable
        {
            Table = table,
            ColumnAction = FilterAction.Keep,
            ColumnsList = new List<string>() { "ID", "Name" } 
        };

        activity.Execute(null);

        Assert.Equal(2, activity.ResultTable.Columns.Count);

        Assert.True(activity.ResultTable.Columns.Contains("ID"));
        Assert.True(activity.ResultTable.Columns.Contains("Name"));
        Assert.False(activity.ResultTable.Columns.Contains("Age"));
    }

    /// <summary>
    /// Проверяет что фильтрация столбцов Remove
    /// удаляет только указанные поля.
    /// </summary>
    [Fact]
    public void Execute_RemoveColumns_RemovesOnlyListedColumns()
    {
        var table = new DataTable();

        table.Columns.Add("ID");
        table.Columns.Add("Name");
        table.Columns.Add("Age");

        table.Rows.Add("1", "John", "30");

        var activity = new FilterDataTable
        {
            Table = table,
            ColumnAction = FilterAction.Remove,
            ColumnsList = new List<string>() { "Age" }
        };

        activity.Execute(null);

        Assert.True(activity.ResultTable.Columns.Contains("ID"));
        Assert.True(activity.ResultTable.Columns.Contains("Name"));
        Assert.False(activity.ResultTable.Columns.Contains("Age"));
    }

    /// <summary>
    /// Коварный тест:
    /// фильтрация не должна изменять исходную таблицу.
    /// </summary>
    [Fact]
    public void Execute_DoesNotMutateSourceTable()
    {
        var table = new DataTable();

        table.Columns.Add("ID");
        table.Columns.Add("Name");

        table.Rows.Add("1", "John");

        var activity = new FilterDataTable
        {
            Table = table,
            ColumnAction = FilterAction.Remove,
            ColumnsList = new List<string>() { "Name" }
        };

        activity.Execute(null);

        Assert.True(table.Columns.Contains("Name"));
        Assert.Equal(2, table.Columns.Count);
    }

    /// <summary>
    /// Проверка множественных условий.
    /// Сейчас логика AND.
    /// Совпасть должны все условия одновременно.
    /// </summary>
    [Fact]
    public void Execute_MultipleConditions_UsesAndLogic()
    {
        var table = new DataTable();

        table.Columns.Add("ID", typeof(int));
        table.Columns.Add("Group", typeof(string));

        table.Rows.Add(1, "A");
        table.Rows.Add(1, "B");
        table.Rows.Add(2, "A");

        var activity = new FilterDataTable
        {
            Table = table,
            RowConditions = new List<FilterRowCondition>()
            {
                new FilterRowCondition
                {
                    ColumnName = "ID",
                    Operator = FilterOperator.Equals,
                    Value = 1
                },
                new FilterRowCondition
                {
                    ColumnName = "Group",
                    Operator = FilterOperator.Equals,
                    Value = "A"
                }
            }
        };

        activity.Execute(null);

        Assert.Single(activity.ResultTable.Rows);

        Assert.Equal(1, activity.ResultTable.Rows[0]["ID"]);
        Assert.Equal("A", activity.ResultTable.Rows[0]["Group"]);
    }
}