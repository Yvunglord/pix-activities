using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Xunit;
using Activities.Custom;

namespace Activities.Custom.Tests;

public class JoinDataTablesTests
{
    /// <summary>
    /// Тест проверяет декартово произведение (многие-ко-многим) для дублирующихся ключей,
    /// а также корректность сопоставления по РАЗНЫМ именам ключей.
    /// При новой логике ОБА ключа остаются в результирующей таблице.
    /// </summary>
    [Fact]
    public void Execute_ManyToManyWithDifferentKeyNames_EvaluatesCorrectCartesianProduct()
    {
        // Arrange
        var left = new DataTable();
        left.Columns.Add("LeftKey", typeof(int));
        left.Columns.Add("Payload", typeof(string));
        left.Rows.Add(10, "L_A");
        left.Rows.Add(10, "L_B"); // Дубликат ключа 10

        var right = new DataTable();
        right.Columns.Add("RightKey", typeof(int));
        right.Columns.Add("Payload", typeof(string)); // Коллизия имени не-ключевой колонки
        right.Rows.Add(10, "R_X");
        right.Rows.Add(10, "R_Y"); // Дубликат ключа 10

        var activity = new JoinDataTables
        {
            FirstTable = left,
            SecondTable = right,
            ConnectionType = JoinType.Inner,
            LeftJoinKeys = new List<string> { "LeftKey" },
            RightJoinKeys = new List<string> { "RightKey" },
            SaveDestination = SaveResultTo.NewTable
        };

        // Act
        activity.Execute(null);
        var result = activity.ResultTable;

        // Assert
        // Должно быть 2 * 2 = 4 строки для ключа 10
        Assert.Equal(4, result.Rows.Count);

        // При новой логике (без явной проекции) ОБА ключа присутствуют в результате
        Assert.Contains("LeftKey", result.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
        Assert.Contains("RightKey", result.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
        
        // Проверяем переименование не-ключевой колонки с коллизией имён
        Assert.Contains("Payload_Right", result.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
    }

    /// <summary>
    /// Коварный тест на коллизию суффиксов.
    /// Что если в правой таблице УЖЕ есть колонка с именем "Info_Right"?
    /// Алгоритм не должен затереть данные или упасть с ошибкой "Column already belongs to this DataTable".
    /// </summary>
    [Fact]
    public void Execute_SuffixCollision_HandlesExistingSuffixGracefully()
    {
        // Arrange
        var left = new DataTable();
        left.Columns.Add("ID", typeof(int));
        left.Columns.Add("Info", typeof(string));
        left.Rows.Add(1, "LeftInfo");

        var right = new DataTable();
        right.Columns.Add("TargetID", typeof(int));
        right.Columns.Add("Info", typeof(string));       // Вызовет генерацию Info_Right
        right.Columns.Add("Info_Right", typeof(string)); // УЖЕ СУЩЕСТВУЕТ! Коварный случай.
        right.Rows.Add(1, "RightInfo", "PreExistingRight");

        var activity = new JoinDataTables
        {
            FirstTable = left,
            SecondTable = right,
            ConnectionType = JoinType.Inner,
            LeftJoinKeys = new List<string> { "ID" },
            RightJoinKeys = new List<string> { "TargetID" }
        };

        // Act
        activity.Execute(null);
        var result = activity.ResultTable;

        // Assert
        var columnNames = result.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
        
        Assert.Contains("Info", columnNames);
        Assert.Contains("Info_Right", columnNames); 
        
        var row = result.Rows[0];
        Assert.Equal("LeftInfo", row["Info"]);
    }

    /// <summary>
    /// Тест на асимметрию конфигурации: передали 2 ключа для левой таблицы и 1 для правой.
    /// Обязано упасть на взлете (до обработки данных).
    /// </summary>
    [Fact]
    public void Execute_MismatchedKeyCount_ThrowsArgumentException()
    {
        // Arrange
        var left = new DataTable();
        left.Columns.Add("K1", typeof(int));
        left.Columns.Add("K2", typeof(string));

        var right = new DataTable();
        right.Columns.Add("K1", typeof(int));

        var activity = new JoinDataTables
        {
            FirstTable = left,
            SecondTable = right,
            ConnectionType = JoinType.Inner,
            LeftJoinKeys = new List<string> { "K1", "K2" },
            RightJoinKeys = new List<string> { "K1" } // Меньше, чем слева
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => activity.Execute(null));
        Assert.Contains("Количество ключей", ex.Message);
    }

    /// <summary>
    /// Тест для Right/Full Join без совпадений. 
    /// При новой логике в результат включаются ОБЕ ключевые колонки ("L_ID" и "R_ID").
    /// Поскольку левой строки нет, "L_ID" будет содержать DBNull, а "R_ID" сохранит свое значение.
    /// </summary>
    [Fact]
    public void Execute_RightJoinWithNoMatch_PreservesBothKeyColumns()
    {
        // Arrange
        var left = new DataTable();
        left.Columns.Add("L_ID", typeof(int));
        left.Columns.Add("L_Data", typeof(string));

        var right = new DataTable();
        right.Columns.Add("R_ID", typeof(int));
        right.Columns.Add("R_Data", typeof(string));
        right.Rows.Add(999, "OrphanRight"); // Нет пары в левой таблице

        var activity = new JoinDataTables
        {
            FirstTable = left,
            SecondTable = right,
            ConnectionType = JoinType.Right,
            LeftJoinKeys = new List<string> { "L_ID" },
            RightJoinKeys = new List<string> { "R_ID" }
        };

        // Act
        activity.Execute(null);
        var result = activity.ResultTable;

        // Assert
        Assert.Single(result.Rows);
        
        // Левая ключевая колонка остается, но пустая (нет левой строки)
        Assert.Equal(DBNull.Value, result.Rows[0]["L_ID"]);
        Assert.Equal(DBNull.Value, result.Rows[0]["L_Data"]);
        
        // Правая ключевая колонка также присутствует и содержит свое значение
        Assert.Equal(999, result.Rows[0]["R_ID"]);
        Assert.Equal("OrphanRight", result.Rows[0]["R_Data"]);
    }

    /// <summary>
    /// Валидация типов должна падать, даже если в таблицах физически нет данных (0 строк), 
    /// но структуры колонок несовместимы.
    /// </summary>
    [Fact]
    public void Execute_EmptyTablesIncompatibleTypes_ThrowsArgumentException()
    {
        // Arrange
        var left = new DataTable();
        left.Columns.Add("ID", typeof(Guid)); // Ключ Guid

        var right = new DataTable();
        right.Columns.Add("ID", typeof(int)); // Ключ int
        // Данных нет в обеих таблицах

        var activity = new JoinDataTables
        {
            FirstTable = left,
            SecondTable = right,
            ConnectionType = JoinType.Inner,
            LeftJoinKeys = new List<string> { "ID" },
            RightJoinKeys = new List<string> { "ID" }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => activity.Execute(null));
        Assert.Contains("Тип поля ключа отличается", ex.Message);
    }
    
    [Fact]
    public void Execute_LeftJoin_SaveDestLeftTable_ShouldPreserveRightTableColumnsAndData()
    {
        var leftTable = new DataTable("LeftTable");
        leftTable.Columns.Add("Id", typeof(int));
        leftTable.Columns.Add("Name", typeof(string));
        leftTable.Rows.Add(1, "Alice");
        leftTable.Rows.Add(2, "Bob");

        var rightTable = new DataTable("RightTable");
        rightTable.Columns.Add("Id_r", typeof(int));
        rightTable.Columns.Add("Age", typeof(int));
        rightTable.Rows.Add(1, 30);
        rightTable.Rows.Add(2, 25);

        var activity = new JoinDataTables
        {
            FirstTable = leftTable,
            SecondTable = rightTable,
            LeftJoinKeys = new List<string> { "Id" },
            RightJoinKeys = new List<string> { "Id_r" },
            ConnectionType = JoinType.Left,
            SaveDestination = SaveResultTo.LeftTable
        };

        activity.Execute(optionID: null);

        Assert.True(
            leftTable.Columns.Contains("Age"),
            "Левая таблица должна содержать новую колонку 'Age' из правой таблицы"
        );

        Assert.Equal(2, leftTable.Rows.Count);

        Assert.False(
            leftTable.Rows[0].IsNull("Age"),
            "Значение колонки 'Age' для первой строки не должно быть null/DBNull"
        );

        Assert.Equal(30, leftTable.Rows[0]["Age"]);
        Assert.Equal(25, leftTable.Rows[1]["Age"]);

        // При новой логике правый ключ тоже остается в таблице
        Assert.Equal(4, leftTable.Columns.Count);
        Assert.False(
            leftTable.Rows[0].IsNull("Id_r"),
            "Значение колонки 'Id_r' для первой строки не должно быть null/DBNull"
        );
    }

    [Fact]
    public void Execute_LeftJoin_SaveDestLeftTable_RightTableHasOnlyKeyColumn_ShouldHandleGracefullyAndAssignResult()
    {
        var leftTable = new DataTable("LeftTable");
        leftTable.Columns.Add("Id", typeof(int));
        leftTable.Columns.Add("Name", typeof(string));
        leftTable.Columns.Add("Department", typeof(string));
        
        leftTable.Rows.Add(1, "Alice", "HR");
        leftTable.Rows.Add(2, "Bob", "IT");
        leftTable.Rows.Add(3, "Charlie", "Finance");

        var rightTable = new DataTable("RightTable");
        rightTable.Columns.Add("Id_right", typeof(int)); // Имя не совпадает с "Id", суффикс не нужен
        
        rightTable.Rows.Add(1);
        rightTable.Rows.Add(3);
        rightTable.Rows.Add(99);

        var activity = new JoinDataTables
        {
            FirstTable = leftTable,
            SecondTable = rightTable,
            LeftJoinKeys = new List<string> { "Id" },
            RightJoinKeys = new List<string> { "Id_right" },
            ConnectionType = JoinType.Left,
            SaveDestination = SaveResultTo.LeftTable
        };

        activity.Execute(optionID: null);

        Assert.Equal(3, leftTable.Rows.Count);

        Assert.Equal("Alice", leftTable.Rows[0]["Name"]);
        Assert.Equal("Bob", leftTable.Rows[1]["Name"]);
        Assert.Equal("Charlie", leftTable.Rows[2]["Name"]);
        
        // При новой логике колонка правого ключа ДОБАВЛЯЕТСЯ в результат
        Assert.Equal(4, leftTable.Columns.Count);
        Assert.True(leftTable.Columns.Contains("Id"));
        Assert.True(leftTable.Columns.Contains("Name"));
        Assert.True(leftTable.Columns.Contains("Department"));
        Assert.True(leftTable.Columns.Contains("Id_right"), 
            "При новой логике колонка правого ключа 'Id_right' должна быть добавлена в результат.");
    }
}