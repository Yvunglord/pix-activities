using System;
using System.Collections.Generic;
using System.Data;
using Xunit;
using Activities.Custom;

namespace Activities.Custom.Tests;

public class JoinDataTablesTests
{
    // Вспомогательный метод для создания тестовых таблиц
    private (DataTable Left, DataTable Right) CreateTestData()
    {
        var left = new DataTable("LeftTable");
        left.Columns.Add("ID", typeof(int));
        left.Columns.Add("Value", typeof(string));
        left.Rows.Add(1, "Left_1");
        left.Rows.Add(2, "Left_2");
        left.Rows.Add(DBNull.Value, "Left_Null"); // Строка с NULL-ключом

        var right = new DataTable("RightTable");
        right.Columns.Add("ID", typeof(int));
        right.Columns.Add("Value", typeof(string)); // Имя совпадает, проверим переименование
        right.Columns.Add("Extra", typeof(string));
        right.Rows.Add(1, "Right_1", "Ext_1");
        right.Rows.Add(3, "Right_3", "Ext_3");
        right.Rows.Add(DBNull.Value, "Right_Null", "Ext_Null"); // Строка с NULL-ключом

        return (left, right);
    }

    [Fact]
    public void Execute_InnerJoin_ReturnsOnlyMatchedRows()
    {
        // Arrange
        var (left, right) = CreateTestData();
        var activity = new JoinDataTables
        {
            FirstTable = left,
            SecondTable = right,
            ConnectionType = JoinType.Inner,
            JoinKeys = new List<string> { "ID" },
            SaveDestination = SaveResultTo.NewTable
        };

        // Act
        activity.Execute(null);
        var result = activity.ResultTable;

        // Assert
        // Должно быть 2 строки: ID=1 и ID=DBNull (так как NULL == NULL в нашей логике CompositeKey)
        Assert.Equal(2, result.Rows.Count);
        
        // Проверяем строку с ID=1
        var row1 = result.AsEnumerable().First(r => r.Field<int?>("ID") == 1);
        Assert.Equal("Left_1", row1["Value"]);
        Assert.Equal("Right_1", row1["Value_Right"]); // Проверка суффикса
    }

    [Fact]
    public void Execute_LeftJoin_ReturnsAllLeftRows()
    {
        // Arrange
        var (left, right) = CreateTestData();
        var activity = new JoinDataTables
        {
            FirstTable = left,
            SecondTable = right,
            ConnectionType = JoinType.Left,
            JoinKeys = new List<string> { "ID" },
            SaveDestination = SaveResultTo.NewTable
        };

        // Act
        activity.Execute(null);
        var result = activity.ResultTable;

        // Assert
        // Должны вернуться все 3 строки из левой таблицы
        Assert.Equal(3, result.Rows.Count);

        // Строка ID=2 не имеет пары справа, проверяем что там DBNull
        var row2 = result.AsEnumerable().First(r => r.Field<int?>("ID") == 2);
        Assert.Equal("Left_2", row2["Value"]);
        Assert.Equal(DBNull.Value, row2["Value_Right"]);
        Assert.Equal(DBNull.Value, row2["Extra"]);
    }

    [Fact]
    public void Execute_RightJoin_ReturnsAllRightRows()
    {
        // Arrange
        var (left, right) = CreateTestData();
        var activity = new JoinDataTables
        {
            FirstTable = left,
            SecondTable = right,
            ConnectionType = JoinType.Right,
            JoinKeys = new List<string> { "ID" },
            SaveDestination = SaveResultTo.NewTable
        };

        // Act
        activity.Execute(null);
        var result = activity.ResultTable;

        // Assert
        // Должны вернуться все 3 строки из правой таблицы
        Assert.Equal(3, result.Rows.Count);

        // Строка ID=3 не имеет пары слева
        var row3 = result.AsEnumerable().First(r => r.Field<int?>("ID") == 3);
        Assert.Equal(DBNull.Value, row3["Value"]); // Из левой таблицы — пусто
        Assert.Equal("Right_3", row3["Value_Right"]);
    }

    [Fact]
    public void Execute_FullJoin_ReturnsAllRows()
    {
        // Arrange
        var (left, right) = CreateTestData();
        var activity = new JoinDataTables
        {
            FirstTable = left,
            SecondTable = right,
            ConnectionType = JoinType.Full,
            JoinKeys = new List<string> { "ID" },
            SaveDestination = SaveResultTo.NewTable
        };

        // Act
        activity.Execute(null);
        var result = activity.ResultTable;

        // Assert
        // 1 (ID=1) + 1 (ID=2) + 1 (ID=3) + 1 (ID=Null) = 4 строки
        Assert.Equal(4, result.Rows.Count);
    }

    [Fact]
    public void Execute_IncompatibleTypes_ThrowsArgumentException()
    {
        // Arrange
        var left = new DataTable();
        left.Columns.Add("ID", typeof(int)); // Ключ — int

        var right = new DataTable();
        right.Columns.Add("ID", typeof(string)); // Ключ — string

        var activity = new JoinDataTables
        {
            FirstTable = left,
            SecondTable = right,
            ConnectionType = JoinType.Inner,
            JoinKeys = new List<string> { "ID" }
        };

        // Act & Assert
        // Проверяем, что валидация типов в ValidateKeys работает и выкидывает ошибку
        Assert.Throws<ArgumentException>(() => activity.Execute(null));
    }
}