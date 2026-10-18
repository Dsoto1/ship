namespace Expedition.Simulation.Core;

/// <summary>
/// Outcome of a command sent to the simulation. A rejected command leaves state unchanged
/// and carries a human-readable reason.
/// </summary>
/// <param name="Succeeded">True if the command was applied.</param>
/// <param name="Reason">Why the command was rejected; empty when it succeeded.</param>
public readonly record struct CommandResult(bool Succeeded, string Reason)
{
    /// <summary>A successful result.</summary>
    public static CommandResult Ok() => new(true, string.Empty);

    /// <summary>A rejected result with the given reason.</summary>
    public static CommandResult Rejected(string reason) => new(false, reason);
}
