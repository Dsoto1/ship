namespace Expedition.Simulation.Persistence;

/// <summary>
/// Reads and writes save slots as files in one directory. The simulation does not decide where that
/// directory is: the game passes Godot's per-user data folder (see docs/DECISIONS.md D-005).
/// Writes are atomic: a crash while saving leaves the previous save intact.
/// </summary>
public sealed class SaveStore
{
    /// <summary>File extension for save slots.</summary>
    public const string Extension = ".expedition.json";

    /// <summary>Creates a store over <paramref name="directory"/>, creating the directory if needed.</summary>
    public SaveStore(string directory)
    {
        Directory = directory;
        System.IO.Directory.CreateDirectory(directory);
    }

    /// <summary>Folder that holds the save files.</summary>
    public string Directory { get; }

    /// <summary>Names of existing slots, alphabetically.</summary>
    public IReadOnlyList<string> ListSlots() =>
        System.IO.Directory.EnumerateFiles(Directory, "*" + Extension)
            .Select(path => Path.GetFileName(path)[..^Extension.Length])
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>Writes <paramref name="json"/> to <paramref name="slot"/>, replacing any existing save atomically.</summary>
    /// <exception cref="ArgumentException">The slot name is empty or contains path characters.</exception>
    public void Write(string slot, string json)
    {
        var path = PathFor(slot);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Reads the JSON in <paramref name="slot"/>.</summary>
    /// <exception cref="SaveLoadException">The slot does not exist or cannot be read.</exception>
    public string Read(string slot)
    {
        var path = PathFor(slot);
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            throw new SaveLoadException($"Save slot '{slot}' could not be read: {ex.Message}", ex);
        }
    }

    private string PathFor(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot) || slot.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || slot.Contains(".."))
        {
            throw new ArgumentException($"'{slot}' is not a valid save slot name.", nameof(slot));
        }

        return Path.Combine(Directory, slot + Extension);
    }
}
