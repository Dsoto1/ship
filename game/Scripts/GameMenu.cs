using System.Text.RegularExpressions;
using Expedition.Simulation.Persistence;
using Godot;

namespace Expedition.Game;

/// <summary>
/// The game's own menus, over everything else: the title screen when the game starts (new voyage, continue,
/// load, settings, quit), the pause menu on Esc or the Menu tab (resume, save, load, settings, back to the
/// title), the list of saves to load or save into, and the settings. The game is paused while it is open.
/// </summary>
public partial class GameMenu : CanvasLayer
{
    /// <summary>The slot the quick save (Cmd+S) writes to.</summary>
    public const string QuickSaveSlot = "quicksave";

    private enum Screen { Title, Pause, Load, Save, Settings }

    private SaveStore _saves = null!;
    private Control _root = null!;
    private PanelContainer _panel = null!;
    private VBoxContainer _body = null!;
    private Screen _screen;
    private Screen _back;
    private string _confirm = "";

    /// <summary>Start a new voyage from the port.</summary>
    public event Action? NewVoyage;

    /// <summary>Load this save slot.</summary>
    public event Action<string>? LoadSlot;

    /// <summary>Save into this slot (a new one, or over an old one).</summary>
    public event Action<string>? SaveSlot;

    /// <summary>The list of controls was asked for.</summary>
    public event Action? ControlsRequested;

    /// <summary>The menu closed: the game carries on.</summary>
    public event Action? Closed;

    /// <summary>The settings changed: Main and the HUD apply them.</summary>
    public event Action? SettingsChanged;

    /// <summary>True while a menu is showing.</summary>
    public bool IsOpen => _root.Visible;

    /// <summary>True when the title screen is showing (there is no voyage in play behind it).</summary>
    public bool AtTitle { get; private set; }

    /// <summary>Whether there is a voyage that can be saved (not at the title).</summary>
    public Func<bool> CanSave { get; set; } = () => true;

    public void Attach(SaveStore saves) => _saves = saves;

    public override void _Ready()
    {
        Layer = 20;
        _root = new Control { Theme = Ui.BuildTheme(), Visible = false };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        var shade = new ColorRect { Color = new Color(0.02f, 0.06f, 0.09f, 0.55f), MouseFilter = Control.MouseFilterEnum.Stop };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(shade);
        _panel = Ui.Frame(Ui.SpaceXl, Ui.SpaceL);
        _panel.AnchorLeft = _panel.AnchorRight = 0.5f;
        _panel.AnchorTop = _panel.AnchorBottom = 0.5f;
        _panel.GrowHorizontal = Control.GrowDirection.Both;
        _panel.GrowVertical = Control.GrowDirection.Both;
        _root.AddChild(_panel);
        _body = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
        _body.AddThemeConstantOverride("separation", Ui.SpaceM);
        _panel.AddChild(_body);
    }

    /// <summary>The title screen, over the port as it stands.</summary>
    public void ShowTitle()
    {
        AtTitle = true;
        Open(Screen.Title);
    }

    /// <summary>The pause menu.</summary>
    public void ShowPause()
    {
        AtTitle = false;
        Open(Screen.Pause);
    }

    /// <summary>For screenshots: the settings, from the pause menu.</summary>
    public void DeveloperOpenSettings() => Open(Screen.Settings);

    /// <summary>Closes the menu, unless the title screen is showing (it closes by choosing what to do).</summary>
    public void Close()
    {
        if (AtTitle)
        {
            return;
        }

        _root.Visible = false;
        Closed?.Invoke();
    }

    /// <summary>Esc: back one screen, or out of the pause menu; true if the menu took it.</summary>
    public bool Back()
    {
        if (!IsOpen)
        {
            return false;
        }

        if (_screen is Screen.Load or Screen.Save or Screen.Settings)
        {
            Open(_back);
        }
        else
        {
            Close();
        }

        return true;
    }

    private void Open(Screen screen)
    {
        if (screen is Screen.Load or Screen.Save or Screen.Settings && _screen is Screen.Title or Screen.Pause)
        {
            _back = _screen;
        }

        _screen = screen;
        _confirm = "";
        Build();
        _root.Visible = true;
    }

    private void Build()
    {
        // Out at once (not at the end of the frame), so the window sizes to what it now holds.
        foreach (var child in _body.GetChildren())
        {
            _body.RemoveChild(child);
            child.QueueFree();
        }

        switch (_screen)
        {
            case Screen.Title: BuildTitle(); break;
            case Screen.Pause: BuildPause(); break;
            case Screen.Load: BuildSaves(saving: false); break;
            case Screen.Save: BuildSaves(saving: true); break;
            case Screen.Settings: BuildSettings(); break;
        }

        // Centred, at the size of what it holds, once it has been laid out.
        _panel.OffsetLeft = _panel.OffsetRight = _panel.OffsetTop = _panel.OffsetBottom = 0;
        _panel.ResetSize();
        Callable.From(Centre).CallDeferred();
    }

    private void Centre()
    {
        var size = _panel.GetCombinedMinimumSize();
        _panel.OffsetLeft = -size.X / 2;
        _panel.OffsetRight = size.X / 2;
        _panel.OffsetTop = -size.Y / 2;
        _panel.OffsetBottom = size.Y / 2;
    }

    // ================================================================ screens

    private void BuildTitle()
    {
        var title = Ui.Title("The Expedition", Ui.TextDisplay + 14, Ui.Gold);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        _body.AddChild(title);
        var sub = Ui.Prose("La Bocana, April 1501. The Garza lies at the quay, and the Ember Coast is a month's sail west.", Ui.TextBody, Ui.InkSoft, 400);
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        _body.AddChild(sub);
        _body.AddChild(new HSeparator());
        var latest = Saves().FirstOrDefault();
        if (latest.Slot is not null)
        {
            Item("Continue", $"{Describe(latest.Slot)}", () => LoadSlot?.Invoke(latest.Slot), primary: true);
        }

        Item("New voyage", "Begin at the quay at La Bocana: hire the crew, buy the stores, and sail.", () => { AtTitle = false; _root.Visible = false; NewVoyage?.Invoke(); }, primary: latest.Slot is null);
        Item("Load a voyage", "Choose a saved voyage.", () => Open(Screen.Load), enabled: latest.Slot is not null);
        Item("Settings", "Sound, window, interface size, when to pause.", () => Open(Screen.Settings));
        Item("Quit", "", () => GetTree().Quit());
    }

    private void BuildPause()
    {
        var title = Ui.Title("Paused", Ui.TextDisplay, Ui.Gold);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        _body.AddChild(title);
        Item("Resume", "Back to the voyage (Esc)", Close, primary: true);
        Item("Save", "Into a slot of its own, or over an old one. Cmd+S saves quickly.", () => Open(Screen.Save), enabled: CanSave());
        Item("Load", "A saved voyage, in place of this one.", () => Open(Screen.Load), enabled: Saves().Count > 0);
        Item("Settings", "Sound, window, interface size, when to pause.", () => Open(Screen.Settings));
        Item("Controls", "The keys and the mouse", () => { Close(); ControlsRequested?.Invoke(); });
        _body.AddChild(new HSeparator());
        Item(_confirm == "title" ? "Leave without saving? Click again" : "Main menu", "Back to the title screen. Unsaved progress is lost.", () =>
        {
            if (_confirm != "title")
            {
                _confirm = "title";
                Build();
                return;
            }

            ShowTitle();
        });
        Item(_confirm == "quit" ? "Quit without saving? Click again" : "Quit", "Close the game. Unsaved progress is lost.", () =>
        {
            if (_confirm != "quit")
            {
                _confirm = "quit";
                Build();
                return;
            }

            GetTree().Quit();
        });
    }

    private void BuildSaves(bool saving)
    {
        _body.AddChild(Ui.Title(saving ? "Save the voyage" : "Load a voyage", Ui.TextDisplay - 4, Ui.Gold));
        if (saving)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", Ui.SpaceS);
            _body.AddChild(row);
            var name = new LineEdit { PlaceholderText = "Name this save", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MaxLength = 40 };
            row.AddChild(name);
            var save = Ui.Button(Ui.ButtonKind.Primary, "Save", "Save into a new slot with this name", () => NewSave(name.Text));
            row.AddChild(save);
            name.TextSubmitted += NewSave;
            Callable.From(() => name.GrabFocus()).CallDeferred();
        }

        var saves = Saves();
        if (saves.Count == 0)
        {
            _body.AddChild(Ui.Text("No saves yet.", Ui.TextBody, Ui.InkSoft));
        }

        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", Ui.SpaceXs);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(480, Math.Min(360, 58 * Math.Max(1, saves.Count))), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.AddChild(list);
        _body.AddChild(scroll);
        list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        foreach (var (slot, _) in saves)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", Ui.SpaceS);
            list.AddChild(row);
            var words = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            words.AddThemeConstantOverride("separation", 0);
            row.AddChild(words);
            words.AddChild(Ui.Text(slot == QuickSaveSlot ? "Quick save" : slot, Ui.TextBody + 1, Ui.Ink));
            words.AddChild(Ui.Text(Describe(slot), Ui.TextMeta, Ui.InkSoft));
            var key = $"{(saving ? "over" : "delete")}:{slot}";
            if (saving)
            {
                row.AddChild(Ui.Button(Ui.ButtonKind.Secondary, _confirm == key ? "Overwrite?" : "Save over", "Replace this save with the voyage as it is now",
                    () => Confirm(key, () => { SaveSlot?.Invoke(slot); Close(); })));
            }
            else
            {
                row.AddChild(Ui.Button(Ui.ButtonKind.Primary, "Load", "Load this voyage", () => LoadSlot?.Invoke(slot)));
                row.AddChild(Ui.Button(Ui.ButtonKind.Tertiary, _confirm == key ? "Delete?" : "Delete", "Delete this save for good",
                    () => Confirm(key, () => { _saves.Delete(slot); Build(); })));
            }
        }

        Item("Back", "", () => Open(_back));
    }

    private void Confirm(string key, Action act)
    {
        if (_confirm == key)
        {
            _confirm = "";
            act();
            return;
        }

        _confirm = key;
        Build();
    }

    private void NewSave(string name)
    {
        var slot = Regex.Replace(name.Trim(), @"[^\p{L}\p{N} _\-,']", "").Trim();
        if (slot.Length == 0)
        {
            slot = $"Voyage {DateTime.Now:yyyy-MM-dd HH.mm}";
        }

        SaveSlot?.Invoke(slot);
        Close();
    }

    private void BuildSettings()
    {
        _body.AddChild(Ui.Title("Settings", Ui.TextDisplay - 4, Ui.Gold));
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", Ui.SpaceL);
        grid.AddThemeConstantOverride("v_separation", Ui.SpaceM);
        _body.AddChild(grid);

        grid.AddChild(Ui.Text("Volume", Ui.TextBody, Ui.Ink));
        var volume = new HSlider { MinValue = 0, MaxValue = 100, Step = 5, Value = GameSettings.Volume * 100, CustomMinimumSize = new Vector2(240, 24) };
        volume.ValueChanged += v => { GameSettings.Volume = (float)v / 100f; Changed(); };
        grid.AddChild(volume);

        grid.AddChild(Ui.Text("Window", Ui.TextBody, Ui.Ink));
        var window = new OptionButton();
        window.AddItem("In a window", 0);
        window.AddItem("Full screen", 1);
        window.Selected = GameSettings.Fullscreen ? 1 : 0;
        window.ItemSelected += i => { GameSettings.Fullscreen = i == 1; Changed(); };
        grid.AddChild(window);

        grid.AddChild(Ui.Text("Interface size", Ui.TextBody, Ui.Ink));
        var size = new OptionButton();
        float[] sizes = [0.9f, 1f, 1.15f, 1.3f];
        foreach (var (s, i) in sizes.Select((s, i) => (s, i)))
        {
            size.AddItem($"{s:P0}", i);
        }

        size.Selected = Math.Max(0, Array.FindIndex(sizes, s => Math.Abs(s - GameSettings.UiScale) < 0.01f));
        size.ItemSelected += i => { GameSettings.UiScale = sizes[i]; Changed(); };
        grid.AddChild(size);

        grid.AddChild(Ui.Text("Edge of screen", Ui.TextBody, Ui.Ink));
        var edge = new CheckBox { Text = "Move the view when the mouse is at the edge", ButtonPressed = GameSettings.EdgePan, FocusMode = Control.FocusModeEnum.None };
        edge.Toggled += on => { GameSettings.EdgePan = on; Changed(); };
        grid.AddChild(edge);

        _body.AddChild(new HSeparator());
        _body.AddChild(Ui.Heading("Pause the game for"));
        var pauses = new GridContainer { Columns = 2 };
        pauses.AddThemeConstantOverride("h_separation", Ui.SpaceL);
        _body.AddChild(pauses);
        foreach (var (category, label) in GameSettings.PauseCategories)
        {
            var box = new CheckBox { Text = label, ButtonPressed = GameSettings.AutoPause.Contains(category), FocusMode = Control.FocusModeEnum.None };
            box.Toggled += on =>
            {
                if (on) GameSettings.AutoPause.Add(category); else GameSettings.AutoPause.Remove(category);
                Changed();
            };
            pauses.AddChild(box);
        }

        Item("Back", "", () => Open(_back));
    }

    private void Changed()
    {
        GameSettings.Apply(GetTree().Root);
        GameSettings.Save();
        SettingsChanged?.Invoke();
    }

    // ================================================================ helpers

    private void Item(string label, string tip, Action pressed, bool primary = false, bool enabled = true)
    {
        var button = Ui.Button(primary ? Ui.ButtonKind.Primary : Ui.ButtonKind.Secondary, label, tip, pressed);
        button.Disabled = !enabled;
        button.CustomMinimumSize = new Vector2(0, 40);
        button.AddThemeFontOverride("font", Ui.DisplayFont);
        button.AddThemeFontSizeOverride("font_size", Ui.TextHeading);
        _body.AddChild(button);
        if (tip.Length > 0 && _screen == Screen.Title)
        {
            var note = Ui.Text(tip, Ui.TextMeta, Ui.InkSoft, 420);
            note.HorizontalAlignment = HorizontalAlignment.Center;
            _body.AddChild(note);
        }
    }

    // The saves, newest first.
    private List<(string Slot, DateTime Written)> Saves()
    {
        try
        {
            return _saves.ListSlots().Select(s => (s, _saves.Describe(s).Written)).OrderByDescending(s => s.Written).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    // "Day 14, at sea · saved 23 Oct, 14:05".
    private string Describe(string slot)
    {
        var (written, phase, tick) = _saves.Describe(slot);
        var where = phase switch
        {
            "PortPreparation" => "in port at La Bocana",
            "Voyage" => "at sea",
            "ShipwreckSurvival" => "wrecked",
            null => "",
            _ => Regex.Replace(phase, "(?<!^)([A-Z])", " $1").ToLowerInvariant(),
        };
        var day = tick is { } t ? $"Day {t / Expedition.Simulation.World.SimulationClock.TicksPerDay + 1}" : "";
        var what = string.Join(", ", new[] { day, where }.Where(s => s.Length > 0));
        return $"{what}{(what.Length > 0 ? " · " : "")}saved {written:d MMM, HH:mm}";
    }
}

/// <summary>The player's settings, kept in user://settings.cfg across sessions.</summary>
public static class GameSettings
{
    private const string Path = "user://settings.cfg";

    /// <summary>Master volume, 0..1.</summary>
    public static float Volume { get; set; } = 0.8f;

    /// <summary>Full screen, or in a window.</summary>
    public static bool Fullscreen { get; set; }

    /// <summary>Interface size, as a scale.</summary>
    public static float UiScale { get; set; } = 1f;

    /// <summary>Move the camera when the mouse is at the edge of the screen.</summary>
    public static bool EdgePan { get; set; }

    /// <summary>What pauses the game when it happens.</summary>
    public static HashSet<AutoPause> AutoPause { get; } =
        [Game.AutoPause.InjuryAndDeath, Game.AutoPause.Weather, Game.AutoPause.ShipDamage, Game.AutoPause.Shortage, Game.AutoPause.Events, Game.AutoPause.Landfall, Game.AutoPause.Called];

    /// <summary>The kinds of thing that can pause the game, as the settings list them.</summary>
    public static readonly (AutoPause Category, string Label)[] PauseCategories =
    [
        (Game.AutoPause.InjuryAndDeath, "Injury and death"),
        (Game.AutoPause.Weather, "Heavy weather"),
        (Game.AutoPause.ShipDamage, "Damage to the ship"),
        (Game.AutoPause.Shortage, "Food or water running short"),
        (Game.AutoPause.Landfall, "Landfall"),
        (Game.AutoPause.Crew, "The crew: sickness, grievances, skills"),
        (Game.AutoPause.Called, "When the standing orders call me"),
    ];

    /// <summary>Reads the settings, if there are any.</summary>
    public static void Load()
    {
        var file = new ConfigFile();
        if (file.Load(Path) != Error.Ok)
        {
            return;
        }

        Volume = (float)file.GetValue("sound", "volume", Volume).AsDouble();
        Fullscreen = file.GetValue("window", "fullscreen", Fullscreen).AsBool();
        UiScale = (float)file.GetValue("interface", "scale", UiScale).AsDouble();
        EdgePan = file.GetValue("interface", "edge_pan", EdgePan).AsBool();
        if (file.HasSectionKey("interface", "pause"))
        {
            AutoPause.Clear();
            foreach (var name in file.GetValue("interface", "pause").AsStringArray())
            {
                if (Enum.TryParse<AutoPause>(name, out var category))
                {
                    AutoPause.Add(category);
                }
            }
        }
    }

    /// <summary>Writes the settings.</summary>
    public static void Save()
    {
        var file = new ConfigFile();
        file.SetValue("sound", "volume", Volume);
        file.SetValue("window", "fullscreen", Fullscreen);
        file.SetValue("interface", "scale", UiScale);
        file.SetValue("interface", "edge_pan", EdgePan);
        file.SetValue("interface", "pause", AutoPause.Select(a => a.ToString()).Order().ToArray());
        file.Save(Path);
    }

    /// <summary>Puts the settings into effect: the sound, the window, the interface size.</summary>
    public static void Apply(Window root)
    {
        AudioServer.SetBusVolumeDb(0, Volume <= 0.001f ? -80f : Mathf.LinearToDb(Volume));
        AudioServer.SetBusMute(0, Volume <= 0.001f);
        var mode = Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed;
        if (DisplayServer.GetName() != "headless" && DisplayServer.WindowGetMode() != mode)
        {
            DisplayServer.WindowSetMode(mode);
        }

        root.ContentScaleFactor = UiScale;
    }
}
