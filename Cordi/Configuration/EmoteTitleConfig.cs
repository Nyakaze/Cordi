using System;
using System.Collections.Generic;
using System.Numerics;

namespace Cordi.Configuration;

public enum EmoteDirection
{
    Receiving = 0,
    Giving = 1,
}

[Serializable]
public class EmoteTitleSettings
{
    public const int MinDisplaySeconds = 1;
    public const int MaxDisplaySeconds = 60;

    public bool Enabled { get; set; } = false;
    public int DisplaySeconds { get; set; } = 5;
    public bool TrackSelfEmotes { get; set; } = false;
    public List<EmoteTitleRule> Rules { get; set; } = new();
}

[Serializable]
public class EmoteTitleRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "New Rule";
    public List<ushort> EmoteIds { get; set; } = new();
    public EmoteDirection Direction { get; set; } = EmoteDirection.Receiving;
    public List<ulong> CharacterIds { get; set; } = new();
    public string Format { get; set; } = "Patted {total} times";
    public bool PrefixTitle { get; set; } = false;
    public Vector3? Color { get; set; } = null;
    public Vector3? Glow { get; set; } = null;
    public int? GradientColourSet { get; set; } = null;
    public int? GradientAnimationStyle { get; set; } = null;
}
