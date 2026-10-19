using Expedition.Simulation.Core;

namespace Expedition.Simulation.Events;

/// <summary>An event waiting for the captain's decision. Part of persistent campaign state.</summary>
/// <param name="DefinitionId">Which event.</param>
/// <param name="Subject">Who it is about, or null.</param>
/// <param name="Other">The second person, for example the other party to a quarrel, or null.</param>
/// <param name="Tick">When it happened.</param>
public sealed record PendingEvent(string DefinitionId, CharacterId? Subject, CharacterId? Other, long Tick);

/// <summary>How often an event has happened, for "once" events and cool-downs.</summary>
/// <param name="Count">Times fired.</param>
/// <param name="LastTick">When it last fired.</param>
public sealed record EventRecord(int Count, long LastTick);

/// <summary>What the captain decided and what came of it, for the interface.</summary>
/// <param name="Title">Event heading.</param>
/// <param name="Choice">The choice's text.</param>
/// <param name="Succeeded">False if a skill check failed.</param>
/// <param name="Text">The outcome, with names filled in.</param>
public sealed record EventResolution(string Title, string Choice, bool Succeeded, string Text);

/// <summary>A choice as offered to the captain: whether it is possible and how likely it is to go well.</summary>
/// <param name="Choice">The choice.</param>
/// <param name="Available">False if the hold lacks what it needs.</param>
/// <param name="Chance">Chance of success, or null if the outcome is certain.</param>
public sealed record OfferedChoice(EventChoice Choice, bool Available, double? Chance);
