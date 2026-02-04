using System;
using System.Collections.Generic;
using Dalamud.Game.Text;

namespace Cordi.Configuration;

[Serializable]
public class ThroughputStats
{
    public long TotalMessages { get; set; } = 0;
    public long TotalPeepsTracked { get; set; } = 0;
    public long TotalEmotesTracked { get; set; } = 0;


    public Dictionary<XivChatType, long> ChatTypeStats { get; set; } = new();
    public Dictionary<string, long> TellStats { get; set; } = new();
    public Dictionary<string, PeeperStats> PeepStats { get; set; } = new();
    public Dictionary<string, PeeperStats> EmoteStats { get; set; } = new();


    public void IncrementTotal()
    {
        lock (this)
        {
            TotalMessages++;
        }
    }

    public void IncrementPeepsTracked()
    {
        lock (this)
        {
            TotalPeepsTracked++;
        }
    }

    public void IncrementEmotesTracked()
    {
        lock (this)
        {
            TotalEmotesTracked++;
        }
    }

    public void IncrementChatType(XivChatType type)
    {
        lock (this)
        {
            if (!ChatTypeStats.ContainsKey(type)) ChatTypeStats[type] = 0;
            ChatTypeStats[type]++;
        }
    }

    public void IncrementTell(string target)
    {
        lock (this)
        {
            if (!TellStats.ContainsKey(target)) TellStats[target] = 0;
            TellStats[target]++;
        }
    }

    public void RecordPeep(string name, string world)
    {
        lock (this)
        {
            var key = $"{name}@{world}";
            if (!PeepStats.TryGetValue(key, out var stats))
            {
                stats = new PeeperStats
                {
                    Name = name,
                    World = world,
                    Count = 0
                };
                PeepStats[key] = stats;
            }

            stats.Count++;
            stats.LastSeen = DateTime.Now;
        }
    }
    public void RecordEmote(string name, string world)
    {
        lock (this)
        {
            var key = $"{name}@{world}";
            if (!EmoteStats.TryGetValue(key, out var stats))
            {
                stats = new PeeperStats
                {
                    Name = name,
                    World = world,
                    Count = 0
                };
                EmoteStats[key] = stats;
            }

            stats.Count++;
            stats.LastSeen = DateTime.Now;
        }
    }
}
