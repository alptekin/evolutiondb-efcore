using System.Collections;
using System.Data.Common;

namespace evosql;

public class EvosqlParameterCollection : DbParameterCollection
{
    private readonly List<EvosqlParameter> _parameters = new();
    private readonly object _syncRoot = new();

    public override int Count => _parameters.Count;
    public override bool IsFixedSize => false;
    public override bool IsReadOnly => false;
    public override bool IsSynchronized => false;
    public override object SyncRoot => _syncRoot;

    public override int Add(object value)
    {
        _parameters.Add((EvosqlParameter)value);
        return _parameters.Count - 1;
    }

    public EvosqlParameter Add(EvosqlParameter parameter)
    {
        _parameters.Add(parameter);
        return parameter;
    }

    public override void AddRange(Array values)
    {
        foreach (EvosqlParameter p in values)
            _parameters.Add(p);
    }

    public override void Clear() => _parameters.Clear();

    public override bool Contains(object value) => _parameters.Contains((EvosqlParameter)value);

    public override bool Contains(string value) => IndexOf(value) != -1;

    public override void CopyTo(Array array, int index) =>
        ((ICollection)_parameters).CopyTo(array, index);

    public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();

    public override int IndexOf(object value) => _parameters.IndexOf((EvosqlParameter)value);

    public override int IndexOf(string parameterName) =>
        _parameters.FindIndex(p => string.Equals(p.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));

    public override void Insert(int index, object value) =>
        _parameters.Insert(index, (EvosqlParameter)value);

    public override void Remove(object value) => _parameters.Remove((EvosqlParameter)value);

    public override void RemoveAt(int index) => _parameters.RemoveAt(index);

    public override void RemoveAt(string parameterName)
    {
        var index = IndexOf(parameterName);
        if (index >= 0) _parameters.RemoveAt(index);
    }

    protected override DbParameter GetParameter(int index) => _parameters[index];

    protected override DbParameter GetParameter(string parameterName)
    {
        var index = IndexOf(parameterName);
        if (index < 0)
            throw new KeyNotFoundException($"Parameter '{parameterName}' not found.");
        return _parameters[index];
    }

    protected override void SetParameter(int index, DbParameter value) =>
        _parameters[index] = (EvosqlParameter)value;

    protected override void SetParameter(string parameterName, DbParameter value)
    {
        var index = IndexOf(parameterName);
        if (index < 0)
            throw new KeyNotFoundException($"Parameter '{parameterName}' not found.");
        _parameters[index] = (EvosqlParameter)value;
    }
}
