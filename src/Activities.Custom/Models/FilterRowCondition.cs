using Activities.Custom.Activities;
using System.ComponentModel;

namespace Activities.Custom.Models;

public class FilterRowCondition
{
    [DisplayName("Имя столбца")]
    public string ColumnName { get; set; } = string.Empty;

    [DisplayName("Операция")]
    public FilterOperator Operator { get; set; } = FilterOperator.Equals;

    [DisplayName("Значение")]
    public object? Value { get; set; }
}