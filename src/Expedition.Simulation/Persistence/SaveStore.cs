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

    /// <summary>What a slot holds, for a list of saves: when it was written, the campaign's phase and its tick,
    /// read from the head of the file without loading the whole save. Phase and tick are null if unreadable.</summary>
    public (DateTime Written, string? Phase, long? Tick) Describe(string slot)
    {
        var path = PathFor(slot);
        var written = File.GetLastWriteTime(path);
        try
        {
            var head = new byte[4096];
            int read;
            using (var file = File.OpenRead(path))
            {
                read = file.Read(head, 0, head.Length);
            }

            var reader = new System.Text.Json.Utf8JsonReader(head.AsSpan(0, read), isFinalBlock: false, state: default);
            string? phase = null;
            long? tick = null;
            while (reader.Read() && (phase is null || tick is null))
            {
                if (reader.TokenType != System.Text.Json.JsonTokenType.PropertyName || reader.CurrentDepth != 1)
                {
                    continue;
                }

                var name = reader.GetString();
                if (!reader.Read())
                {
                    break;
                }

                if (name == "phase" && reader.TokenType == System.Text.Json.JsonTokenType.String)
                {
                    phase = reader.GetString();
                }
                else if (name == "tick" && reader.TokenType == System.Text.Json.JsonTokenType.Number)
                {
                    tick = reader.GetInt64();
                }
            }

            return (written, phase, tick);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return (written, null, null);
        }
    }

    /// <summary>Deletes <paramref name="slot"/>, if it exists.</summary>
    public void Delete(string slot) => File.Delete(PathFor(slot));

    private string PathFor(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot) || slot.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || slot.Contains(".."))
        {
            throw new ArgumentException($"'{slot}' is not a valid save slot name.", nameof(slot));
        }

        return Path.Combine(Directory, slot + Extension);
    }
}
