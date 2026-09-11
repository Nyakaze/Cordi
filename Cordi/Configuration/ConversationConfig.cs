using System;
using System.Collections.Generic;
using System.Numerics;

namespace Cordi.Configuration;

public enum ConversationTellRouting
{
    ConversationsOnly,
    Both,
    ChannelsOnly,
}

[Serializable]
public class ConversationConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public bool Open { get; set; } = true;
    public bool Pinned { get; set; }
    public long LastActivityTicks { get; set; }

    public float WindowX { get; set; }
    public float WindowY { get; set; }
    public float WindowWidth { get; set; }
    public float WindowHeight { get; set; }

    public bool HasWindowGeometry => WindowWidth > 0f && WindowHeight > 0f;

    public string Label => string.IsNullOrEmpty(World) ? Name : $"{Name}@{World}";
}

[Serializable]
public class ConversationWindowChrome : IWindowChromeConfig
{
    public bool IgnoreEsc { get; set; } = true;
    public float BackgroundOpacity { get; set; } = 1f;

    public bool HideTitleBar => false;
    public bool LockPosition => false;
    public bool LockSize => false;
}

[Serializable]
public class ConversationSettings
{
    public bool Enabled { get; set; } = true;
    public ConversationTellRouting TellRouting { get; set; } = ConversationTellRouting.ConversationsOnly;

    public bool AutoOpenIncoming { get; set; } = true;
    public bool AutoOpenOutgoing { get; set; } = true;
    public bool AutoFocusOutgoing { get; set; } = true;
    public bool ReopenOnLogin { get; set; }
    public bool OpenInOwnWindow { get; set; }
    public bool ContextMenuEntry { get; set; } = true;
    public bool SuppressGameLog { get; set; }
    public ConversationWindowChrome Window { get; set; } = new();

    public string SectionLabel { get; set; } = "Direct Messages";
    public bool ShowWorldInLabel { get; set; } = true;
    public Vector4 Color { get; set; } = new(0.36f, 0.72f, 0.98f, 1f);

    public int HistoryWindow { get; set; } = 500;
    public int HistoryPageSize { get; set; } = 500;

    public bool MuteNotifications { get; set; }
    public bool MuteGameSound { get; set; }
    public bool TreatAllAsMention { get; set; }

    public bool PlaySound { get; set; } = true;
    public uint SoundEffect { get; set; } = 1;
    public bool UseCustomSound { get; set; }
    public string CustomSoundPath { get; set; } = string.Empty;
    public float CustomSoundVolume { get; set; } = 0.6f;

    public bool Flash { get; set; } = true;
    public bool NoFlashing { get; set; }
    public int FlashPeriodMs { get; set; } = 1000;

    public bool FlashTaskbar { get; set; }
    public bool FocusGameWindow { get; set; }

    public List<ConversationConfig> Items { get; set; } = new();
}
