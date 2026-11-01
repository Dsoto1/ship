using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;

namespace Expedition.Simulation.World;

/// <summary>How a person walks, and so how fast: age, injury, illness and a load all slow them.</summary>
public enum Gait
{
    /// <summary>Their own walk, at the usual pace.</summary>
    Usual,

    /// <summary>Old (60 to 64): a slower, careful walk.</summary>
    Old,

    /// <summary>Very old (65 and over): slow and shaky.</summary>
    Elderly,

    /// <summary>A sprain or a wound: favouring it.</summary>
    Injured,

    /// <summary>Fever, flux, scurvy, half drowned: unsteady.</summary>
    Ill,

    /// <summary>Under a heavy load (planks, crates, casks, stone, poles).</summary>
    Laden,
}

public sealed partial class SimulationRunner
{
    private static readonly HashSet<string> HeavyLoads = ["plank", "crate", "cask"];

    // Over this much in hand ashore, a load slows the walk.
    private const double HeavyKg = 15;

    /// <summary>
    /// How this person walks now (docs/WALKS.md): the most telling of their age, an injury, an illness and a load.
    /// </summary>
    public Gait GaitOf(CharacterId id)
    {
        if (!Campaign.Characters.TryGetValue(id, out var character))
        {
            return Gait.Usual;
        }

        var conditions = character.Health.Conditions;
        if (conditions.Contains("condition.sprain") || conditions.Contains("condition.wounded"))
        {
            return Gait.Injured;
        }

        if (conditions.Contains("condition.fever") || conditions.Contains("condition.flux")
            || conditions.Contains("condition.scurvy") || conditions.Contains("condition.near_drowned"))
        {
            return Gait.Ill;
        }

        if (Laden(id))
        {
            return Gait.Laden;
        }

        return character.Age >= 65 ? Gait.Elderly : character.Age >= 60 ? Gait.Old : Gait.Usual;
    }

    /// <summary>
    /// How fast this person walks against the usual pace: 1 for most, down to 0.35. Age (0.85 from 55, 0.7 from 60,
    /// 0.4 from 65), an injury (0.6), an illness (0.8) and a heavy load (0.75) multiply together.
    /// </summary>
    public double WalkPace(CharacterId id)
    {
        if (!Campaign.Characters.TryGetValue(id, out var character))
        {
            return 1;
        }

        var pace = character.Age >= 65 ? 0.4 : character.Age >= 60 ? 0.7 : character.Age >= 55 ? 0.85 : 1;
        var conditions = character.Health.Conditions;
        if (conditions.Contains("condition.sprain") || conditions.Contains("condition.wounded"))
        {
            pace *= 0.6;
        }
        else if (conditions.Contains("condition.fever") || conditions.Contains("condition.flux")
                 || conditions.Contains("condition.scurvy") || conditions.Contains("condition.near_drowned"))
        {
            pace *= 0.8;
        }

        if (Laden(id))
        {
            pace *= 0.75;
        }

        return Math.Max(0.35, pace);
    }

    private bool Laden(CharacterId id) =>
        (Campaign.ActiveTasks.TryGetValue(id, out var task) && Content.Tasks.TryGetValue(task.DefinitionId, out var definition)
            && definition.Carries is { } load && HeavyLoads.Contains(load))
        || InHandKg(id) > HeavyKg;
}
