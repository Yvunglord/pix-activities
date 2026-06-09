using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using BR.Core;
using BR.Core.Attributes;

namespace Activities.Custom;

public enum JoinType
{
    Inner,
    Left,
    Right,
    Full
}

public enum SaveResultTo
{
    NewTable,
    LeftTable,
    RightTable
}

[ScreenName("Соединить две таблицы")]
[Representation("[FirstTable] + [SecondTable] = [ResultTable]")]
[BR.Core.Attributes.Path("Custom activities")]
public class JoinDataTables : Activity
{
    [Category("Входные данные")]
    [DisplayName("Первая (левая) таблица")]
    [IsRequired]
    public DataTable FirstTable { get; set; } = new();

    [Category("Входные данные")]
    [DisplayName("Вторая (правая) таблица")]
    [IsRequired]
    public DataTable SecondTable { get; set; } = new();

    [Category("Настройки")]
    [DisplayName("Тип соединения")]
    [IsRequired]
    public JoinType ConnectionType { get; set; } = JoinType.Inner;

    [Category("Настройки")]
    [DisplayName("Ключи соединения")]
    [IsRequired]
    public List<string> JoinKeys { get; set; } = new();

    [Category("Настройки вывода")]
    [DisplayName("Куда сохранить результат")]
    [IsRequired]
    public SaveResultTo SaveDestination { get; set; } = SaveResultTo.NewTable;

    [Category("Выходные данные")]
    [DisplayName("Результирующая таблица")]
    [IsOut]
    public DataTable ResultTable { get; set; } = new();

    public override void Execute(int? optionID)
    {
        ArgumentNullException.ThrowIfNull(FirstTable);
        ArgumentNullException.ThrowIfNull(SecondTable);

        if (JoinKeys == null || JoinKeys.Count == 0)
        {
            throw new ArgumentException(
                "Необходимо указать хотя бы один ключ для объединения.");
        }

        var joinedResult = MergeTables(
            FirstTable,
            SecondTable,
            JoinKeys,
            ConnectionType);

        switch (SaveDestination)
        {
            case SaveResultTo.NewTable:
                ResultTable = joinedResult;
                break;

            case SaveResultTo.LeftTable:
                ReplaceTableContent(FirstTable, joinedResult);
                break;

            case SaveResultTo.RightTable:
                ReplaceTableContent(SecondTable, joinedResult);
                break;
        }
    }

    private static void ReplaceTableContent(
        DataTable target,
        DataTable source)
    {
        target.Reset();

        foreach (DataColumn column in source.Columns)
        {
            target.Columns.Add(
                column.ColumnName,
                column.DataType);
        }

        foreach (DataRow row in source.Rows)
        {
            target.ImportRow(row);
        }
    }

    private DataTable MergeTables(
        DataTable left,
        DataTable right,
        IReadOnlyCollection<string> keys,
        JoinType joinType)
    {
        ValidateKeys(left, right, keys);

        var result = CreateResultStructure(left, right, keys);

        CompositeKey BuildKey(DataRow row)
        {
            return new CompositeKey(
                keys.Select(k =>
                    row.IsNull(k)
                        ? DBNull.Value
                        : row[k]));
        }

        var leftLookup = left.AsEnumerable()
            .GroupBy(BuildKey)
            .ToDictionary(
                g => g.Key,
                g => g.ToList());

        var rightLookup = right.AsEnumerable()
            .GroupBy(BuildKey)
            .ToDictionary(
                g => g.Key,
                g => g.ToList());

        var allKeys = leftLookup.Keys
            .Union(rightLookup.Keys)
            .ToList();

        void AddCombinedRow(
            DataRow? leftRow,
            DataRow? rightRow)
        {
            var newRow = result.NewRow();

            if (leftRow != null)
            {
                foreach (DataColumn column in left.Columns)
                {
                    newRow[column.ColumnName] =
                        leftRow[column];
                }
            }

            if (rightRow != null)
            {
                foreach (DataColumn column in right.Columns)
                {
                    string targetColumn;

                    if (keys.Contains(column.ColumnName))
                    {
                        targetColumn = column.ColumnName;
                    }
                    else if (left.Columns.Contains(column.ColumnName))
                    {
                        targetColumn = column.ColumnName + "_Right";
                    }
                    else
                    {
                        targetColumn = column.ColumnName;
                    }

                    newRow[targetColumn] =
                        rightRow[column];
                }
            }

            result.Rows.Add(newRow);
        }

        switch (joinType)
        {
            case JoinType.Inner:
            {
                foreach (var key in leftLookup.Keys)
                {
                    if (!rightLookup.TryGetValue(
                            key,
                            out var rightRows))
                    {
                        continue;
                    }

                    foreach (var leftRow in leftLookup[key])
                    {
                        foreach (var rightRow in rightRows)
                        {
                            AddCombinedRow(
                                leftRow,
                                rightRow);
                        }
                    }
                }

                break;
            }

            case JoinType.Left:
            {
                foreach (var pair in leftLookup)
                {
                    if (rightLookup.TryGetValue(
                            pair.Key,
                            out var rightRows))
                    {
                        foreach (var leftRow in pair.Value)
                        {
                            foreach (var rightRow in rightRows)
                            {
                                AddCombinedRow(
                                    leftRow,
                                    rightRow);
                            }
                        }
                    }
                    else
                    {
                        foreach (var leftRow in pair.Value)
                        {
                            AddCombinedRow(
                                leftRow,
                                null);
                        }
                    }
                }

                break;
            }

            case JoinType.Right:
            {
                foreach (var pair in rightLookup)
                {
                    if (leftLookup.TryGetValue(
                            pair.Key,
                            out var leftRows))
                    {
                        foreach (var rightRow in pair.Value)
                        {
                            foreach (var leftRow in leftRows)
                            {
                                AddCombinedRow(
                                    leftRow,
                                    rightRow);
                            }
                        }
                    }
                    else
                    {
                        foreach (var rightRow in pair.Value)
                        {
                            AddCombinedRow(
                                null,
                                rightRow);
                        }
                    }
                }

                break;
            }

            case JoinType.Full:
            {
                foreach (var key in allKeys)
                {
                    bool hasLeft =
                        leftLookup.TryGetValue(
                            key,
                            out var leftRows);

                    bool hasRight =
                        rightLookup.TryGetValue(
                            key,
                            out var rightRows);

                    if (hasLeft && hasRight)
                    {
                        foreach (var leftRow in leftRows!)
                        {
                            foreach (var rightRow in rightRows!)
                            {
                                AddCombinedRow(
                                    leftRow,
                                    rightRow);
                            }
                        }
                    }
                    else if (hasLeft)
                    {
                        foreach (var leftRow in leftRows!)
                        {
                            AddCombinedRow(
                                leftRow,
                                null);
                        }
                    }
                    else
                    {
                        foreach (var rightRow in rightRows!)
                        {
                            AddCombinedRow(
                                null,
                                rightRow);
                        }
                    }
                }

                break;
            }

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(joinType));
        }

        return result;
    }

    private static void ValidateKeys(
        DataTable left,
        DataTable right,
        IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            if (!left.Columns.Contains(key))
            {
                throw new ArgumentException(
                    $"Левая таблица не содержит ключ '{key}'.");
            }

            if (!right.Columns.Contains(key))
            {
                throw new ArgumentException(
                    $"Правая таблица не содержит ключ '{key}'.");
            }

            if (left.Columns[key]!.DataType !=
                right.Columns[key]!.DataType)
            {
                throw new ArgumentException(
                    $"Тип поля '{key}' отличается: " +
                    $"{left.Columns[key]!.DataType.Name} <> " +
                    $"{right.Columns[key]!.DataType.Name}");
            }
        }
    }

    private static DataTable CreateResultStructure(
        DataTable left,
        DataTable right,
        IReadOnlyCollection<string> keys)
    {
        var result = new DataTable();

        foreach (DataColumn column in left.Columns)
        {
            result.Columns.Add(
                column.ColumnName,
                column.DataType);
        }

        foreach (DataColumn column in right.Columns)
        {
            string name = column.ColumnName;

            if (result.Columns.Contains(name) &&
                !keys.Contains(name))
            {
                name += "_Right";
            }

            if (!result.Columns.Contains(name))
            {
                result.Columns.Add(
                    name,
                    column.DataType);
            }
        }

        return result;
    }
}