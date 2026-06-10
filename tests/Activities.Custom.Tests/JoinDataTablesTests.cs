using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Xunit;
using Activities.Custom;

namespace Activities.Custom.Tests;

public class JoinDataTablesEvilTests
{
    /// <summary>
    /// Тест проверяет декартово произведение (многие-ко-многим) для дублирующихся ключей,
    /// а также корректность сопоставления по РАЗНЫМ именам ключей.
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

        // Проверяем, что правый ключ вообще исчез из структуры, уступив место левому
        Assert.Contains("LeftKey", result.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
        Assert.DoesNotContain("RightKey", result.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
        
        // Проверяем переименование не-ключевой колонки с коллизией имён
        Assert.Contains("Payload_Right", result.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
    }

    /// <summary>
    /// Коварный тест на коллизию суффиксов.
    /// Что если в правой таблице УЖЕ есть колонка с именем "Name_Right"?
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
        // Ожидаем, что код защищён от дублирования имён (например, не добавит Info_Right дважды)
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
    /// Самый злой тест для Right/Full Join без совпадений. 
    /// Проверяет, переносятся ли значения ключевых полей из Правой таблицы в Левые ключи результирующей структуры,
    /// когда левой строки физически нет (leftRow == null).
    /// </summary>
    [Fact]
    public void Execute_RightJoinWithNoMatch_PopulatesKeyColumnsFromRightTable()
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
        
        // Ключевая колонка в результате называется "L_ID" (так как правый ключ R_ID мы дропнули).
        // Но значение туда должно прийти из правой таблицы (999), а не остаться DBNull!
        Assert.Equal(999, result.Rows[0]["L_ID"]);
        Assert.Equal(DBNull.Value, result.Rows[0]["L_Data"]);
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
}