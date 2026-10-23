using Expedition.Simulation.Characters;
using Expedition.Simulation.Shore;

namespace Expedition.Simulation.World;

/// <summary>
/// The camp's later buildings (docs/STRUCTURES.md): the chapel, where Sunday is kept; the trading house,
/// where the coast's peoples are received; the salt pans; the forge. The house and the smokehouse are in
/// People and the settlement's work.
/// </summary>
public sealed partial class SimulationRunner
{
    // The first day of the voyage, for the days of the week.
    private static readonly DateTime FirstDay = new(1501, 4, 12);

    /// <summary>Whether today is a Sunday.</summary>
    public bool Sunday => FirstDay.AddDays(Campaign.Clock.Day).DayOfWeek == DayOfWeek.Sunday;

    // Relief to everyone ashore on a Sunday kept at the chapel.
    private const double SundayRelief = 3;

    /// <summary>Whether a trading house stands.</summary>
    public bool TradingHouseStands => StandingOf(b => b.Trades).Any();

    // How much sooner a people come again when there is a trading house to receive them.
    private const double TradingHouseVisits = 0.75;

    // Salt boiled until this much is laid by.
    private const int SaltKept = 10;

    // The tools a forge makes, and how many the camp would have of each, by the company's size.
    private static readonly (string Tool, double PerHead)[] ForgedTools =
        [("item.axe", 0.25), ("item.spade", 0.2), ("item.mattock", 0.15), ("item.adze", 0.1), ("item.auger", 0.1)];

    /// <summary>The tool the camp is shortest of for its size, that the forge can make, or null if none is short.</summary>
    public string? ToolWanted()
    {
        var people = Math.Max(1, Ashore().Count(c => !IsChild(c)));
        return ForgedTools
            .Select(t => (t.Tool, Short: Math.Ceiling(people * t.PerHead) - Campaign.SharedStore.Count(t.Tool) - (Campaign.Camp?.Broken.GetValueOrDefault(t.Tool) ?? 0)))
            .Where(t => t.Short > 0).OrderByDescending(t => t.Short).ThenBy(t => t.Tool, StringComparer.Ordinal)
            .Select(t => t.Tool).FirstOrDefault();
    }

    // Sunday, with a chapel standing: Mass, or the captain's prayers, and rest. The company is easier for it.
    private void KeepSunday()
    {
        if (!Sunday || !StandingOf(b => b.Chapel).Any())
        {
            return;
        }

        foreach (var person in Ashore().Where(c => c.SurvivalState == SurvivalState.Alive))
        {
            person.Needs.Stress = Needs.Clamp(person.Needs.Stress - SundayRelief);
        }
    }
}
