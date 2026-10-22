namespace Expedition.Simulation.History;

/// <summary>Category of a chronicle entry.</summary>
public enum ChronicleEntryKind
{
    /// <summary>The campaign moved to a new phase.</summary>
    PhaseChanged,

    /// <summary>A requested phase transition was not allowed.</summary>
    TransitionRejected,

    /// <summary>A character's survival state changed (injury, death, found, ...).</summary>
    SurvivalStateChanged,

    /// <summary>A character was hurt (for example, by a hazardous task).</summary>
    Injury,

    /// <summary>A character's task stopped before it was finished.</summary>
    TaskInterrupted,

    /// <summary>A task was paused by an urgent order; it waits in the queue with its progress kept.</summary>
    TaskPaused,

    /// <summary>A waiting task could not be started and was removed from the queue.</summary>
    TaskDropped,

    /// <summary>Someone needed food or water and none was available.</summary>
    SupplyShortage,

    /// <summary>A character became the captain the player leads as.</summary>
    CaptainAppointed,

    /// <summary>How one character regards another changed (characters: holder first, then subject).</summary>
    RelationshipChanged,

    /// <summary>The weather at sea changed (parameters: from, to).</summary>
    WeatherChanged,

    /// <summary>A heavy sea struck the hull and opened a seam (parameters: damage, level = hull now).</summary>
    HullStruck,

    /// <summary>
    /// Something carried away aloft in heavy weather (parameters: to = "yard" or "line", need = the
    /// line's number, level = rigging now).
    /// </summary>
    RiggingCarriedAway,

    /// <summary>
    /// The captain saw someone at work in a skill and it was far from what he had been told (parameters:
    /// need = the skill, from = the level told, level = the level seen).
    /// </summary>
    ClaimTested,

    /// <summary>The noon sight was taken (parameters: level = the latitude found, need = its likely error in leagues).</summary>
    NoonSight,

    /// <summary>The bilge rose past an alarm level (parameter: level).</summary>
    BilgeAlarm,

    /// <summary>The ship filled and sank.</summary>
    Foundered,

    /// <summary>The destination was reached, and known for what it is (parameter: to = destination).</summary>
    Landfall,

    /// <summary>By the reckoning the coast is near (parameter: level = leagues off by the reckoning).</summary>
    CoastNear,

    /// <summary>A sign of land (parameter: to = birds, weed, water or cloud).</summary>
    LandSign,

    /// <summary>The lead was cast (parameters: level = fathoms, or none; to = the bottom; character: who).</summary>
    Sounding,

    /// <summary>The coast was raised (parameter: level = leagues off).</summary>
    LandSighted,

    /// <summary>A landmark came into view (parameter: to = landmark ID).</summary>
    LandmarkSighted,

    /// <summary>The captain, or the best hand aboard, took the coast for a landmark (parameter: to = landmark ID; character: who decided).</summary>
    CoastIdentified,

    /// <summary>Where she was taken to be at the destination, it was not there (parameter: to = destination landmark ID).</summary>
    NotTheDestination,

    /// <summary>She ran onto the coast unseen (parameters: damage, level = hull now).</summary>
    StruckTheCoast,

    /// <summary>All hands were called on deck, or dismissed (parameter: to = called or dismissed).</summary>
    AllHands,

    /// <summary>Heavy weather foreseen from its signs (parameter: to = the weather coming; character: who said so).</summary>
    StormWarning,

    /// <summary>A preparation for heavy weather done (parameter: to = aloft, cargo or hatches; character: who).</summary>
    Prepared,

    /// <summary>Fair again: topmasts swayed up, lashings cast off, hatches opened.</summary>
    PreparationsUndone,

    /// <summary>A heavy sea sprang a leak (parameters: to = where, level = how bad).</summary>
    LeakSprung,

    /// <summary>A leak plugged in a hurry (parameter: to = where; character: who).</summary>
    LeakPlugged,

    /// <summary>A leak made good with plank (parameter: to = where; character: who).</summary>
    LeakPatched,

    /// <summary>A plugged leak burst open again in heavy weather (parameter: to = where).</summary>
    LeakReopened,

    /// <summary>Cargo not lashed shifted and stove casks (parameters: to = item, level = lost).</summary>
    CargoShifted,

    /// <summary>Stores wetted below open hatches (parameters: to = item, level = lost).</summary>
    StoresWetted,

    /// <summary>
    /// The ship lost and the survivors ashore (parameters: cause = foundered or struck, to = where ashore,
    /// level = days in the boat, need = survivors, reason = boat or noBoat).
    /// </summary>
    Wrecked,

    /// <summary>Bought from islanders (parameters: to = item, level = quantity, need = price, from = island).</summary>
    IslandTrade,

    /// <summary>The captain made for an island (parameter: to = island ID).</summary>
    MakingForIsland,

    /// <summary>The island was raised (parameters: to = island ID, level = leagues off).</summary>
    IslandSighted,

    /// <summary>She came to anchor off an island (parameters: to = island ID, reason = fix or found).</summary>
    Anchored,

    /// <summary>Where the chart put a reported island, nothing in sight (parameter: to = island ID).</summary>
    IslandNotFound,

    /// <summary>Back to the passage (parameters: to = island ID, reason = weighed or gave up).</summary>
    WeighedAnchor,

    /// <summary>The captain decided an event (parameters: event, choice, outcome; characters involved).</summary>
    EventResolved,

    /// <summary>Rations were cut or restored (parameter: to = short or full).</summary>
    RationingChanged,

    /// <summary>The captain changed how much sail is carried (parameter: to = plan name).</summary>
    SailPlanChanged,

    /// <summary>Nobody is left alive aboard.</summary>
    AllHandsLost,

    /// <summary>Someone signed on (parameter: level = advance paid; character: who).</summary>
    CrewHired,

    /// <summary>The captain noticed something about someone (parameter: to = fact ID; character: who).</summary>
    Noticed,

    /// <summary>Someone stopped waiting for a berth and left (parameter: reason; character: who).</summary>
    LeftPort,

    /// <summary>The convoy sails soon (parameter: level = hours left).</summary>
    ConvoyWarning,

    /// <summary>The ship stayed after the convoy and paid for it (parameter: level = sueldos).</summary>
    LateFee,

    /// <summary>The captain heard where someone worth finding might be. Parameter: <c>to</c> (the lead ID).</summary>
    LeadHeard,

    /// <summary>Stores were found spoiling, the first time for that item (parameters: to = item ID, level = units lost).</summary>
    StoresSpoiled,

    /// <summary>Someone fell ill from a bad ration (parameters: condition, to = item ID; character: who).</summary>
    SickenedByRation,

    /// <summary>Someone got over an illness (parameter: condition; character: who).</summary>
    Recovered,

    /// <summary>The captain stood a room a round (parameters: to = room name, level = sueldos; characters: who drank).</summary>
    RoundBought,

    /// <summary>A game of dice in a room (parameter: level = sueldos won, negative lost; characters: who played).</summary>
    DicePlayed,

    /// <summary>Someone sang in a room (parameter: to = the song's title; character: the singer).</summary>
    SongSung,

    /// <summary>
    /// Someone fell ill (parameters: condition; reason = what brought it on, comma-separated: caught, tired,
    /// wet, foul, nofresh; from = the shipmate it was caught from; character: who).
    /// </summary>
    FellIll,

    /// <summary>An injury healed (parameter: condition; character: who).</summary>
    Healed,

    /// <summary>Sickness is spreading (parameters: level = how many are sick, condition = the commonest, reason = why: foul, untended, wet; health = cleanliness).</summary>
    SicknessSpreads,

    /// <summary>A grievance went a stage further (parameters: to = the stage, reason = the cause; character: who).</summary>
    GrievanceRose,

    /// <summary>A grievance eased (parameters: to = the stage now, reason = the cause; character: who).</summary>
    GrievanceEased,

    /// <summary>Others gathered round someone's grievance (parameter: reason = the cause; characters: the leader, then the rest).</summary>
    FactionFormed,

    /// <summary>A promise to the crew was kept (parameter: reason = its cause).</summary>
    PromiseKept,

    /// <summary>A promise to the crew was broken (parameter: reason = its cause).</summary>
    PromiseBroken,

    /// <summary>Someone's skill rose a level (parameters: to = skill, level, from = the mentor's ID; characters: who, then the mentor).</summary>
    SkillImproved,

    /// <summary>Someone began learning from a shipmate (parameter: to = skill; characters: learner, mentor).</summary>
    MentorshipBegun,

    /// <summary>
    /// Cargo breaking loose caught someone (parameters: condition, reason = why: sea, unlashed, tired;
    /// damage; character: who).
    /// </summary>
    CargoInjury,

    /// <summary>The officer of the watch acted on a standing order (parameters: reason = which, a <see cref="Voyage.StandingOrderReasons"/>; to = detail).</summary>
    StandingOrderActed,

    /// <summary>The captain was called, as the standing orders say (parameters: reason, level, need or to = detail).</summary>
    CaptainCalled,

    /// <summary>The captain laid off a leg on the chart (parameter: level = how many legs now).</summary>
    LegPlotted,

    /// <summary>She reached a leg's end by the reckoning (parameter: level = legs left).</summary>
    LegReached,
}
