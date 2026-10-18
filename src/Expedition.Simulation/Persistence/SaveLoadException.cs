namespace Expedition.Simulation.Persistence;

/// <summary>
/// A save could not be read. The message says why in terms a player or developer can act on.
/// Loading never falls back to a fresh or partial campaign.
/// </summary>
public sealed class SaveLoadException(string message, Exception? inner = null) : Exception(message, inner);
