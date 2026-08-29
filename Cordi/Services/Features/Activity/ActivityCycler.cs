using System;
using Crovus.Models;

namespace Cordi.Services.Activity;

public sealed class ActivityCycler
{
    private const string LogSource = "Activity";

    private ActivityType? _type;
    private DateTime _lastSwap = DateTime.MinValue;
    private int _index = -1;

    public int Index => _index;

    public void Advance(ActivityCandidate candidate, CordiLogService? log)
    {
        var config = candidate.Config;

        if (!config.EnableCycling || config.CycleFormats is not { Count: > 0 })
        {
            Reset();
            return;
        }

        if (_type != candidate.Type)
        {
            _type = candidate.Type;
            _lastSwap = DateTime.Now;
            _index = -1;

            log?.Debug(LogSource, $"Cycling reset for new activity type {candidate.Type}.");
        }

        if ((DateTime.Now - _lastSwap).TotalSeconds < config.CycleIntervalSeconds) return;

        _index++;

        if (_index >= config.CycleFormats.Count) _index = -1;

        _lastSwap = DateTime.Now;

        log?.Debug(LogSource, $"Cycle advanced to index {_index}.");
    }

    public void Reset()
    {
        _type = null;
        _index = -1;
        _lastSwap = DateTime.MinValue;
    }
}
