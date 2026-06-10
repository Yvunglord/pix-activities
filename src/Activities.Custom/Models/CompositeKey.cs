public sealed class CompositeKey : IEquatable<CompositeKey>
{
    private readonly object[] _values;

    public CompositeKey(IEnumerable<object> values)
    {
        _values = values.ToArray();
    }

    public bool Equals(CompositeKey? other)
    {
        if (other == null)
            return false;

        if (_values.Length != other._values.Length)
            return false;

        for (int i = 0; i < _values.Length; i++)
        {
            if (!Equals(_values[i], other._values[i]))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as CompositeKey);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;

            foreach (var value in _values)
            {
                hash = hash * 23 + (value?.GetHashCode() ?? 0);
            }

            return hash;
        }
    }
}