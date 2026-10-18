namespace Expedition.Simulation.Core;

/// <summary>
/// Separate deterministic random streams, one per kind of randomness (TECHNICAL_SPEC.md §24).
/// Each stream is its own PCG32 sequence derived from the campaign seed, so adding rolls to one
/// domain (for example, weather) never shifts the results of another (for example, task injuries).
/// </summary>
public sealed class RandomStreams
{
    /// <summary>Task outcomes: injuries and, later, work quality.</summary>
    public const string TasksName = "tasks";

    /// <summary>Character generation: candidates, appearance.</summary>
    public const string CharactersName = "characters";

    /// <summary>Weather and sea state.</summary>
    public const string WeatherName = "weather";

    /// <summary>Events and encounters.</summary>
    public const string EventsName = "events";

    /// <summary>Every stream name, in save order.</summary>
    public static readonly IReadOnlyList<string> Names = [TasksName, CharactersName, WeatherName, EventsName];

    private readonly Dictionary<string, SimulationRandom> _streams;

    /// <summary>Creates every stream from <paramref name="seed"/>.</summary>
    public RandomStreams(ulong seed)
    {
        _streams = Names.ToDictionary(name => name, name => new SimulationRandom(seed, StreamNumber(name)), StringComparer.Ordinal);
    }

    private RandomStreams(Dictionary<string, SimulationRandom> streams) => _streams = streams;

    /// <summary>Stream for task outcomes.</summary>
    public SimulationRandom Tasks => _streams[TasksName];

    /// <summary>Stream for character generation.</summary>
    public SimulationRandom Characters => _streams[CharactersName];

    /// <summary>Stream for weather.</summary>
    public SimulationRandom Weather => _streams[WeatherName];

    /// <summary>Stream for events.</summary>
    public SimulationRandom Events => _streams[EventsName];

    /// <summary>Saved state of every stream, by name.</summary>
    public IReadOnlyDictionary<string, (ulong State, ulong Increment)> SaveState() =>
        Names.ToDictionary(name => name, name => _streams[name].SaveState(), StringComparer.Ordinal);

    /// <summary>Recreates the streams from <see cref="SaveState"/> output. Every stream must be present.</summary>
    /// <exception cref="ArgumentException">A stream is missing, unknown or corrupt.</exception>
    public static RandomStreams FromState(IReadOnlyDictionary<string, (ulong State, ulong Increment)> states)
    {
        var unknown = states.Keys.Where(name => !Names.Contains(name)).ToList();
        var missing = Names.Where(name => !states.ContainsKey(name)).ToList();
        if (unknown.Count > 0 || missing.Count > 0)
        {
            throw new ArgumentException(
                $"Random streams do not match: missing [{string.Join(", ", missing)}], unknown [{string.Join(", ", unknown)}].",
                nameof(states));
        }

        return new RandomStreams(Names.ToDictionary(
            name => name, name => SimulationRandom.FromState(states[name].State, states[name].Increment), StringComparer.Ordinal));
    }

    /// <summary>A fixed PCG stream number per name (FNV-1a), so streams never depend on declaration order.</summary>
    internal static ulong StreamNumber(string name)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in name)
        {
            hash = unchecked((hash ^ c) * 1099511628211UL);
        }

        return hash;
    }
}
