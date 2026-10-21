using System.Globalization;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Knowledge;
using Expedition.Simulation.Port;

namespace Expedition.Simulation.World;

/// <summary>
/// Evenings in the Three Anchors: dice with whoever is drinking there, and a song when the captain calls
/// for one. Both take their time as the clock runs: a game holds the table for half an hour, a song lasts
/// twenty minutes and the singer is seen singing it. Both are ways to come to know people:
/// men talk over the dice, and a song says something of the singer.
/// </summary>
public sealed partial class SimulationRunner
{
    /// <summary>Minutes a game of dice takes.</summary>
    public const int DiceMinutes = 30;

    /// <summary>Minutes a song takes, with the talk after.</summary>
    public const int SongMinutes = 20;

    /// <summary>What the singer is given for a song.</summary>
    public const int SongPrice = 5;

    /// <summary>The stakes a game can be played for.</summary>
    public static readonly IReadOnlyList<int> Stakes = [10, 20, 50];

    private double HourNow => Campaign.Clock.TimeOfDay.TotalHours;

    // Evening hours, past midnight included: from <paramref name="from"/> to <paramref name="to"/> next morning.
    private bool Evening(double from, double to) => HourNow >= from || HourNow < to;

    /// <summary>Who in the captain's room would sit down to dice: gamblers first, then drinkers (three at most).</summary>
    public IReadOnlyList<CharacterId> DicePartners()
    {
        if (CaptainRoom is not { Round: not null } room)
        {
            return [];
        }

        return Campaign.Characters.Values
            .Where(c => c.Membership == Membership.Local && c.PersonId is not null && RoomOf(c.Id) == room
                        && Campaign.Placements.TryGetValue(c.Id, out var p) && !p.IsMoving
                        && Campaign.ActiveTasks.TryGetValue(c.Id, out var task) && task.DefinitionId is "task.gamble" or "task.drink")
            .OrderBy(c => Campaign.ActiveTasks[c.Id].DefinitionId == "task.gamble" ? 0 : 1).ThenBy(c => c.Id)
            .Take(3).Select(c => c.Id).ToList();
    }

    /// <summary>
    /// Sits in at dice for <paramref name="stake"/> a throw with whoever will play. Everyone throws two
    /// dice; the highest takes the pot (a tie is thrown again). Sitting down with them warms them to the
    /// captain; losing gracefully more so, winning their money less. Over the dice one of them may talk
    /// of someone else. Half an hour goes by.
    /// </summary>
    public (CommandResult Result, string Said) PlayDice(int stake)
    {
        if (Campaign.Port is not { } port || Campaign.CaptainId is not { } captain || CaptainRoom is not { Round: not null } room)
        {
            return (CommandResult.Rejected("There is no dicing here."), "");
        }

        if (!Evening(17, 2))
        {
            return (CommandResult.Rejected("No one dices this early. Come back in the evening."), "");
        }

        if (Campaign.Chronicle.Entries.LastOrDefault(e => e.Kind == ChronicleEntryKind.DicePlayed) is { } game && Campaign.Clock.Tick - game.Tick < DiceMinutes)
        {
            return (CommandResult.Rejected("The game is still going round the table."), "");
        }

        if (!Stakes.Contains(stake))
        {
            return (CommandResult.Rejected($"The stakes here are {string.Join(", ", Stakes)} sueldos."), "");
        }

        var players = DicePartners();
        if (players.Count == 0)
        {
            return (CommandResult.Rejected("No one here is in the mood for dice."), "");
        }

        if (port.Funds < stake)
        {
            return (CommandResult.Rejected($"Only {port.Funds} sueldos left: not enough for the stake."), "");
        }

        // The throws: the same each time for the same moment, so a game is never rerolled by reloading.
        var dice = new SimulationRandom(Campaign.Seed ^ (ulong)Campaign.Clock.Tick ^ 0x6469_6365UL);
        int Throw() => dice.NextInt(6) + 1 + dice.NextInt(6) + 1;
        var throwers = new List<CharacterId>(players) { captain };
        var throws = throwers.ToDictionary(t => t, _ => Throw());
        for (var again = 0; again < 3; again++)
        {
            var best = throws.Values.Max();
            var tied = throws.Where(t => t.Value == best).Select(t => t.Key).ToList();
            if (tied.Count == 1)
            {
                break;
            }

            foreach (var t in tied)
            {
                throws[t] = Throw() + 12 * (again + 1);
            }
        }

        var winner = throws.MaxBy(t => t.Value).Key;
        var names = players.Select(p => Campaign.Characters[p].Name).ToList();
        var said = new List<string> { $"You sit in with {Join(names)}, {stake} sueldos a throw." };
        int won;
        if (winner == captain)
        {
            won = stake * players.Count;
            port.Funds += won;
            said.Add($"The bones run your way: you take the pot, {won} sueldos. {Campaign.Characters[players[0]].Name} pushes the coins over with a sour look.");
        }
        else
        {
            won = -stake;
            port.Funds -= stake;
            said.Add($"{Campaign.Characters[winner].Name} throws high and sweeps up the pot. You lose {stake}, and take it well; it is remembered.");
        }

        foreach (var player in players)
        {
            var affection = winner == captain ? 1 : player == winner ? 4 : 3;
            Campaign.ChangeRelationship(player, captain, RelationshipDimension.Affection, affection, "diced with the captain");
        }

        // Over the dice, talk: someone says what they know of someone the captain has not asked them about.
        foreach (var player in players.OrderBy(_ => dice.NextInt(100)))
        {
            if (dice.NextInt(100) >= 60 || PersonOf(player) is not { } person)
            {
                continue;
            }

            var talk = person.Knows.FirstOrDefault(k => CharacterFor(k.About) is { } about && !port.Asked.Contains((player, about.Id)));
            if (talk is not null && CharacterFor(talk.About) is { } subject)
            {
                port.MarkAsked(player, subject.Id);
                foreach (var (topic, level) in talk.Levels.OrderBy(c => c.Key, StringComparer.Ordinal))
                {
                    Campaign.Learn(subject.Id, topic, level, KnowledgeSource.Rumour, player);
                }

                if (talk.Fact is { } fact)
                {
                    Campaign.Learn(subject.Id, fact, 1, KnowledgeSource.Rumour, player);
                }

                said.Add($"Between throws, {Campaign.Characters[player].Name} talks of {subject.Name}: \"{talk.Text}\"");
                break;
            }
        }

        Campaign.Record(ChronicleEntryKind.DicePlayed, [new(ChronicleParameters.Level, won.ToString(CultureInfo.InvariantCulture))], [.. players]);
        return (CommandResult.Ok(), string.Join("\n\n", said));
    }

    /// <summary>
    /// Calls for a song, once an evening, and gives the singer a few coins: someone in the room who has a
    /// song of their own, and has not sung it yet, stands and sings. The room warms to the captain; the
    /// singer more. What the song shows of them (a tongue they claimed to speak) the captain hears for
    /// himself. Twenty minutes go by.
    /// </summary>
    public (CommandResult Result, string Said) CallForSong()
    {
        if (Campaign.Port is not { } port || Campaign.CaptainId is not { } captain || CaptainRoom is not { Round: not null } room)
        {
            return (CommandResult.Rejected("No one sings here."), "");
        }

        if (!Evening(19, 1))
        {
            return (CommandResult.Rejected("It is too early for singing. The room fills after dark."), "");
        }

        var evening = (Campaign.Clock.Tick - 6 * SimulationClock.TicksPerHour) / SimulationClock.TicksPerDay;
        if (port.LastSong == evening)
        {
            return (CommandResult.Rejected("There has been a song tonight already. Come back tomorrow evening."), "");
        }

        if (port.Funds < SongPrice)
        {
            return (CommandResult.Rejected("You have nothing left to give a singer."), "");
        }

        var present = Campaign.Characters.Values
            .Where(c => c.Membership == Membership.Local && c.PersonId is not null && RoomOf(c.Id) == room
                        && Campaign.Placements.TryGetValue(c.Id, out var p) && !p.IsMoving)
            .OrderBy(c => c.Id).ToList();
        var singer = present.FirstOrDefault(c => PersonOf(c.Id)?.Song is not null && !port.Sung.Contains(c.Id))
                     ?? present.FirstOrDefault(c => PersonOf(c.Id)?.Song is not null);
        if (singer is null || PersonOf(singer.Id)?.Song is not { } song)
        {
            return (CommandResult.Rejected("No one here will sing tonight. Try again when the room is fuller."), "");
        }

        port.Funds -= SongPrice;
        port.LastSong = evening;
        port.MarkSung(singer.Id);
        foreach (var listener in present.Where(c => c.Id != singer.Id))
        {
            Campaign.ChangeRelationship(listener.Id, captain, RelationshipDimension.Affection, 2, "heard a song the captain called for");
        }

        Campaign.ChangeRelationship(singer.Id, captain, RelationshipDimension.Affection, 5, "sang for the captain");
        Campaign.ChangeRelationship(singer.Id, captain, RelationshipDimension.Trust, 3, "sang for the captain");
        foreach (var (topic, level) in song.Shows.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            Campaign.Learn(singer.Id, topic, level, KnowledgeSource.Observation);
        }

        Campaign.Record(ChronicleEntryKind.SongSung, [new(ChronicleParameters.To, song.Title)], singer.Id);
        var words = song.Lines.Count == 0 ? "" : "\n\n" + string.Join("\n", song.Lines.Select(l => $"    {l}"));
        return (CommandResult.Ok(), $"You put {SongPrice} sueldos on the table and call for a song. {song.Says}{words}");
    }

    /// <summary>Who is at dice with the captain just now (for the view): the last game's players, for its length.</summary>
    public IReadOnlyList<CharacterId> Dicing =>
        Campaign.Chronicle.Entries.LastOrDefault(e => e.Kind == ChronicleEntryKind.DicePlayed) is { } last && Campaign.Clock.Tick - last.Tick < DiceMinutes
            ? last.Characters
            : [];

    /// <summary>Who is singing just now (for the view), or null: the last singer, for the song's length.</summary>
    public CharacterId? Singing =>
        Campaign.Chronicle.Entries.LastOrDefault(e => e.Kind == ChronicleEntryKind.SongSung) is { } last
        && Campaign.Clock.Tick - last.Tick < SongMinutes && last.Characters.Count > 0
            ? last.Characters[0]
            : null;
}
