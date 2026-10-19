namespace Expedition.Simulation.Voyage;

/// <summary>
/// Tunable numbers for a sea passage, loaded from <c>data/voyage.json</c>: how the hull leaks, how
/// wind and rigging set the ship's speed, and the weather that drives both.
/// </summary>
public sealed record VoyageRules
{
    /// <summary>Bilge rise (percent of what she can hold before foundering) per hour with a sound hull.</summary>
    public required double BaseLeakPerHour { get; init; }

    /// <summary>Extra bilge rise per hour for each point of hull lost below 100.</summary>
    public required double LeakPerHullPointLost { get; init; }

    /// <summary>Speed fraction left with the rigging in tatters (0); full speed at 100.</summary>
    public required double RiggingSpeedFloor { get; init; }

    /// <summary>Speed multiplier when nobody is standing watch (no one at the helm or on lookout).</summary>
    public required double NoWatchSpeedMultiplier { get; init; }

    /// <summary>Effect of each sail plan on speed, wear and leaks, by plan name.</summary>
    public IReadOnlyDictionary<SailPlan, SailPlanEffect> SailPlans { get; init; } = new Dictionary<SailPlan, SailPlanEffect>
    {
        [SailPlan.PressOn] = new() { Speed = 1.2, Wear = 1.5, Leak = 1.25 },
        [SailPlan.Normal] = new(),
        [SailPlan.HoveTo] = new() { Speed = 0.08, Wear = 0.35, Leak = 0.55 },
    };

    /// <summary>The effect of a plan, or no change if the rules do not list it.</summary>
    public SailPlanEffect Plan(SailPlan plan) => SailPlans.TryGetValue(plan, out var effect) ? effect : new SailPlanEffect();

    /// <summary>Bilge levels that raise an alarm when crossed, rising.</summary>
    public IReadOnlyList<double> BilgeAlarms { get; init; } = [50, 80];

    /// <summary>Weather the voyage starts in.</summary>
    public required string StartingWeather { get; init; }

    /// <summary>Every kind of weather.</summary>
    public required IReadOnlyList<WeatherDefinition> Weathers { get; init; }

    /// <summary>Weather by ID.</summary>
    public WeatherDefinition Weather(string id) => Weathers.First(w => w.Id == id);

    /// <summary>Problems with these rules, empty if they are valid.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        var ids = Weathers.Select(w => w.Id).ToHashSet(StringComparer.Ordinal);
        if (ids.Count != Weathers.Count)
        {
            errors.Add("Voyage: weather IDs must be unique.");
        }

        if (!ids.Contains(StartingWeather))
        {
            errors.Add($"Voyage: startingWeather '{StartingWeather}' is not a weather.");
        }

        if (BaseLeakPerHour < 0 || LeakPerHullPointLost < 0 || RiggingSpeedFloor is < 0 or > 1 || NoWatchSpeedMultiplier is < 0 or > 1)
        {
            errors.Add("Voyage: leak rates must not be negative, and riggingSpeedFloor and noWatchSpeedMultiplier must be 0..1.");
        }

        foreach (var weather in Weathers)
        {
            if (weather.MinHours <= 0 || weather.MaxHours < weather.MinHours)
            {
                errors.Add($"Weather '{weather.Id}': needs 0 < minHours <= maxHours.");
            }

            if (weather.Next.Count == 0 || weather.Next.Any(n => !ids.Contains(n.Key) || n.Value <= 0))
            {
                errors.Add($"Weather '{weather.Id}': next must list known weathers with positive weights.");
            }

            if (weather.HullStrikeChancePerHour is < 0 or > 1)
            {
                errors.Add($"Weather '{weather.Id}': hullStrikeChancePerHour must be 0..1.");
            }
        }

        return errors;
    }
}

/// <summary>Multipliers a sail plan applies to speed, wear (hull and rigging, and sea strikes) and leaks.</summary>
public sealed record SailPlanEffect
{
    /// <summary>Speed multiplier.</summary>
    public double Speed { get; init; } = 1;

    /// <summary>Hull and rigging wear multiplier, also applied to the chance of a sea striking.</summary>
    public double Wear { get; init; } = 1;

    /// <summary>Leak multiplier.</summary>
    public double Leak { get; init; } = 1;
}

/// <summary>One kind of weather at sea (ART_BIBLE.md 7.2, 10).</summary>
public sealed record WeatherDefinition
{
    /// <summary>ID, for example <c>weather.gale</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Name, for example "a gale".</summary>
    public required string Name { get; init; }

    /// <summary>Leagues per hour this wind gives a sound ship with someone on watch.</summary>
    public required double WindLeaguesPerHour { get; init; }

    /// <summary>How rough the sea is: 0 calm, 1 moderate, 2 heavy, 3 very heavy. Drives the visuals.</summary>
    public required double SeaState { get; init; }

    /// <summary>How much darker and greyer the sky is, 0..1. Drives the visuals.</summary>
    public double Gloom { get; init; }

    /// <summary>Multiplies the leak rate: a working hull lets in more water in a seaway.</summary>
    public required double LeakMultiplier { get; init; }

    /// <summary>Hull points lost per hour.</summary>
    public required double HullWearPerHour { get; init; }

    /// <summary>Rigging points lost per hour.</summary>
    public required double RiggingWearPerHour { get; init; }

    /// <summary>Chance per hour of a heavy sea striking the hull and opening a seam.</summary>
    public double HullStrikeChancePerHour { get; init; }

    /// <summary>Hull points lost when a sea strikes.</summary>
    public double HullStrikeDamage { get; init; }

    /// <summary>Shortest spell of this weather, in hours.</summary>
    public required int MinHours { get; init; }

    /// <summary>Longest spell of this weather, in hours.</summary>
    public required int MaxHours { get; init; }

    /// <summary>Weather that can follow, with relative weights.</summary>
    public required IReadOnlyDictionary<string, double> Next { get; init; }
}
