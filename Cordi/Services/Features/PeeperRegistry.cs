using System;
using System.Collections.Generic;

using Cordi.Services;

namespace Cordi.Services.Features;

public sealed class PeeperSnapshot
{
    public static readonly PeeperSnapshot Empty =
        new(Array.Empty<PeeperState>(), Array.Empty<PeeperState>());

    public PeeperSnapshot(IReadOnlyList<PeeperState> active, IReadOnlyList<PeeperState> history)
    {
        Active = active;
        History = history;
    }

    public IReadOnlyList<PeeperState> Active { get; }

    public IReadOnlyList<PeeperState> History { get; }

    public bool IsEmpty => Active.Count == 0 && History.Count == 0;
}

public sealed class PeeperRegistry
{
    private const int HistoryLimit = 50;

    private readonly object _gate = new();
    private readonly Dictionary<ulong, PeeperState> _active = new();
    private readonly List<PeeperState> _history = new();

    private PeeperSnapshot _snapshot = PeeperSnapshot.Empty;

    public PeeperSnapshot Snapshot
    {
        get { lock (_gate) return _snapshot; }
    }

    public bool TryGetActive(ulong id, out PeeperState state)
    {
        lock (_gate) return _active.TryGetValue(id, out state!);
    }

    public PeeperState? FindTracked(ulong id, string name, string world)
    {
        lock (_gate)
        {
            if (_active.TryGetValue(id, out var active)) return active;

            foreach (var entry in _history)
            {
                if (Matches(entry, id, name, world)) return entry;
            }

            return null;
        }
    }

    public PeeperState? TakeFromHistory(ulong id, string name, string world)
    {
        lock (_gate)
        {
            for (var i = 0; i < _history.Count; i++)
            {
                if (!Matches(_history[i], id, name, world)) continue;

                var entry = _history[i];
                _history.RemoveAt(i);
                return entry;
            }

            return null;
        }
    }

    public void Add(PeeperState state)
    {
        lock (_gate) _active[state.GameObjectId] = state;
    }

    public void Retire(PeeperState state)
    {
        lock (_gate)
        {
            _active.Remove(state.GameObjectId);
            _history.Insert(0, state);
            if (_history.Count > HistoryLimit) _history.RemoveAt(_history.Count - 1);
        }
    }

    public List<PeeperState> TakeLost(HashSet<ulong> present)
    {
        var lost = new List<PeeperState>();

        lock (_gate)
        {
            foreach (var (id, state) in _active)
            {
                if (state.IsLooking && !present.Contains(id)) lost.Add(state);
            }
        }

        return lost;
    }

    public PeeperState? FindByMessageId(ulong messageId)
    {
        lock (_gate)
        {
            foreach (var state in _active.Values)
            {
                if (state.MessageId == messageId) return state;
            }

            return null;
        }
    }

    public void EndTick(long tick)
    {
        lock (_gate)
        {
            var active = new PeeperState[_active.Count];
            var index = 0;

            foreach (var state in _active.Values)
            {
                ApplyPresence(state, tick);
                active[index++] = state;
            }

            var history = new PeeperState[_history.Count];

            for (var i = 0; i < _history.Count; i++)
            {
                ApplyPresence(_history[i], tick);
                history[i] = _history[i];
            }

            _snapshot = new PeeperSnapshot(active, history);
        }
    }

    private static void ApplyPresence(PeeperState state, long tick)
    {
        state.IsPresent = state.SeenTick == tick;
        if (state.IsPresent) return;

        state.CurrentTargetId = 0;
        state.CurrentTargetName = null;
        state.Resolved = null;
    }

    private static bool Matches(PeeperState state, ulong id, string name, string world) =>
        state.GameObjectId == id || (state.Name == name && state.World == world);
}
