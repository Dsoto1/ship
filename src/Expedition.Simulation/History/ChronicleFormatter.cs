using System.Globalization;
using System.Text.RegularExpressions;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;

namespace Expedition.Simulation.History;

/// <summary>
/// Turns chronicle entries into English text, resolving IDs to current names. This is the single
/// place chronicle wording lives, so a later localisation layer replaces this class and nothing else.
/// </summary>
public sealed partial class ChronicleFormatter(CampaignState campaign, GameContent content)
{
    /// <summary>Readable text for <paramref name="entry"/>.</summary>
    public string Format(ChronicleEntry entry)
    {
        if (entry.Get(ChronicleParameters.LegacyText) is { } legacy)
        {
            return legacy;
        }

        var who = entry.Characters.Count > 0 ? Name(entry.Characters[0]) : "Someone";
        var cause = Cause(entry.Get(ChronicleParameters.Cause));
        return entry.Kind switch
        {
            ChronicleEntryKind.PhaseChanged =>
                $"The expedition moved from {Phase(entry.Get(ChronicleParameters.From))} to {Phase(entry.Get(ChronicleParameters.To))}: {cause}.",
            ChronicleEntryKind.TransitionRejected =>
                $"Could not move from {Phase(entry.Get(ChronicleParameters.From))} to {Phase(entry.Get(ChronicleParameters.To))} ({cause}).",
            ChronicleEntryKind.SurvivalStateChanged => entry.Get(ChronicleParameters.To) switch
            {
                nameof(SurvivalState.Dead) => $"{who} died ({cause}).",
                nameof(SurvivalState.Alive) => $"{who} is able to act again ({cause}).",
                var state => $"{who} is now {Words(state)} ({cause}).",
            },
            ChronicleEntryKind.Injury =>
                $"{who} suffered {Condition(entry.Get(ChronicleParameters.Condition))} while doing {Task(entry.Get(ChronicleParameters.Task))} " +
                $"(-{entry.Get(ChronicleParameters.Damage)} health, now {entry.Get(ChronicleParameters.Health)}){Because(entry)}.",
            ChronicleEntryKind.TaskInterrupted =>
                $"{who} stopped {Task(entry.Get(ChronicleParameters.Task))}: {InterruptReason(entry, cause)}.",
            ChronicleEntryKind.TaskPaused =>
                $"{who} paused {Task(entry.Get(ChronicleParameters.Task))} to {Task(entry.Get(ChronicleParameters.PausedBy))}.",
            ChronicleEntryKind.TaskDropped =>
                $"{who} could not start {Task(entry.Get(ChronicleParameters.Task))}: {cause}",
            ChronicleEntryKind.StoresSpoiled => Spoiled(entry),
            ChronicleEntryKind.SickenedByRation =>
                $"{who} is down with {Condition(entry.Get(ChronicleParameters.Condition)).ToLowerInvariant()}, from the {ItemName(entry.Get(ChronicleParameters.To)).ToLowerInvariant()}.",
            ChronicleEntryKind.Recovered => $"{who} is over {Condition(entry.Get(ChronicleParameters.Condition)).ToLowerInvariant()}.",
            ChronicleEntryKind.SupplyShortage => entry.Get(ChronicleParameters.Need) == "water"
                ? $"{who} went thirsty: there is nothing left to drink."
                : $"{who} went hungry: there is no food left.",
            ChronicleEntryKind.RelationshipChanged => Relationship(entry, who, cause),
            ChronicleEntryKind.CaptainAppointed => $"{who} took command of the expedition ({cause}).",
            ChronicleEntryKind.WeatherChanged => WeatherChange(entry),
            ChronicleEntryKind.EventResolved => EventLine(entry),
            ChronicleEntryKind.AllHandsLost => $"No one is left alive aboard {Ship()}.",
            ChronicleEntryKind.Noticed => $"You noticed something about {who}: {Secret(entry.Get(ChronicleParameters.To))}",
            ChronicleEntryKind.LeftPort => $"{who} {entry.Get(ChronicleParameters.Reason)}.",
            ChronicleEntryKind.ConvoyWarning => $"The Chamber's notice: the convoy sails at dawn, in {entry.Get(ChronicleParameters.Level)} hours.",
            ChronicleEntryKind.LeadHeard => Lead(entry.Get(ChronicleParameters.To)) is { } lead
                ? $"Heard of someone worth finding: \"{lead.Text}\" ({lead.Where})"
                : "Heard of someone worth finding.",
            ChronicleEntryKind.DicePlayed => int.TryParse(entry.Get(ChronicleParameters.Level), out var won) && won >= 0
                ? $"Dice in the Three Anchors: you won {won} sueldos."
                : $"Dice in the Three Anchors: you lost {-int.Parse(entry.Get(ChronicleParameters.Level) ?? "0", System.Globalization.CultureInfo.InvariantCulture)} sueldos.",
            ChronicleEntryKind.SongSung => $"{who} sang \"{entry.Get(ChronicleParameters.To)}\" in the Three Anchors.",
            ChronicleEntryKind.RoundBought => $"You stood {entry.Get(ChronicleParameters.To)} a round, {entry.Get(ChronicleParameters.Level)} sueldos.",
            ChronicleEntryKind.LateFee => $"The convoy has gone without {Ship()}. Harbour fees and the Company's displeasure: {entry.Get(ChronicleParameters.Level)} sueldos.",
            ChronicleEntryKind.CrewHired => int.TryParse(entry.Get(ChronicleParameters.Level), out var paid) && paid < 0
                ? $"{who} signed on, paying {-paid} sueldos for the passage."
                : $"{who} signed on for an advance of {entry.Get(ChronicleParameters.Level)} sueldos.",
            ChronicleEntryKind.SailPlanChanged => entry.Get(ChronicleParameters.To) switch
            {
                "PressOn" => "The captain ordered every sail set: press on.",
                "HoveTo" => "The captain ordered the ship hove to, to ride it out.",
                _ => "The captain ordered sail set for the weather.",
            },
            ChronicleEntryKind.RationingChanged => entry.Get(ChronicleParameters.To) switch
            {
                "short" => "The captain put the crew on short rations.",
                "emergency" => "The captain put the crew on emergency rations: water once a day, food every other day.",
                _ => "The crew are back on full rations.",
            },
            ChronicleEntryKind.MilestoneReached => entry.Get(ChronicleParameters.To) switch
            {
                "water" => "The camp has reliable water of its own.",
                "storehouse" => "The first storehouse stands: the stores are off the ground and dry.",
                "homes" => "There are dry berths in huts for everyone ashore.",
                "harvest" => "The first harvest is in.",
                "tool" => "A broken tool was mended at the camp instead of lost.",
                "apprentice" => "Someone has been taught a skill ashore: the camp trains its own.",
                "trade" => "A people of the coast trust the camp and trade with it.",
                "season" => "The first season is survived.",
                var other => $"A milestone: {other}.",
            },
            ChronicleEntryKind.Joined => entry.Get(ChronicleParameters.From) switch
            {
                "relief" => $"{who} came ashore from the relief ship, to settle.",
                "brennish" => $"{who} came over from the Brennish ship, to stay.",
                var from => $"{who} of {CommunityName(from)} came to live at the camp.",
            },
            ChronicleEntryKind.HouseholdFormed => entry.Get(ChronicleParameters.From) is { } customs
                ? $"{who} and {Other(entry)} made a home together, as is the custom of {CommunityName(customs)}."
                : $"{who} and {Other(entry)} made a home together.",
            ChronicleEntryKind.Expecting => $"{who} is expecting a child.",
            ChronicleEntryKind.ChildBorn => $"A child was born to {Other(entry)}: {who}.",
            ChronicleEntryKind.WentHome => $"{who} took passage home on the relief ship.",
            ChronicleEntryKind.HullStruck =>
                $"A heavy sea struck {Ship()} and opened a seam (hull down {entry.Get(ChronicleParameters.Damage)}, now {entry.Get(ChronicleParameters.Level)}%).",
            ChronicleEntryKind.RiggingCarriedAway => entry.Get(ChronicleParameters.To) == "yard"
                ? $"A squall sprang the main topsail yard on {Ship()}; the topsail cannot be set until it is fished (rigging now {entry.Get(ChronicleParameters.Level)}%)."
                : $"A line parted aloft on {Ship()} and flies loose (rigging now {entry.Get(ChronicleParameters.Level)}%).",
            ChronicleEntryKind.ClaimTested => ClaimLine(who, entry),
            ChronicleEntryKind.NoonSight => $"Noon sight by {who}: latitude {entry.Get(ChronicleParameters.Level)}.",
            ChronicleEntryKind.BilgeAlarm => $"The bilge is {entry.Get(ChronicleParameters.Level)}% full and rising. Man the pumps!",
            ChronicleEntryKind.Foundered => $"{Capitalise(entry.Get(ChronicleParameters.To) ?? "The ship")} filled and foundered.",
            ChronicleEntryKind.CoastNear => $"By the reckoning the coast is {entry.Get(ChronicleParameters.Level)} leagues off. Heave the lead, and keep a good lookout.",
            ChronicleEntryKind.LandSign => entry.Get(ChronicleParameters.To) switch
            {
                "birds" => "Land birds about the ship, flying west at dusk.",
                "weed" => "Weed in the water, and a drifting branch with its leaves still green.",
                "water" => "The sea has changed colour: greener, and cloudy.",
                _ => "A bank of cloud stands still in the west all day, as cloud does over land.",
            },
            ChronicleEntryKind.Sounding => entry.Get(ChronicleParameters.Level) == "none"
                ? $"{who} heaved the deep-sea lead: no bottom at {Coast()?.ShelfFathoms ?? 100:0} fathoms."
                : $"{who} heaved the lead: {entry.Get(ChronicleParameters.Level)} fathoms, {entry.Get(ChronicleParameters.To)}.",
            ChronicleEntryKind.LandSighted => $"Land! The coast raised from the masthead, some {entry.Get(ChronicleParameters.Level)} leagues off.",
            ChronicleEntryKind.LandmarkSighted => $"In view: {Coast()?.Find(entry.Get(ChronicleParameters.To))?.Looks ?? "a mark on the coast."}",
            ChronicleEntryKind.CoastIdentified => $"{who} took the coast for {Coast()?.Find(entry.Get(ChronicleParameters.To))?.Name ?? "a mark on the chart"}.",
            ChronicleEntryKind.NotTheDestination =>
                $"Standing in where {Coast()?.Find(entry.Get(ChronicleParameters.To))?.Name ?? "the destination"} should be: no ruin on the headland, no lagoon behind. It is not here.",
            ChronicleEntryKind.StruckTheCoast =>
                $"She struck in the dark, unseen surf and then rock, and was got off with the hull stove (down {entry.Get(ChronicleParameters.Damage)}, now {entry.Get(ChronicleParameters.Level)}%).",
            ChronicleEntryKind.AllHands => entry.Get(ChronicleParameters.To) switch
            {
                "called" => "All hands! Both watches called on deck.",
                "turnedOut" => entry.Get(ChronicleParameters.Reason) == "storm"
                    ? "The storm: the watch below turned out, all hands on deck."
                    : "The pumps are losing: the watch below turned out to them.",
                "stoodDown" => "Pumped out and the worst past: the watch below goes below again.",
                _ => "All hands dismissed: the watch below goes below.",
            },
            ChronicleEntryKind.StormWarning => $"{StormSigns(entry.Get(ChronicleParameters.To))} {(entry.Characters.Count > 0 ? $"{who} says it will blow hard before long." : "")}".Trim(),
            ChronicleEntryKind.Prepared => entry.Get(ChronicleParameters.To) switch
            {
                "aloft" => $"{who} and the topmen struck the topmasts and sent down the topsail yard.",
                "cargo" => $"{who} lashed and chocked the cargo in the hold.",
                _ => $"{who} battened down the hatches and covered the gratings.",
            },
            ChronicleEntryKind.PreparationsUndone => "Fair again: the topmasts swayed up, the lashings cast off, the hatches opened.",
            ChronicleEntryKind.LeakSprung => $"A heavy sea sprang a leak {entry.Get(ChronicleParameters.To)}.",
            ChronicleEntryKind.LeakPlugged => $"{who} plugged the leak {entry.Get(ChronicleParameters.To)} with oakum and a wedge: it will hold, for now.",
            ChronicleEntryKind.LeakPatched => $"{who} made good the leak {entry.Get(ChronicleParameters.To)} with plank and oakum.",
            ChronicleEntryKind.LeakReopened => $"The plug {entry.Get(ChronicleParameters.To)} burst in the seas: the leak is open again.",
            ChronicleEntryKind.CargoShifted => $"Cargo not lashed broke loose in the hold and shifted: {entry.Get(ChronicleParameters.Level)} of {ItemWord(entry.Get(ChronicleParameters.To))} lost, the hull strained.",
            ChronicleEntryKind.StoresWetted => $"Water down the open hatches wetted the stores: {entry.Get(ChronicleParameters.Level)} of {ItemWord(entry.Get(ChronicleParameters.To))} spoiled.",
            ChronicleEntryKind.Wrecked => (entry.Get(ChronicleParameters.Cause) == "struck"
                    ? $"She broke up on the coast. {entry.Get(ChronicleParameters.Need)} came ashore at {entry.Get(ChronicleParameters.To)}" +
                      (entry.Get(ChronicleParameters.Reason) == "boat" ? " in the boat." : ", the boat stove, as best they could.")
                    : $"She went down. {entry.Get(ChronicleParameters.Need)} in the boat reached {entry.Get(ChronicleParameters.To)} after {entry.Get(ChronicleParameters.Level)} days.") ,
            ChronicleEntryKind.IslandTrade =>
                $"Bought from the people of {Island(entry.Get(ChronicleParameters.From))?.Name ?? "the island"}: {entry.Get(ChronicleParameters.Level)} {ItemWord(entry.Get(ChronicleParameters.To))}, for {entry.Get(ChronicleParameters.Need)} sueldos. " +
                $"{Island(entry.Get(ChronicleParameters.From))?.Sells.FirstOrDefault(g => g.Item == entry.Get(ChronicleParameters.To))?.Looks}".TrimEnd(),
            ChronicleEntryKind.MakingForIsland => $"The captain left the passage and made for {IslandName(entry)}.",
            ChronicleEntryKind.IslandSighted => $"Land! {Capitalise(IslandName(entry))} raised from the masthead, some {entry.Get(ChronicleParameters.Level)} leagues off. " +
                                                $"{Island(entry)?.Looks}",
            ChronicleEntryKind.Anchored => entry.Get(ChronicleParameters.Reason) == "fix"
                ? $"At anchor off {IslandName(entry)}. Her place is known again: the reckoning is put right."
                : $"At anchor off {IslandName(entry)}: it is there after all, if not quite where it was drawn.",
            ChronicleEntryKind.IslandNotFound => $"Where the chart puts {IslandName(entry)}, an empty sea all round from the masthead. It is given up.",
            ChronicleEntryKind.WeighedAnchor => entry.Get(ChronicleParameters.Reason) == "weighed"
                ? $"Anchor weighed off {IslandName(entry)}: back to the passage."
                : $"The captain gave up {IslandName(entry)} and went back to the passage.",
            ChronicleEntryKind.Landfall => $"Landfall: {entry.Get(ChronicleParameters.To) ?? "the coast"} reached, and known for what it is.",
            ChronicleEntryKind.FellIll => $"{who} is down with {Condition(entry.Get(ChronicleParameters.Condition)).ToLowerInvariant()}{Because(entry)}.",
            ChronicleEntryKind.Healed => $"{who}'s {Condition(entry.Get(ChronicleParameters.Condition)).ToLowerInvariant()} has healed.",
            ChronicleEntryKind.SicknessSpreads =>
                $"Sickness spreads: {entry.Get(ChronicleParameters.Level)} down, most with {Condition(entry.Get(ChronicleParameters.Condition)).ToLowerInvariant()}" +
                (Reasons(entry.Get(ChronicleParameters.Reason)) is { Length: > 0 } why ? $": {why}" : "") +
                $". The ship is kept {entry.Get(ChronicleParameters.Health)}% clean.",
            ChronicleEntryKind.GrievanceRose => entry.Get(ChronicleParameters.To) switch
            {
                nameof(GrievanceStage.Concern) => $"{who} is muttering about {Grievance(entry)}.",
                nameof(GrievanceStage.Complaint) => $"{who} means to bring {Grievance(entry)} to the captain.",
                nameof(GrievanceStage.Refusal) => $"{who} refuses to turn to over {Grievance(entry)}: they will pump to save their lives, and nothing more.",
                _ => $"{who}'s grievance over {Grievance(entry)} grows.",
            },
            ChronicleEntryKind.GrievanceEased => entry.Get(ChronicleParameters.To) == nameof(GrievanceStage.None)
                ? $"{who} has let it go: {Grievance(entry)} no longer rankles."
                : $"{who} has turned to again; {Grievance(entry)} still rankles.",
            ChronicleEntryKind.FactionFormed =>
                $"A party forms round {who} over {Grievance(entry)}: {string.Join(", ", entry.Characters.Skip(1).Select(Name))} with them.",
            ChronicleEntryKind.PromiseKept => $"The captain kept his word over {World.SimulationRunner.GrievanceWords(entry.Get(ChronicleParameters.Reason) ?? "")}.",
            ChronicleEntryKind.PromiseBroken => $"The captain's word over {World.SimulationRunner.GrievanceWords(entry.Get(ChronicleParameters.Reason) ?? "")} was not kept, and the crew remember it.",
            ChronicleEntryKind.SkillImproved =>
                $"{who} has grown handier at {SkillWord(entry.Get(ChronicleParameters.To))}: now {LevelWord(entry.Get(ChronicleParameters.Level))}" +
                (entry.Characters.Count > 1 ? $", taught by {Name(entry.Characters[1])}." : ", by practice."),
            ChronicleEntryKind.MentorshipBegun =>
                $"{who} is learning {SkillWord(entry.Get(ChronicleParameters.To))} from {(entry.Characters.Count > 1 ? Name(entry.Characters[1]) : "a shipmate")}.",
            ChronicleEntryKind.CargoInjury =>
                $"Cargo breaking loose caught {who}: {Condition(entry.Get(ChronicleParameters.Condition)).ToLowerInvariant()} (-{entry.Get(ChronicleParameters.Damage)} health){Because(entry)}.",
            ChronicleEntryKind.LegPlotted => $"The captain laid off a leg on the chart: {entry.Get(ChronicleParameters.Level)} laid off.",
            ChronicleEntryKind.WentAshore => entry.Get(ChronicleParameters.Reason) == "anchored"
                ? $"Ashore at {entry.Get(ChronicleParameters.To)}: {entry.Get(ChronicleParameters.Need)} landed through the surf to make a camp, the ship at anchor off."
                : $"A camp made at {entry.Get(ChronicleParameters.To)} by the {entry.Get(ChronicleParameters.Need)} who came ashore.",
            ChronicleEntryKind.CampMoved =>
                $"The camp moved from {entry.Get(ChronicleParameters.From)} to {entry.Get(ChronicleParameters.To)}: {entry.Get(ChronicleParameters.Need)} went, carrying {entry.Get(ChronicleParameters.Level)} kg of the stores.",
            ChronicleEntryKind.CampWorkDone => entry.Get(ChronicleParameters.To) switch
            {
                Shore.CampWorks.Shelter => $"A shelter finished: berths under cover for {entry.Get(ChronicleParameters.Level)}.",
                Shore.CampWorks.Fire => "A fire made on the beach, above the tide.",
                Shore.CampWorks.Cover => "The stores stacked off the sand and covered.",
                Shore.CampWorks.Latrine => "A latrine dug below the camp.",
                Shore.CampWorks.SickShelter => "A shelter made for the sick.",
                var other => $"Finished: {other}.",
            },
            ChronicleEntryKind.CampFound => Capitalise(entry.Get(ChronicleParameters.Reason) switch
            {
                "water" => $"Water found: {entry.Get(ChronicleParameters.To)}.",
                "food" => $"Food found: {entry.Get(ChronicleParameters.To)}.",
                _ => entry.Get(ChronicleParameters.Need) ?? $"Found: {entry.Get(ChronicleParameters.To)}.",
            }),
            ChronicleEntryKind.CampStoresWet => "Rain on the stores, uncovered on the sand: food is spoiling.",
            ChronicleEntryKind.CampAllLanded => $"The last of the stores landed, in {entry.Get(ChronicleParameters.Level)} trips.",
            ChronicleEntryKind.CampFirstDays => string.IsNullOrEmpty(entry.Get(ChronicleParameters.Reason))
                ? $"Three days ashore. The {entry.Get(ChronicleParameters.Need)} have water, shelter and a fire."
                : $"Three days ashore. The {entry.Get(ChronicleParameters.Need)} still want: {entry.Get(ChronicleParameters.Reason)}.",
            ChronicleEntryKind.ProjectBegun => $"Ground marked out for {Article(BuildingName(entry.Get(ChronicleParameters.To)))}.",
            ChronicleEntryKind.BuildingFinished => $"{Capitalise(Article(BuildingName(entry.Get(ChronicleParameters.To))))} finished: {QualityWord(entry.Get(ChronicleParameters.Level))} work.",
            ChronicleEntryKind.BuildingDown => entry.Get(ChronicleParameters.To) switch
            {
                "shelter.sail" => "The wind tore down a sail shelter.",
                "shelter.brush" => "The wind tore down a brush shelter.",
                var building => $"The {BuildingName(building).ToLowerInvariant()} came down in the weather.",
            },
            ChronicleEntryKind.ToolBroke => $"{Capitalise(Article(ItemWord(entry.Get(ChronicleParameters.To)).ToLowerInvariant()))} broke in the work.",
            ChronicleEntryKind.ToolMended => $"A broken {ItemWord(entry.Get(ChronicleParameters.To)).ToLowerInvariant()} mended in the workshop.",
            ChronicleEntryKind.ToolLost => $"A broken {ItemWord(entry.Get(ChronicleParameters.To)).ToLowerInvariant()} could not be saved.",
            ChronicleEntryKind.Sown => $"A bed sown with {CropName(entry.Get(ChronicleParameters.To))}.",
            ChronicleEntryKind.Harvested => entry.Get(ChronicleParameters.Level) == "0"
                ? $"The {CropName(entry.Get(ChronicleParameters.To))} brought in: nothing worth the name."
                : $"The {CropName(entry.Get(ChronicleParameters.To))} brought in: {entry.Get(ChronicleParameters.Level)} measures.",
            ChronicleEntryKind.CropFailed => $"A bed of {CropName(entry.Get(ChronicleParameters.To))} has failed" + entry.Get(ChronicleParameters.Reason) switch
            {
                "wet" => ", rotted in the wet.",
                "untended" => ", choked with weeds.",
                _ => ": it would not take in this ground.",
            },
            ChronicleEntryKind.CoastNews => entry.Get(ChronicleParameters.Need) ?? "",
            ChronicleEntryKind.Learned => $"Learned from {CommunityName(entry.Get(ChronicleParameters.From))}: {entry.Get(ChronicleParameters.Need)}.",
            ChronicleEntryKind.CharterJudged => entry.Get(ChronicleParameters.Outcome) switch
            {
                "kept" => $"The relief ship has come, and the charter is kept: {entry.Get(ChronicleParameters.Level)} logs of embrawood go aboard her.",
                "in part" => $"The relief ship has come. The charter is kept in part: {entry.Get(ChronicleParameters.Level)} logs go aboard, and the Company will want {entry.Get(ChronicleParameters.Reason)}.",
                _ => $"The relief ship has come, and the Company's charter is revoked{(string.IsNullOrEmpty(entry.Get(ChronicleParameters.Reason)) ? "" : $": wanting {entry.Get(ChronicleParameters.Reason)}")}.",
            },
            ChronicleEntryKind.WetSeason => entry.Get(ChronicleParameters.To) == "dry"
                ? "The rains are over: the dry season has come again."
                : "The wet season has come: rain most days now, the ground turning to mud.",
            ChronicleEntryKind.StormAshore => "A storm, the change of the season: wind and rain off the sea, and no work out of doors.",
            ChronicleEntryKind.SeasonJudged => entry.Get(ChronicleParameters.Outcome) == "survived"
                ? $"The first season is survived: {entry.Get(ChronicleParameters.Need)} alive, dry, fed and watered."
                : $"The first season has gone hard: {entry.Get(ChronicleParameters.Need)} alive, and still wanting {entry.Get(ChronicleParameters.Reason)}.",
            ChronicleEntryKind.LegReached => entry.Get(ChronicleParameters.Level) == "0"
                ? "She reached the end of the last leg laid off, by the reckoning: back on the passage's own course."
                : $"She reached a leg's end, by the reckoning; {entry.Get(ChronicleParameters.Level)} more laid off.",
            ChronicleEntryKind.StandingOrderActed => entry.Get(ChronicleParameters.Reason) switch
            {
                Voyage.StandingOrderReasons.HoveTo => $"By the captain's standing order, the officer of the watch hove her to: {WeatherName(entry.Get(ChronicleParameters.To))}.",
                Voyage.StandingOrderReasons.MadeSail => "The blow easing, the officer of the watch made sail again, as ordered.",
                Voyage.StandingOrderReasons.Eased => $"By standing order, sail was taken in from pressing on: {WeatherName(entry.Get(ChronicleParameters.To))}.",
                Voyage.StandingOrderReasons.Preparing => "By standing order, hands were set to make ready for heavy weather.",
                Voyage.StandingOrderReasons.AllHands => $"By standing order, all hands were called: the bilge at {entry.Get(ChronicleParameters.To)}%.",
                Voyage.StandingOrderReasons.Dismissed => "The bilge pumped down, all hands were dismissed, as ordered.",
                Voyage.StandingOrderReasons.Rations => $"By standing order, the crew were put on short rations: the stores at about {entry.Get(ChronicleParameters.To)} days.",
                var other => $"A standing order was acted on ({other}).",
            },
            ChronicleEntryKind.CaptainCalled => entry.Get(ChronicleParameters.Reason) switch
            {
                Voyage.StandingOrderReasons.WakeStores => $"The captain was called: the {entry.Get(ChronicleParameters.Need) ?? "stores"} will last only about {entry.Get(ChronicleParameters.Level)} days.",
                Voyage.StandingOrderReasons.WakeBilge => $"The captain was called: the bilge at {entry.Get(ChronicleParameters.Level)}%.",
                Voyage.StandingOrderReasons.WakeDoubt => $"The captain was called: the reckoning may be out by {entry.Get(ChronicleParameters.Level)} leagues {(entry.Get(ChronicleParameters.Need) == "across" ? "east or west" : "north or south")}.",
                Voyage.StandingOrderReasons.WakeNoon => "The captain was called: noon, and the sun to take.",
                Voyage.StandingOrderReasons.WakeWind => $"The captain was called: the wind has shifted {entry.Get(ChronicleParameters.Level)}°, and is now from the {entry.Get(ChronicleParameters.To)}.",
                var other => $"The captain was called ({other}).",
            },
            _ => $"{entry.Kind}: {string.Join(", ", entry.Parameters.Select(p => $"{p.Key}={p.Value}"))}",
        };
    }

    private Voyage.CoastRules? Coast() => content.Voyage?.Route?.Coast;

    private string ItemWord(string? id) => id is not null && content.Items.TryGetValue(id, out var item) ? item.Name.ToLowerInvariant() : "the stores";

    private static string StormSigns(string? coming) => coming == "weather.storm"
        ? "The sun went down red in a brassy haze, the swell heavier by the hour and the birds gone: something bad is coming."
        : "A long swell rising out of the south-west, and mares' tails high up: wind coming."; 

    private Voyage.Island? Island(ChronicleEntry entry) => Island(entry.Get(ChronicleParameters.To));

    private Voyage.Island? Island(string? id) => content.Voyage?.Route?.Islands.FirstOrDefault(i => i.Id == id);

    private string IslandName(ChronicleEntry entry) => Island(entry)?.Name ?? "the island";

    /// <summary>The name of a kind of weather, for example "a gale".</summary>
    public string WeatherName(string? id) =>
        id is not null && content.Voyage?.Weathers.FirstOrDefault(w => w.Id == id) is { } weather ? weather.Name : Prettify(id ?? "weather");

    private string WeatherChange(ChronicleEntry entry)
    {
        var to = entry.Get(ChronicleParameters.To);
        var wind = content.Voyage?.Weathers.FirstOrDefault(w => w.Id == to)?.WindLeaguesPerHour ?? 1;
        var from = content.Voyage?.Weathers.FirstOrDefault(w => w.Id == entry.Get(ChronicleParameters.From))?.WindLeaguesPerHour ?? 1;
        var verb = to is "weather.gale" or "weather.storm" ? "The weather worsened" : wind > from ? "The wind rose" : "The wind eased";
        return $"{verb}: {WeatherName(to)}.";
    }

    // "Man overboard! Heave to and lower the boat: Pedro was hauled back aboard, half drowned."
    private string EventLine(ChronicleEntry entry)
    {
        if (!content.Events.TryGetValue(entry.Get(ChronicleParameters.Event) ?? "", out var definition))
        {
            return $"An event ({entry.Get(ChronicleParameters.Event)}) was decided.";
        }

        var choice = definition.Choices.FirstOrDefault(c => c.Id == entry.Get(ChronicleParameters.Choice));
        var outcome = choice is null ? null : entry.Get(ChronicleParameters.Outcome) == "failure" && choice.Failure is not null ? choice.Failure : choice.Success;
        CharacterId? subject = entry.Characters.Count > 0 ? entry.Characters[0] : null;
        CharacterId? other = entry.Characters.Count > 1 ? entry.Characters[1] : null;
        string Fill(string text) => EventText.Fill(text, campaign, subject, other);
        return choice is null || outcome is null
            ? Fill(definition.Title)
            : $"{Fill(definition.Title)} {Fill(choice.Text)}: {Fill(outcome.Text)}";
    }

    private string Secret(string? id) =>
        id is not null && content.Port?.People.Select(p => p.FactText(id)).FirstOrDefault(t => t is not null) is { Length: > 0 } text
            ? char.ToLowerInvariant(text[0]) + text[1..]
            : "something odd.";

    private string ItemName(string? id) => id is not null && content.Items.TryGetValue(id, out var item) ? item.Name : id ?? "stores";

    // Found at sea: biscuit with weevils, wine gone sour, pork gone green, water gone foul.
    private string Spoiled(ChronicleEntry entry)
    {
        var id = entry.Get(ChronicleParameters.To);
        var name = ItemName(id).ToLowerInvariant();
        var lost = entry.Get(ChronicleParameters.Level);
        return id is not null && content.Items.TryGetValue(id, out var item) && item.ThirstRelief > 0
            ? $"The {name} has turned: {lost} poured over the side, and more will follow."
            : $"Weevils and rot in the {name}: {lost} thrown over the side, and more will follow.";
    }

    private Port.LeadDefinition? Lead(string? id) =>
        content.Port?.AllLeads().Select(l => l.Lead).FirstOrDefault(l => l.Id == id);

    private string ClaimLine(string who, ChronicleEntry entry)
    {
        var topic = entry.Get(ChronicleParameters.Need) ?? "";
        var skill = content.Skills.TryGetValue(topic, out var definition) ? definition.Name.ToLowerInvariant()
            : content.Languages.TryGetValue(topic, out var language) ? language.Name : topic;
        var told = int.Parse(entry.Get(ChronicleParameters.From) ?? "0", CultureInfo.InvariantCulture);
        var seen = int.Parse(entry.Get(ChronicleParameters.Level) ?? "0", CultureInfo.InvariantCulture);
        var (toldWord, seenWord) = (KnowledgeText.LevelWord(told).ToLowerInvariant(), KnowledgeText.LevelWord(seen).ToLowerInvariant());
        return seen < told
            ? $"At sea, {who}'s {skill} shows for what it is: {seenWord}, not the {toldWord} you were told. The crew have seen it too."
            : $"At sea, {who} turns out better at {skill} than you were told: {seenWord}, not {toldWord}. The crew have noticed.";
    }

    private string Ship() => campaign.Voyage?.ShipName ?? "the ship";

    private static string Capitalise(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>Readable name for a <see cref="World.NeedChange"/> source, for "why" explanations.</summary>
    public string DescribeNeedSource(string source) => source switch
    {
        World.NeedLedger.Baseline => "daily life",
        ChronicleCauses.Starvation => "starvation",
        ChronicleCauses.Dehydration => "dehydration",
        _ when content.Tasks.TryGetValue(source, out var task) => task.Name.ToLowerInvariant(),
        _ when content.Items.TryGetValue(source, out var item) => item.Name,
        _ when content.Conditions.TryGetValue(source, out var condition) => condition.Name.ToLowerInvariant(),
        _ => Prettify(source),
    };

    private string Relationship(ChronicleEntry entry, string who, string cause)
    {
        var whom = entry.Characters.Count > 1 ? Name(entry.Characters[1]) : "someone";
        var delta = int.TryParse(entry.Get(ChronicleParameters.Delta), NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ? d : 0;
        var change = delta >= 0 ? $"rose by {delta}" : $"fell by {-delta}";
        var dimension = Words(entry.Get(ChronicleParameters.Dimension));
        var preposition = dimension == "trust" ? "in" : "for";
        return $"{who}'s {dimension} {preposition} {whom} {change} ({cause}).";
    }

    private string InterruptReason(ChronicleEntry entry, string cause) => entry.Get(ChronicleParameters.Reason) switch
    {
        InterruptReasons.Exhausted => "exhausted",
        InterruptReasons.Cancelled => "ordered to stop",
        InterruptReasons.Moved => "ordered elsewhere",
        InterruptReasons.Weather => "the weather changed",
        InterruptReasons.StoresFull => "no room left in the stores",
        InterruptReasons.HandsFull => "hands full, off to the stores with it",
        InterruptReasons.Incapacitated => $"{Words(entry.Get(ChronicleParameters.To))} ({cause})",
        var other => Words(other),
    };

    // Why something happened, as the simulation says it (VOYAGE_EXPERIENCE §42): " — worn out, in a heavy sea".
    private string Because(ChronicleEntry entry) =>
        Reasons(entry.Get(ChronicleParameters.Reason), entry.Get(ChronicleParameters.From)) is { Length: > 0 } why ? $" — {why}" : "";

    private string Reasons(string? reasons, string? from = null)
    {
        if (string.IsNullOrEmpty(reasons))
        {
            return "";
        }

        return string.Join(", ", reasons.Split(',').Select(reason => reason switch
        {
            "caught" => from is not null && int.TryParse(from, out var id) ? $"caught from {Name(new CharacterId(id))}" : "caught from a shipmate",
            "tired" => "worn out",
            "wet" => "wet through below with the hatches open",
            "foul" => "in a foul ship",
            "nofresh" => "weeks without anything fresh",
            "untended" => "no one tending the sick",
            "sea" => "in a heavy sea",
            "unlashed" => "the cargo not lashed",
            var other => other,
        }));
    }

    private static string Grievance(ChronicleEntry entry) => World.SimulationRunner.GrievanceWords(entry.Get(ChronicleParameters.Reason) ?? "");

    private string SkillWord(string? skill) =>
        skill is not null && content.Skills.TryGetValue(skill, out var definition) ? definition.Name.ToLowerInvariant() : Prettify(skill ?? "");

    private static string LevelWord(string? level) =>
        int.TryParse(level, CultureInfo.InvariantCulture, out var value) ? KnowledgeText.LevelWord(value).ToLowerInvariant() : level ?? "";

    private string Name(CharacterId id) => campaign.Characters.TryGetValue(id, out var character) ? character.Name : id.ToString();

    private string Task(string? taskId) =>
        taskId is not null && content.Tasks.TryGetValue(taskId, out var task) ? task.Name.ToLowerInvariant() : Prettify(taskId);

    private string Cause(string? cause) => cause switch
    {
        null => "no reason recorded",
        _ when cause.StartsWith("task.", StringComparison.Ordinal) => Task(cause),
        _ when content.Conditions.ContainsKey(cause) => Condition(cause),
        _ when cause.Contains('.') && !cause.Contains(' ') => Prettify(cause),
        _ => cause,
    };

    private string Condition(string? conditionId) =>
        conditionId is not null && content.Conditions.TryGetValue(conditionId, out var condition)
            ? condition.Name.ToLowerInvariant()
            : Prettify(conditionId);

    /// <summary>Fallback for IDs without a definition: <c>condition.rope_burn</c> becomes <c>rope burn</c>.</summary>
    private static string Prettify(string? id) =>
        id is null ? "something" : id[(id.IndexOf('.') + 1)..].Replace('_', ' ');

    private static string Phase(string? phase) => Words(phase);

    /// <summary><c>PortPreparation</c> becomes <c>port preparation</c>.</summary>
    private static string Words(string? identifier) =>
        identifier is null ? "unknown" : CapitalSplit().Replace(identifier, " $1").Trim().ToLower(CultureInfo.InvariantCulture);

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex CapitalSplit();

    private string BuildingName(string? id) => content.Shore?.Settlement.Building(id ?? "")?.Name ?? id ?? "building";

    private string CropName(string? id) => content.Shore?.Settlement.Crops.FirstOrDefault(c => c.Id == id)?.Name ?? id ?? "crop";

    private static string Article(string noun) => noun.Length > 0 && "aeiouAEIOU".Contains(noun[0]) ? $"an {noun.ToLowerInvariant()}" : $"a {noun.ToLowerInvariant()}";

    private static string QualityWord(string? level) =>
        double.TryParse(level, NumberStyles.Float, CultureInfo.InvariantCulture, out var q) ? q >= 0.75 ? "sound" : q >= 0.5 ? "fair" : "rough" : "fair";

    // A people's name, from the coast's rules.
    private string Other(ChronicleEntry entry) => entry.Characters.Count > 1 ? Name(entry.Characters[1]) : "another";

    private string CommunityName(string? id) => id is not null && content.Shore?.Region?.Community(id) is { } community ? community.Name : "the coast's people";
}
