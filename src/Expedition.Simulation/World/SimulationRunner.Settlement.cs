using System.Globalization;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Shore;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.Voyage;

namespace Expedition.Simulation.World;

/// <summary>
/// Making the camp last through the first season (R7, docs/R7_ESTABLISHMENT.md; SETTLEMENT_EXPERIENCE §7-19,
/// §36-37). Buildings are raised on plots in phases, each phase ordinary work for the same hands, with the
/// materials the site gives and the tools the ship carried, which wear and break; how well each phase is
/// done makes the building's quality, and quality decides how it stands the weather. Stores spoil by where
/// they are kept. The wet season comes, and a storm with it; the garden is an experiment. After 90 days
/// the first season is judged.
/// </summary>
public sealed partial class SimulationRunner
{
    // The carpenter's chest, the ship's fishing gear and the seed chest (R7, decided 2026-10-22).
    private static readonly IReadOnlyDictionary<string, int> ShipsChest = new SortedDictionary<string, int>(StringComparer.Ordinal)
    {
        ["item.axe"] = 3, ["item.saw"] = 1, ["item.adze"] = 2, ["item.auger"] = 1, ["item.spade"] = 2, ["item.mattock"] = 1,
        ["item.cutlass"] = 3, ["item.fishing_net"] = 1,
        ["item.seed_wheat"] = 10, ["item.seed_chickpea"] = 8, ["item.seed_onion"] = 6, ["item.seed_cabbage"] = 4,
        // The Company's trade chest (R8): what the factory traded for wood and food.
        ["item.trade_knives"] = 40, ["item.trade_cloth"] = 12, ["item.fishhooks"] = 300,
    };

    // Seed a bed takes; beds a tending sees to; days before a bed wants tending again.
    private const int SeedPerBed = 2;
    private const int BedsTended = 3;
    private const double TendEveryDays = 3;

    // A building is wanted mended below this condition, and gives no shelter below the other.
    private const double RepairBelow = 75;
    private const double StandsAbove = 25;

    private SettlementRules? Settling => Content.Shore?.Settlement;

    /// <summary>Days ashore so far.</summary>
    public double CampDay => Campaign.Camp is { } camp ? (Campaign.Clock.Tick - camp.Landed) / (double)SimulationClock.TicksPerDay : 0;

    /// <summary>True once the wet season has come.</summary>
    public bool WetSeason => Campaign.Camp is not null && Settling is { } rules && CampDay >= rules.WetFromDay;

    /// <summary>True while the storm at the change of season blows.</summary>
    public bool StormAshore => Campaign.Camp is not null && Settling is { } rules && CampDay >= rules.StormDay && CampDay < rules.StormDay + rules.StormHours / 24.0;

    // The ship's chest, as it comes ashore: all of it afloat at anchor; after a wreck, each thing saved
    // by the boat or left in the wreck, by what could be reached.
    private void LandShipsChest(CampState camp, WreckState? wreck)
    {
        foreach (var (item, count) in ShipsChest)
        {
            if (!Content.Items.TryGetValue(item, out var definition))
            {
                continue;
            }

            if (wreck is null)
            {
                camp.PutAfloat(item, count);
                continue;
            }

            var reach = Content.Voyage?.Wreck.Reach.GetValueOrDefault(definition.Category, 0.5) ?? 0.5;
            for (var i = 0; i < count; i++)
            {
                if (Campaign.Randomness.Events.Chance(reach))
                {
                    Campaign.SharedStore.TryAdd(item, 1, Content.Items);
                }
                else if (wreck.Remains.Count > 0)
                {
                    camp.PutAfloat(item, 1);
                }
            }
        }
    }

    // ================================================================ projects

    // The plot a camp spot belongs to: plot.hut_2.a is hut_2.
    private static string? PlotOf(string? spotId) =>
        spotId is not null && spotId.StartsWith("plot.", StringComparison.Ordinal) && spotId.Split('.') is [_, var plot, _] ? plot : null;

    /// <summary>Gives up a building not yet finished. What went into it stays in it.</summary>
    public CommandResult CancelProject(int id)
    {
        if (Campaign.Camp is not { } camp || camp.Projects.FirstOrDefault(p => p.Id == id) is not { } project)
        {
            return CommandResult.Rejected("There is no such work.");
        }

        camp.RemoveProject(project);
        if (project.Phase > 0 || project.Done > 0)
        {
            GroundLeft(camp, FootprintOf(project));
        }

        return CommandResult.Ok();
    }

    /// <summary>The phase a project is at, or null when finished.</summary>
    public PhaseDefinition? PhaseOf(CampProject project) =>
        Settling?.Building(project.Building) is { } building && project.Phase < building.Phases.Count ? building.Phases[project.Phase] : null;

    // How many more times a project's phase wants doing, after the work already under way on it.
    private int PhaseLeft(CampProject project) =>
        PhaseOf(project) is not { } phase ? 0
            : PhaseTimes(project, phase) - project.Done - Campaign.ActiveTasks.Values.Count(t => t.DefinitionId == phase.Task && PlotOf(t.SpotId) == project.Plot);

    // Whether a camp spot may be worked at for a task now: a building phase only on a plot at that phase,
    // a building's own work only where it stands.
    private bool SpotServes(TaskDefinition task, WorkSpotDefinition spot)
    {
        if (Campaign.Camp is not { } camp || PlotOf(spot.Id) is not { } plot)
        {
            // Off the plots: mending at the hearth, before there is a workshop; nothing else of a building's.
            return !task.BuildPhase && !task.RepairsBuilding && task.Garden is null && (!task.MendsTool || !StandingOf(b => b.Mends).Any());
        }

        if (task.BuildPhase)
        {
            return camp.Projects.FirstOrDefault(p => p.Plot == plot) is { } project && PhaseOf(project)?.Task == task.Id && PhaseLeft(project) > 0;
        }

        var standing = camp.Buildings.FirstOrDefault(b => b.Plot == plot);
        var kind = standing is null ? null : Settling?.Building(standing.Building);
        return task switch
        {
            { RepairsBuilding: true } => standing is { Condition: < RepairBelow },
            { MendsTool: true } => kind is { Mends: true },
            { Garden: not null } => kind is { Beds: > 0 },
            // Whatever else it is for: the workshop's sawing, the well's water.
            _ => kind is not null && WorkAt(kind).Contains(task.Id),
        };
    }

    // Materials still wanted for every project's remaining phases, beyond what is ashore.
    private int MaterialShort(string item)
    {
        if (Campaign.Camp is not { } camp || Settling is not { } rules)
        {
            return 0;
        }

        var wanted = 0;
        foreach (var project in camp.Projects)
        {
            if (rules.Building(project.Building) is not { } building)
            {
                continue;
            }

            for (var phase = project.Phase; phase < building.Phases.Count; phase++)
            {
                var times = (phase == project.Phase ? PhaseTimes(project, building.Phases[phase]) - project.Done : building.Phases[phase].Times);
                wanted += times * (Content.Tasks.TryGetValue(building.Phases[phase].Task, out var task) ? task.Consumes.GetValueOrDefault(item) : 0);
            }
        }

        // Repairs want thatch too.
        if (item == "item.thatch")
        {
            wanted += camp.Buildings.Count(b => b.Condition < RepairBelow) * 4;
        }

        // Planks with no ruin to salvage them from are sawn from poles at the workshop.
        if (item == "item.poles" && StockLeft("camp_timber") <= 0 && StandingOf(b => b.Mends).Any()
            && Content.Tasks.TryGetValue("task.pit_saw_planks", out var saw) && saw.Makes.GetValueOrDefault("item.plank") is > 0 and var made)
        {
            wanted += (MaterialShort("item.plank") + made - 1) / made * saw.Consumes.GetValueOrDefault("item.poles");
        }

        return Math.Max(0, wanted - Campaign.SharedStore.Count(item) - Campaign.ActiveTasks.Values.Count(t => Content.Tasks[t.DefinitionId].Yield?.Item == item) * 4);
    }

    /// <summary>
    /// What the site has left near the camp of what is gathered there (a yield's <c>ashore</c>): poles and
    /// thatch at the forest's edge, stone on the rocks, planks in a ruin found.
    /// </summary>
    public int StockLeft(string ashore)
    {
        if (Campaign.Camp is not { } camp || Settling is not { } rules || CampSite is not { } site)
        {
            return 0;
        }

        var share = site.Brush ? 1 : rules.Scarce;
        return ashore switch
        {
            "camp_poles" => (int)(rules.Poles * share) - camp.PolesCut,
            "camp_thatch" => (int)(rules.Thatch * share) - camp.ThatchCut,
            "camp_stone" => site.Stone ? rules.Stone - camp.StoneGathered : 0,
            "camp_timber" => site.Finds.Where(f => f.Planks > 0 && camp.Found.Contains(f.Id)).Sum(f => f.Planks) - camp.TimberSalvaged,
            _ => 0,
        };
    }

    // Whether the settlement wants a work now (DutyCondition.Camp, R7's part).
    private bool SettlementWants(string need, TaskDefinition task)
    {
        if (Campaign.Camp is not { } camp || Settling is not { } rules)
        {
            return false;
        }

        return need switch
        {
            "build" or "build_dry" => (need == "build" || !Raining)
                                       && camp.Projects.Any(p => PhaseOf(p)?.Task == task.Id && PhaseLeft(p) > 0),
            "poles" => MaterialShort("item.poles") > 0,
            "thatch" => MaterialShort("item.thatch") > 0,
            "stone" => MaterialShort("item.stone") > 0 && StockLeft("camp_stone") > 0,
            // Planks: salvaged from a ruin while there are any, else sawn at the workshop.
            "timber" => MaterialShort("item.plank") > 0 && StockLeft("camp_timber") > 0,
            "planks" => MaterialShort("item.plank") > 0 && StockLeft("camp_timber") <= 0
                        && !Campaign.ActiveTasks.Values.Any(t => t.DefinitionId == task.Id),
            "mend" => camp.Broken.Count > 0 && !Campaign.ActiveTasks.Values.Any(t => Content.Tasks[t.DefinitionId].MendsTool),
            "repair_dry" => !Raining && camp.Buildings.Any(b => b.Condition < RepairBelow)
                            && !Campaign.ActiveTasks.Values.Any(t => Content.Tasks[t.DefinitionId].RepairsBuilding),
            "sow" => FreeBeds() > 0 && SowableCrop() is not null,
            "tend" => camp.Beds.Any(b => Campaign.Clock.Tick - b.Tended > TendEveryDays * SimulationClock.TicksPerDay)
                      && !Campaign.ActiveTasks.Values.Any(t => Content.Tasks[t.DefinitionId].Garden == "tend"),
            "harvest" => camp.Beds.Any(Ripe) && !Campaign.ActiveTasks.Values.Any(t => Content.Tasks[t.DefinitionId].Garden == "harvest"),
            _ => false,
        };
    }

    // The buildings standing of a kind.
    private IEnumerable<CampBuilding> StandingOf(Func<BuildingDefinition, bool> kind) =>
        Campaign.Camp is { } camp && Settling is { } rules
            ? camp.Buildings.Where(b => b.Condition > StandsAbove && rules.Building(b.Building) is { } d && kind(d))
            : [];

    /// <summary>Dry berths in the huts standing.</summary>
    public int HutBerths => StandingOf(b => b.Berths > 0).Sum(b => Settling!.Building(b.Building)!.Berths);

    /// <summary>Berths for the sick in an infirmary standing.</summary>
    public int InfirmaryBerths => StandingOf(b => b.SickBerths > 0).Sum(b => Settling!.Building(b.Building)!.SickBerths);

    /// <summary>True while a storehouse stands.</summary>
    public bool StorehouseStands => StandingOf(b => b.Stores).Any();

    private bool CookhouseStands => StandingOf(b => b.Cooks).Any();

    // How the infirmary helps: the sick mend faster, and pass it on less.
    private double InfirmaryCare => InfirmaryBerths > 0 ? 1.25 : 1;
    // Apart from the huts it keeps the sickness in; close by them, less so.
    private double InfirmaryContagion => StandingOf(b => b.SickBerths > 0).Select(SitingFactor).DefaultIfEmpty(1).Min();

    // How well a hand does building work: their skill, and how fresh they are.
    private double WorkQuality(Character character, TaskDefinition task)
    {
        var skill = task.Skill is { } s ? character.Skills.Get(s) : Math.Max(character.Skills.Get("skill.carpentry"), character.Skills.Get("skill.crafting")) / 2.0;
        var tired = character.Needs.Fatigue >= 70 ? 0.15 : 0;
        return Math.Clamp(0.35 + 0.12 * skill - tired, 0.1, 1);
    }

    // A building phase's work done: the project goes on, and is finished with its last phase.
    private void FinishPhase(Character character, TaskDefinition definition, string? spotId)
    {
        if (Campaign.Camp is not { } camp || Settling is not { } rules || PlotOf(spotId) is not { } plot
            || camp.Projects.FirstOrDefault(p => p.Plot == plot) is not { } project || rules.Building(project.Building) is not { } building
            || PhaseOf(project) is not { } phase || phase.Task != definition.Id)
        {
            return;
        }

        project.Done++;
        project.QualitySum += WorkQuality(character, definition);
        project.Worked++;
        if (project.Done < PhaseTimes(project, phase))
        {
            return;
        }

        Felled(project, definition);

        // Whatever was laid ready for this phase and not wanted goes back to the store.
        if (project.Laid > 0)
        {
            foreach (var (item, count) in definition.Consumes)
            {
                Campaign.SharedStore.TryAdd(item, count * project.Laid, Content.Items);
            }

            project.Laid = 0;
        }

        project.Phase++;
        project.Done = 0;
        if (project.Phase < building.Phases.Count)
        {
            return;
        }

        camp.RemoveProject(project);
        var quality = project.Worked > 0 ? project.QualitySum / project.Worked : 0.5;
        camp.AddBuilding(new CampBuilding { Building = building.Id, Plot = plot, X = project.X, Y = project.Y, Turned = project.Turned, Quality = quality });
        Campaign.Record(ChronicleEntryKind.BuildingFinished, [
            new(ChronicleParameters.To, building.Id), new(ChronicleParameters.Level, quality.ToString("0.00", CultureInfo.InvariantCulture)),
        ], character.Id);
    }

    // The settlement's own work done: mending, repairs, the garden.
    private void FinishSettlementWork(Character character, TaskDefinition definition, string? spotId)
    {
        if (Campaign.Camp is not { } camp || Settling is not { } rules)
        {
            return;
        }

        if (definition.BuildPhase)
        {
            FinishPhase(character, definition, spotId);
        }

        foreach (var (item, count) in definition.Makes)
        {
            Campaign.SharedStore.TryAdd(item, count, Content.Items);
        }

        if (definition.MendsTool && camp.Broken.Keys.FirstOrDefault() is { } tool)
        {
            camp.Break(tool, -1);
            // At a workshop's bench most are saved; by the fire, fewer.
            var bench = PlotOf(spotId) is not null ? 1 : 0.6;
            var saved = Campaign.Randomness.Tasks.Chance((rules.MendChance + 0.05 * character.Skills.Get("skill.carpentry")) * bench);
            if (saved)
            {
                Campaign.SharedStore.TryAdd(tool, 1, Content.Items);
            }

            Campaign.Record(saved ? ChronicleEntryKind.ToolMended : ChronicleEntryKind.ToolLost, [new(ChronicleParameters.To, tool)], character.Id);
        }

        if (definition.RepairsBuilding && camp.Buildings.Where(b => b.Plot == PlotOf(spotId)).OrderBy(b => b.Condition).FirstOrDefault() is { } worn)
        {
            worn.Condition = Math.Min(100, worn.Condition + 40);
        }

        switch (definition.Garden)
        {
            case "sow" when FreeBeds() > 0 && SowableCrop() is { } crop && Campaign.SharedStore.TryRemove(crop.Seed, SeedPerBed):
                camp.AddBed(new GardenBed { Crop = crop.Id, Sown = Campaign.Clock.Tick, Tended = Campaign.Clock.Tick });
                Campaign.Record(ChronicleEntryKind.Sown, [new(ChronicleParameters.To, crop.Id)], character.Id);
                break;
            case "tend":
                foreach (var bed in camp.Beds.OrderBy(b => b.Tended).Take(BedsTended))
                {
                    bed.Tended = Campaign.Clock.Tick;
                }

                break;
            case "harvest":
                foreach (var bed in camp.Beds.Where(Ripe).ToList())
                {
                    var grown = rules.Crops.First(c => c.Id == bed.Crop);
                    var units = (int)Math.Round(grown.PerBed * bed.Health);
                    if (units > 0)
                    {
                        Campaign.SharedStore.TryAdd(grown.Yields, units, Content.Items);
                    }

                    camp.RemoveBed(bed);
                    Campaign.Record(ChronicleEntryKind.Harvested, [
                        new(ChronicleParameters.To, grown.Id), new(ChronicleParameters.Level, units.ToString(CultureInfo.InvariantCulture)),
                    ], character.Id);
                }

                break;
        }
    }

    // Work stopped before it was done, ashore: a building phase leaves its materials laid on the plot for the
    // next start; other camp work's materials go back to the store. (At sea what was used stays used.)
    private void TaskDropped(TaskInstance task)
    {
        if (Campaign.Camp is not { } camp || !task.MaterialsConsumed || !Content.Tasks.TryGetValue(task.DefinitionId, out var definition)
            || definition.Consumes.Count == 0)
        {
            return;
        }

        if (definition.BuildPhase && camp.Projects.FirstOrDefault(p => p.Plot == PlotOf(task.SpotId)) is { } project)
        {
            project.Laid++;
            return;
        }

        foreach (var (item, count) in definition.Consumes)
        {
            Campaign.SharedStore.TryAdd(item, count, Content.Items);
        }
    }

    // Whether a building phase about to start finds its materials already laid on a plot at that phase.
    private bool UseLaidMaterials(Character character, TaskDefinition task, string? preferredSpot)
    {
        if (Campaign.Camp is not { } camp || !task.BuildPhase)
        {
            return false;
        }

        var spot = preferredSpot ?? FindWorkSpot(character, task).Spot?.Id;
        if (camp.Projects.FirstOrDefault(p => p.Plot == PlotOf(spot)) is not { Laid: > 0 } project || PhaseOf(project)?.Task != task.Id)
        {
            return false;
        }

        project.Laid--;
        return true;
    }

    // ================================================================ the garden

    private bool IsGarden(CampBuilding building) => Settling?.Building(building.Building) is { Beds: > 0 };

    // Thatched, and so worn by the weather (a well, a landing, a garden have their own troubles).
    private bool Roofed(CampBuilding building) => Settling?.Building(building.Building) is { Roofed: true };

    // Beds the garden has room for, after those sown and being sown.
    private int FreeBeds() =>
        Campaign.Camp is { } camp
            ? StandingOf(b => b.Beds > 0).Sum(b => Settling!.Building(b.Building)!.Beds) - camp.Beds.Count
              - Campaign.ActiveTasks.Values.Count(t => Content.Tasks[t.DefinitionId].Garden == "sow")
            : 0;

    // What to sow next: of the crops there is seed for and that may be sown now, the one least sown.
    private CropDefinition? SowableCrop() =>
        Campaign.Camp is { } camp && Settling is { } rules
            ? rules.Crops.Where(c => Campaign.SharedStore.Count(c.Seed) >= SeedPerBed && !(c.DrySowingOnly && WetSeason) && (c.Knowledge is null || Learned(c.Knowledge)))
                .OrderBy(c => camp.Beds.Count(b => b.Crop == c.Id)).ThenByDescending(c => c.Suitability).FirstOrDefault()
            : null;

    // Whether a bed is ready to be brought in.
    private bool Ripe(GardenBed bed) =>
        Settling?.Crops.FirstOrDefault(c => c.Id == bed.Crop) is { } crop && Campaign.Clock.Tick - bed.Sown >= crop.GrowDays * (long)SimulationClock.TicksPerDay;

    /// <summary>How far a bed has grown, 0..1.</summary>
    public double Growth(GardenBed bed) =>
        Settling?.Crops.FirstOrDefault(c => c.Id == bed.Crop) is { } crop
            ? Math.Clamp((Campaign.Clock.Tick - bed.Sown) / (crop.GrowDays * (double)SimulationClock.TicksPerDay), 0, 1)
            : 0;

    // ================================================================ tools

    // Tools in use by others now: two hands cannot use one axe.
    private int ToolsInUse(string tool, CharacterId except) =>
        Campaign.ActiveTasks.Values.Count(t => t.CharacterId != except && Content.Tasks[t.DefinitionId].RequiredTools.Contains(tool));

    // A tool may break in the work, an hour's chance spread over its minutes; the work stops for it.
    private bool ToolBreaks(Character character, TaskDefinition definition)
    {
        if (Campaign.Camp is not { } camp || Settling is not { } rules || definition.RequiredTools.Count == 0)
        {
            return false;
        }

        foreach (var tool in definition.RequiredTools)
        {
            if (Campaign.Randomness.Tasks.Chance(rules.ToolBreaksPerHour * HoursPerTick) && Campaign.SharedStore.TryRemove(tool, 1))
            {
                camp.Break(tool);
                Campaign.Record(ChronicleEntryKind.ToolBroke, [new(ChronicleParameters.To, tool), new(ChronicleParameters.Task, definition.Id)], character.Id);
                return true;
            }
        }

        return false;
    }

    // What the forest's edge gives this minute, by what is left near; further off, much less. Counted as cut.
    private (string Item, double PerHour)? GatherYield(string ashore, TaskYield yield)
    {
        // Stone and a ruin's timbers are there or not; the forest's edge, cut back, still gives a little.
        if (ashore is "camp_stone" or "camp_timber")
        {
            return StockLeft(ashore) > 0 ? (yield.Item, yield.PerHour) : null;
        }

        var item = ashore == "camp_poles" ? "item.poles" : "item.thatch";
        return (item, yield.PerHour * (StockLeft(ashore) > 0 ? 1 : 0.3));
    }

    // What the site gave: so much less of it; a tree felled for every few poles, near where they cut.
    private void NoteGathered(string ashore, Character character)
    {
        if (Campaign.Camp is not { } camp)
        {
            return;
        }

        switch (ashore)
        {
            case "camp_poles":
                camp.PolesCut++;
                if (camp.PolesCut % PolesPerTreeFelled == 0 && Campaign.Placements.TryGetValue(character.Id, out var placement))
                {
                    Fell(placement.CellAt(Campaign.Clock.Tick));
                }

                break;
            case "camp_thatch":
                camp.ThatchCut++;
                camp.ThatchLastCut = Campaign.Clock.Tick;
                break;
            case "camp_stone":
                camp.StoneGathered++;
                break;
            case "camp_timber":
                camp.TimberSalvaged++;
                break;
        }
    }

    // ================================================================ the season

    // Once an hour ashore, R7's part: the storm, the cookhouse's fire; at first light, the day's wear.
    private void StepSettlement(CampState camp, CampRules campRules)
    {
        if (Settling is not { } rules)
        {
            return;
        }

        var hour = Campaign.Clock.TimeOfDay.TotalHours;
        var day = CampDay;
        if (CookhouseStands)
        {
            camp.FireUntil = Campaign.Clock.Tick + (long)(campRules.FireHours * SimulationClock.TicksPerHour);
        }

        // The wet season comes, and the storm at its change.
        if (Math.Abs(day - rules.WetFromDay) < 1.0 / 48)
        {
            Campaign.Record(ChronicleEntryKind.WetSeason, []);
        }

        if (StormAshore)
        {
            if (Math.Abs(day - rules.StormDay) < 1.0 / 48)
            {
                Campaign.Record(ChronicleEntryKind.StormAshore, [new(ChronicleParameters.Level, rules.StormHours.ToString(CultureInfo.InvariantCulture))]);
            }

            StormDamage(camp);
        }

        if (hour == 6)
        {
            StepSettlementDay(camp, rules);
        }

        if (camp.Survived is null && day >= rules.JudgeDay)
        {
            JudgeTheSeason(camp);
        }
    }

    // An hour of the storm: roofs lifted and walls racked by how well they were made, the camp's sail and
    // brush shelters blown down, and the ship, if she is at anchor, dragging at her cables.
    private void StormDamage(CampState camp)
    {
        var random = Campaign.Randomness.Events;
        foreach (var building in camp.Buildings.Where(Roofed).ToList())
        {
            // Roofs tied down and made fast, warned by the coast's people, stand it better (R8).
            building.Condition -= (1.2 - building.Quality) * 3 * (0.5 + random.NextDouble()) * (Learned("know.storm") ? 0.6 : 1);
            if (building.Condition <= 0)
            {
                camp.RemoveBuilding(building);
                GroundLeft(camp, FootprintOf(building));
                Campaign.Record(ChronicleEntryKind.BuildingDown, [new(ChronicleParameters.To, building.Building)]);
            }
        }

        if (camp.Work(CampWorks.Shelter) > 0 && random.Chance(0.05))
        {
            var sail = camp.Work(CampWorks.SailShelters) > 0;
            camp.AddWork(sail ? CampWorks.SailShelters : CampWorks.BrushShelters, -1);
            camp.AddWork(CampWorks.Shelter, -Math.Min(camp.Work(CampWorks.Shelter), sail ? 4 : 3));
            Campaign.Record(ChronicleEntryKind.BuildingDown, [new(ChronicleParameters.To, sail ? "shelter.sail" : "shelter.brush")]);
        }

        if (camp.ShipAtAnchor && Campaign.Voyage is { } ship)
        {
            ship.Apply(new ShipEffects { Hull = -0.4 }, 1);
        }
    }

    // At first light: stores spoil by where they are kept, the garden grows or wastes, the weather wears
    // the buildings.
    private void StepSettlementDay(CampState camp, SettlementRules rules)
    {
        var wet = WetSeason;

        // Spoilage by place: in the open, under a sail, in the storehouse (as well as it was made).
        var store = Campaign.SharedStore;
        var place = StoresPlaceFactor();
        foreach (var item in Content.Items.Values.Where(i => i.HungerRelief > 0 || i.Category == "seed").OrderBy(i => i.Id, StringComparer.Ordinal))
        {
            var count = store.Count(item.Id);
            if (count <= 0)
            {
                continue;
            }

            var rate = Math.Max(item.SpoilsPerDay, item.Category == "seed" ? 0.01 : 0.004) * place * (wet ? 1.5 : 1);
            var expected = count * rate;
            var lost = Math.Min(count, (int)Math.Floor(expected) + (Campaign.Randomness.Stores.Chance(expected - Math.Floor(expected)) ? 1 : 0));
            if (lost > 0 && store.TryRemove(item.Id, lost))
            {
                camp.Spoiled += lost;
            }
        }

        // The garden: each bed wastes by how ill the crop suits the shore and the garden's ground, more in
        // the wet, more untended.
        var gardenGround = StandingOf(b => b.Beds > 0).Select(SitingFactor).DefaultIfEmpty(1).Min();
        foreach (var bed in camp.Beds.ToList())
        {
            var crop = rules.Crops.First(c => c.Id == bed.Crop);
            var untended = (Campaign.Clock.Tick - bed.Tended) / (double)SimulationClock.TicksPerDay > TendEveryDays + 1;
            bed.Health -= (1 - crop.Suitability) * 0.02 * gardenGround + (wet ? crop.WetRot : 0) + (untended ? 0.03 : 0);
            if (bed.Health <= 0)
            {
                camp.RemoveBed(bed);
                Campaign.Record(ChronicleEntryKind.CropFailed, [new(ChronicleParameters.To, crop.Id), new(ChronicleParameters.Reason, wet ? "wet" : untended ? "untended" : "soil")]);
            }
        }

        StepGroundDay(camp);

        // The weather wears what is built, worse what was built badly (the garden's beds have their own trouble).
        foreach (var building in camp.Buildings.Where(Roofed))
        {
            building.Condition = Math.Max(1, building.Condition - (1 - building.Quality) * (wet ? 1.2 : 0.3));
        }

    }

    /// <summary>
    /// Whether the first season has been survived (SETTLEMENT §36): more than half of those who landed are
    /// alive, there is water, they are all under a dry roof, and there is food for a fortnight or a source of it.
    /// </summary>
    public (bool Survived, IReadOnlyList<string> Wanting) SeasonVerdict()
    {
        var wanting = new List<string>();
        if (Campaign.Camp is not { } camp)
        {
            return (false, wanting);
        }

        var alive = Ashore().Count;
        if (alive * 2 <= camp.LandedCount)
        {
            wanting.Add("too many lost");
        }

        if (CampWater() is null)
        {
            wanting.Add("water");
        }

        if (HutBerths < alive)
        {
            wanting.Add("dry roofs for all");
        }

        var (food, _) = SupplyForecast();
        if (food < 14 && CampFood() is null)
        {
            wanting.Add("food");
        }

        return (wanting.Count == 0, wanting);
    }

    private void JudgeTheSeason(CampState camp)
    {
        var (survived, wanting) = SeasonVerdict();
        camp.Survived = survived;
        Campaign.Record(ChronicleEntryKind.SeasonJudged, [
            new(ChronicleParameters.Outcome, survived ? "survived" : "failed"), new(ChronicleParameters.Reason, string.Join(", ", wanting)),
            new(ChronicleParameters.Need, Ashore().Count.ToString(CultureInfo.InvariantCulture)),
        ]);
    }

    /// <summary>Developer tool for tests and screenshots: a building finished where asked, or on the first free ground, as well made as asked.</summary>
    public CommandResult DeveloperRaise(string buildingId, double quality = 0.6, (int X, int Y)? at = null)
    {
        var ordered = at is { } place ? PlaceBuilding(buildingId, place.X, place.Y) : OrderBuilding(buildingId);
        if (!ordered.Succeeded || Campaign.Camp is not { } camp)
        {
            return ordered;
        }

        var project = camp.Projects[^1];
        camp.RemoveProject(project);
        camp.AddBuilding(new CampBuilding
        {
            Building = buildingId, Plot = project.Plot, X = project.X, Y = project.Y, Turned = project.Turned, Quality = Math.Clamp(quality, 0, 1),
        });
        return CommandResult.Ok();
    }

    /// <summary>The share of each food lost a day where the stores are kept now, before the food's own spoiling: in the open, under a sail, in a storehouse.</summary>
    public double StoresPlaceFactor() =>
        Campaign.Camp is not { } camp ? 1
        : StorehouseStands ? StandingOf(b => b.Stores).Min(b => (0.5 - 0.35 * b.Quality) * SitingFactor(b))
        : camp.Work(CampWorks.Cover) >= 1 ? 1.0 : 1.6;

    /// <summary>
    /// The camp's stage, from what it has rather than points (SETTLEMENT §7): a landfall camp; a survival
    /// camp with water, protected stores, a latrine and food; an established outpost with dry roofs for
    /// all, a workshop, an infirmary, a garden and, if there is a boat, a landing.
    /// </summary>
    public string CampStage()
    {
        if (Campaign.Camp is not { } camp)
        {
            return "";
        }

        var survival = CampWater() is not null && (StorehouseStands || camp.Work(CampWorks.Cover) >= 1)
                       && camp.Work(CampWorks.Latrine) >= 1 && (CampFood() is not null || camp.Beds.Count > 0);
        var outpost = survival && HutBerths >= Ashore().Count && StandingOf(b => b.Mends).Any() && InfirmaryBerths > 0 && StandingOf(b => b.Beds > 0).Any()
                      && (!camp.Boat || StandingOf(b => b.Landing).Any());
        return outpost ? "an established outpost" : survival ? "a survival camp" : "a landfall camp";
    }
}
