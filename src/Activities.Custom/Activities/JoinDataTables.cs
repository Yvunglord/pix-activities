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
    [DisplayName("Ключи левой таблицы")]
    [IsRequired]
    public List<string> LeftJoinKeys { get; set; } = new();

    [Category("Настройки")]
    [DisplayName("Ключи правой таблицы")]
    [IsRequired]
    public List<string> RightJoinKeys { get; set; } = new();

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

        if (LeftJoinKeys == null || LeftJoinKeys.Count == 0 || RightJoinKeys == null || RightJoinKeys.Count == 0)
        {
            throw new ArgumentException("Необходимо указать ключи для обеих таблиц.");
        }

        if (LeftJoinKeys.Count != RightJoinKeys.Count)
        {
            throw new ArgumentException("Количество ключей в левой и правой таблицах должно совпадать.");
        }

        var joinedResult = MergeTables(
            FirstTable,
            SecondTable,
            LeftJoinKeys,
            RightJoinKeys,
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

    private static void ReplaceTableContent(DataTable target, DataTable source)
    {
        // Полностью очищаем данные, старые связи и ограничения
        target.Constraints.Clear();
        target.Rows.Clear();
        target.Columns.Clear(); // <-- Гарантированно удаляет ВСЕ колонки

        // Строим новую схему на основе результатов объединения
        foreach (DataColumn column in source.Columns)
        {
            target.Columns.Add(column.ColumnName, column.DataType);
        }

        // Импортируем строки
        foreach (DataRow row in source.Rows)
        {
            target.ImportRow(row);
        }
    }

    private DataTable MergeTables(
        DataTable left,
        DataTable right,
        List<string> leftKeys,
        List<string> rightKeys,
        JoinType joinType)
    {
        ValidateKeys(left, right, leftKeys, rightKeys);

        var result = CreateResultStructure(left, right, leftKeys, rightKeys);

        // Функция сборки композитного ключа для Левой таблицы
        CompositeKey BuildLeftKey(DataRow row)
        {
            return new CompositeKey(leftKeys.Select(k => row.IsNull(k) ? DBNull.Value : row[k]));
        }

        // Функция сборки композитного ключа для Правой таблицы
        CompositeKey BuildRightKey(DataRow row)
        {
            return new CompositeKey(rightKeys.Select(k => row.IsNull(k) ? DBNull.Value : row[k]));
        }

        var leftLookup = left.AsEnumerable()
            .GroupBy(BuildLeftKey)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rightLookup = right.AsEnumerable()
            .GroupBy(BuildRightKey)
            .ToDictionary(g => g.Key, g => g.ToList());

        var allKeys = leftLookup.Keys
            .Union(rightLookup.Keys)
            .ToList();

        void AddCombinedRow(DataRow? leftRow, DataRow? rightRow)
        {
            var newRow = result.NewRow();

            // Заполняем данные из левой таблицы
            if (leftRow != null)
            {
                foreach (DataColumn column in left.Columns)
                {
                    newRow[column.ColumnName] = leftRow[column];
                }
            }

            // Заполняем данные из правой таблицы
            if (rightRow != null)
            {
                // Если левой строки нет (например, при Right/Full Join), 
                // нам нужно перенести значения ключей из правой таблицы в соответствующие колонки результирующей таблицы
                if (leftRow == null)
                {
                    for (int i = 0; i < rightKeys.Count; i++)
                    {
                        string targetLeftKeyName = leftKeys[i];
                        newRow[targetLeftKeyName] = rightRow[rightKeys[i]];
                    }
                }

                foreach (DataColumn column in right.Columns)
                {
                    // Ключевую колонку правой таблицы пропускаем, так как её роль уже выполняет левая ключевая колонка
                    if (rightKeys.Contains(column.ColumnName))
                    {
                        continue;
                    }

                    string targetColumn;
                    // Если имя колонки совпадает с колонкой из левой таблицы, добавляем суффикс
                    if (left.Columns.Contains(column.ColumnName))
                    {
                        targetColumn = column.ColumnName + "_Right";
                    }
                    else
                    {
                        targetColumn = column.ColumnName;
                    }

                    newRow[targetColumn] = rightRow[column];
                }
            }

            result.Rows.Add(newRow);
        }

        switch (joinType)
        {
            case JoinType.Inner:
                foreach (var key in leftLookup.Keys)
                {
                    if (!rightLookup.TryGetValue(key, out var rightRows))
                    {
                        continue;
                    }

                    foreach (var leftRow in leftLookup[key])
                    {
                        foreach (var rightRow in rightRows)
                        {
                            AddCombinedRow(leftRow, rightRow);
                        }
                    }
                }
                break;

            case JoinType.Left:
                foreach (var pair in leftLookup)
                {
                    if (rightLookup.TryGetValue(pair.Key, out var rightRows))
                    {
                        foreach (var leftRow in pair.Value)
                        {
                            foreach (var rightRow in rightRows)
                            {
                                AddCombinedRow(leftRow, rightRow);
                            }
                        }
                    }
                    else
                    {
                        foreach (var leftRow in pair.Value)
                        {
                            AddCombinedRow(leftRow, null);
                        }
                    }
                }
                break;

            case JoinType.Right:
                foreach (var pair in rightLookup)
                {
                    if (leftLookup.TryGetValue(pair.Key, out var leftRows))
                    {
                        foreach (var rightRow in pair.Value)
                        {
                            foreach (var leftRow in leftRows)
                            {
                                AddCombinedRow(leftRow, rightRow);
                            }
                        }
                    }
                    else
                    {
                        foreach (var rightRow in pair.Value)
                        {
                            AddCombinedRow(null, rightRow);
                        }
                    }
                }
                break;

            case JoinType.Full:
                foreach (var key in allKeys)
                {
                    bool hasLeft = leftLookup.TryGetValue(key, out var leftRows);
                    bool hasRight = rightLookup.TryGetValue(key, out var rightRows);

                    if (hasLeft && hasRight)
                    {
                        foreach (var leftRow in leftRows!)
                        {
                            foreach (var rightRow in rightRows!)
                            {
                                AddCombinedRow(leftRow, rightRow);
                            }
                        }
                    }
                    else if (hasLeft)
                    {
                        foreach (var leftRow in leftRows!)
                        {
                            AddCombinedRow(leftRow, null);
                        }
                    }
                    else
                    {
                        foreach (var rightRow in rightRows!)
                        {
                            AddCombinedRow(null, rightRow);
                        }
                    }
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(joinType));
        }

        return result;
    }

    private static void ValidateKeys(
        DataTable left,
        DataTable right,
        List<string> leftKeys,
        List<string> rightKeys)
    {
        for (int i = 0; i < leftKeys.Count; i++)
        {
            string leftKey = leftKeys[i];
            string rightKey = rightKeys[i];

            if (!left.Columns.Contains(leftKey))
            {
                throw new ArgumentException($"Левая таблица не содержит ключ '{leftKey}'.");
            }

            if (!right.Columns.Contains(rightKey))
            {
                throw new ArgumentException($"Правая таблица не содержит ключ '{rightKey}'.");
            }

            if (left.Columns[leftKey]!.DataType != right.Columns[rightKey]!.DataType)
            {
                throw new ArgumentException(
                    $"Тип поля ключа отличается: " +
                    $"Левый '{leftKey}' ({left.Columns[leftKey]!.DataType.Name}) <> " +
                    $"Правый '{rightKey}' ({right.Columns[rightKey]!.DataType.Name})");
            }
        }
    }

    private static DataTable CreateResultStructure(
        DataTable left,
        DataTable right,
        List<string> leftKeys,
        List<string> rightKeys)
    {
        var result = new DataTable();

        // Добавляем все колонки из левой таблицы
        foreach (DataColumn column in left.Columns)
        {
            result.Columns.Add(column.ColumnName, column.DataType);
        }

        // Добавляем колонки из правой таблицы
        foreach (DataColumn column in right.Columns)
        {
            string name = column.ColumnName;

            // Если колонка является правым ключом соединения, пропускаем её 
            // (в результирующей таблице за неё отвечает колонка левого ключа)
            if (rightKeys.Contains(name))
            {
                continue;
            }

            // Коллизия имён: если колонка с таким же именем уже есть из левой таблицы, добавляем суффикс
            if (result.Columns.Contains(name))
            {
                name += "_Right";
            }

            if (!result.Columns.Contains(name))
            {
                result.Columns.Add(name, column.DataType);
            }
        }

        return result;
    }
}