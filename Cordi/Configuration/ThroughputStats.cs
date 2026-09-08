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

    public void IncrementChatType(XivChatType type) => Increment(ChatTypeStats, type);

    public void IncrementTell(string target) => Increment(TellStats, target);

    public void RecordPeep(string name, string world) => Record(PeepStats, name, world);

    public void RecordEmote(string name, string world) => Record(EmoteStats, name, world);

    private void Increment<TKey>(Dictionary<TKey, long> counters, TKey key) where TKey : notnull
    {
        lock (this)
        {
            counters.TryGetValue(key, out var count);
            counters[key] = count + 1;
        }
    }

    private void Record(Dictionary<string, PeeperStats> target, string name, string world)
    {
        lock (this)
        {
            var key = $"{name}@{world}";
            if (!target.TryGetValue(key, out var stats))
            {
                stats = new PeeperStats
                {
                    Name = name,
                    World = world,
                    Count = 0
                };
                target[key] = stats;
            }

            stats.Count++;
            stats.LastSeen = DateTime.Now;
        }
    }
}
