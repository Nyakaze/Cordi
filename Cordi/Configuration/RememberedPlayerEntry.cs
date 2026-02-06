using System;

namespace Cordi.Configuration;

[Serializable]
public class RememberedPlayerEntry
{
    public string LodestoneId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public DateTime LastSeen { get; set; } = DateTime.Now;
    public string Notes { get; set; } = string.Empty;
    public PlayerGlamour? Glamour { get; set; }


    public RememberedPlayerEntry() { }

    public RememberedPlayerEntry(string name, string world, string lodestoneId = "")
    {
        Name = name;
        World = world;
        LodestoneId = lodestoneId;
        LastSeen = DateTime.Now;
    }

    public string FullName => $"{Name}@{World}";

    public string GetLastSeenRelative()
    {
        var timeSpan = DateTime.Now - LastSeen;

        if (timeSpan.TotalMinutes < 1)
            return "Just now";
        if (timeSpan.TotalHours < 1)
            return $"{(int)timeSpan.TotalMinutes} minute{((int)timeSpan.TotalMinutes != 1 ? "s" : "")} ago";
        if (timeSpan.TotalDays < 1)
            return $"{(int)timeSpan.TotalHours} hour{((int)timeSpan.TotalHours != 1 ? "s" : "")} ago";
        if (timeSpan.TotalDays < 7)
            return $"{(int)timeSpan.TotalDays} day{((int)timeSpan.TotalDays != 1 ? "s" : "")} ago";
        if (timeSpan.TotalDays < 30)
            return $"{(int)(timeSpan.TotalDays / 7)} week{((int)(timeSpan.TotalDays / 7) != 1 ? "s" : "")} ago";
        if (timeSpan.TotalDays < 365)
            return $"{(int)(timeSpan.TotalDays / 30)} month{((int)(timeSpan.TotalDays / 30) != 1 ? "s" : "")} ago";

        return $"{(int)(timeSpan.TotalDays / 365)} year{((int)(timeSpan.TotalDays / 365) != 1 ? "s" : "")} ago";
    }
}
