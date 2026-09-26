using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Cordi.Configuration;

public enum RowSide
{
    Left = 0,
    Right = 1,
    Second = 2,
}

public enum RowFieldStyle
{
    Normal = 0,
    Muted = 1,
    Accent = 2,
    Custom = 3,
}

[Serializable]
public class RowFieldConfig
{
    public string Key { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool InHistory { get; set; } = true;
    public RowSide Side { get; set; } = RowSide.Left;
    public RowFieldStyle Style { get; set; } = RowFieldStyle.Normal;
    public Vector4 Color { get; set; } = new(1f, 1f, 1f, 1f);
    public string Format { get; set; } = string.Empty;
}

[Serializable]
public class RowLayoutConfig
{
    public List<RowFieldConfig> Fields { get; set; } = new();

    public RowFieldConfig? Find(string key) =>
        Fields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.Ordinal));

    public bool IsEnabled(string key) => Find(key) is { Enabled: true };

    public void Normalize(IReadOnlyList<RowFieldConfig> defaults)
    {
        Fields.RemoveAll(f => string.IsNullOrEmpty(f.Key) || !defaults.Any(d => d.Key == f.Key));

        foreach (var field in defaults)
        {
            var existing = Find(field.Key);

            if (existing != null)
            {
                if (string.IsNullOrEmpty(existing.Format)) existing.Format = field.Format;
                continue;
            }

            Fields.Add(new RowFieldConfig
            {
                Key = field.Key,
                Enabled = field.Enabled,
                InHistory = field.InHistory,
                Side = field.Side,
                Style = field.Style,
                Color = field.Color,
                Format = field.Format,
            });
        }
    }

    public void Move(string key, RowSide side, int index)
    {
        var field = Find(key);
        if (field == null) return;

        if (field.Enabled && field.Side == side)
        {
            int current = Fields.Where(f => f.Enabled && f.Side == side).ToList().IndexOf(field);
            if (current >= 0 && current < index) index--;
        }

        Fields.Remove(field);
        field.Side = side;

        var order = Fields.Where(f => f.Enabled && f.Side == side).ToList();
        index = Math.Clamp(index, 0, order.Count);

        int insert = index < order.Count ? Fields.IndexOf(order[index]) : Fields.Count;
        Fields.Insert(Math.Clamp(insert, 0, Fields.Count), field);

        field.Enabled = true;
    }

    public IEnumerable<RowFieldConfig> Visible(RowSide side) =>
        Fields.Where(f => f.Enabled && f.Side == side);
}
