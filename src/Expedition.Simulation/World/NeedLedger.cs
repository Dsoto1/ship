using Expedition.Simulation.Core;

namespace Expedition.Simulation.World;

/// <summary>A need or health value whose changes are tracked for explanations.</summary>
public enum TrackedNeed
{
    /// <summary>Hunger.</summary>
    Hunger,

    /// <summary>Thirst.</summary>
    Thirst,

    /// <summary>Fatigue.</summary>
    Fatigue,

    /// <summary>Stress.</summary>
    Stress,

    /// <summary>Health (losses are negative).</summary>
    Health,
}

/// <summary>Total change to one need from one source over the explanation window.</summary>
/// <param name="Source">
/// What caused it: <see cref="NeedLedger.Baseline"/>, a task ID (work), an item ID (eating or drinking),
/// a condition ID (injury), or a cause such as <c>starvation</c>.
/// </param>
/// <param name="Amount">Net change in need points; positive makes hunger, thirst, fatigue or stress worse.</param>
public sealed record NeedChange(string Source, double Amount);

/// <summary>
/// Diagnostic record of what changed each character's needs and health over the last
/// <see cref="WindowHours"/> in-game hours, answering questions such as "why is this character
/// exhausted?" (TECHNICAL_SPEC.md §23.1).
/// </summary>
/// <remarks>
/// Changes are kept in hourly buckets, so the window is the current (partial) hour plus the
/// <see cref="WindowHours"/> - 1 full hours before it: between 23 and 24 hours of history.
/// Amounts are the contributions each source made before needs were clamped to 0..100. The ledger is
/// a diagnostic, not game state: it is not saved, so it starts empty after loading and fills again as
/// time passes.
/// </remarks>
public sealed class NeedLedger
{
    /// <summary>How far back explanations look, in hourly buckets including the current one.</summary>
    public const int WindowHours = 24;

    /// <summary>Source name for changes that happen to everyone just by living through the hours.</summary>
    public const string Baseline = "baseline";

    private readonly Dictionary<CharacterId, Bucket[]> _buckets = [];

    /// <summary>
    /// Changes to <paramref name="need"/> for <paramref name="characterId"/> over the last
    /// <see cref="WindowHours"/> hours before <paramref name="now"/>, largest effect first.
    /// </summary>
    public IReadOnlyList<NeedChange> Explain(CharacterId characterId, TrackedNeed need, long now)
    {
        if (!_buckets.TryGetValue(characterId, out var buckets))
        {
            return [];
        }

        var currentHour = now / SimulationClock.TicksPerHour;
        var totals = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var bucket in buckets)
        {
            if (bucket is null || bucket.Hour <= currentHour - WindowHours)
            {
                continue;
            }

            foreach (var ((trackedNeed, source), amount) in bucket.Amounts)
            {
                if (trackedNeed == need)
                {
                    totals[source] = totals.GetValueOrDefault(source) + amount;
                }
            }
        }

        return totals
            .Where(pair => Math.Abs(pair.Value) > 1e-9)
            .OrderByDescending(pair => Math.Abs(pair.Value))
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new NeedChange(pair.Key, pair.Value))
            .ToList();
    }

    /// <summary>Records a change. Called by <see cref="SimulationRunner"/> only.</summary>
    internal void Add(long tick, CharacterId characterId, TrackedNeed need, string source, double amount)
    {
        if (amount == 0)
        {
            return;
        }

        if (!_buckets.TryGetValue(characterId, out var buckets))
        {
            buckets = new Bucket[WindowHours];
            _buckets[characterId] = buckets;
        }

        var hour = tick / SimulationClock.TicksPerHour;
        var index = (int)(hour % WindowHours);
        var bucket = buckets[index];
        if (bucket is null || bucket.Hour != hour)
        {
            bucket = new Bucket(hour);
            buckets[index] = bucket;
        }

        var key = (need, source);
        bucket.Amounts[key] = bucket.Amounts.GetValueOrDefault(key) + amount;
    }

    private sealed class Bucket(long hour)
    {
        public long Hour { get; } = hour;

        public Dictionary<(TrackedNeed Need, string Source), double> Amounts { get; } = [];
    }
}
