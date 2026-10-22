using Godot;

namespace Expedition.Game;

/// <summary>How much something needs the player (UI_UX_DESIGN_SYSTEM.md §8, §17).</summary>
public enum Severity
{
    /// <summary>Routine; nothing to do.</summary>
    Information,

    /// <summary>Stable and good.</summary>
    Positive,

    /// <summary>Worth reviewing.</summary>
    Attention,

    /// <summary>Action likely needed.</summary>
    Urgent,

    /// <summary>Immediate threat.</summary>
    Critical,
}

/// <summary>
/// Design tokens for the whole interface (UI_UX_DESIGN_SYSTEM.md §5–11): one palette, spacing scale,
/// type scale and hit size, and the theme built from them. Every panel in every phase uses these, so
/// the port, the voyage and later the settlement read as one interface family. Colours carry meaning
/// only together with an icon or word (§8).
/// </summary>
public static class Ui
{
    // ---- palette: warm ink on dark, translucent panels; restrained semantic colours

    /// <summary>Main text.</summary>
    public static readonly Color Ink = new(0.94f, 0.91f, 0.85f);

    /// <summary>Secondary text: metadata, captions.</summary>
    public static readonly Color InkSoft = new(0.94f, 0.91f, 0.85f, 0.68f);

    /// <summary>Headings and the selected state: a brass tone.</summary>
    public static readonly Color Brass = new(0.93f, 0.8f, 0.52f);

    /// <summary>Quiet HUD panels.</summary>
    public static readonly Color Panel = new(0.06f, 0.05f, 0.045f, 0.62f);

    /// <summary>Management drawers: denser, more opaque.</summary>
    public static readonly Color Drawer = new(0.08f, 0.065f, 0.05f, 0.9f);

    /// <summary>Documents the captain holds (cards, journal, manifest): warm paper-dark.</summary>
    public static readonly Color Paper = new(0.15f, 0.12f, 0.085f, 0.92f);

    /// <summary>Thin borders on documents.</summary>
    public static readonly Color Rule = new(0.62f, 0.5f, 0.3f, 0.75f);

    /// <summary>Colour for a severity. Chosen to stay distinct with common colour-vision deficiencies
    /// (blue, cream, amber, orange-red, magenta-red differ in lightness too), and always paired with an icon.</summary>
    public static Color Of(Severity severity) => severity switch
    {
        Severity.Positive => new Color(0.62f, 0.86f, 0.9f),
        Severity.Attention => new Color(1f, 0.82f, 0.42f),
        Severity.Urgent => new Color(1f, 0.6f, 0.3f),
        Severity.Critical => new Color(1f, 0.42f, 0.5f),
        _ => Ink,
    };

    /// <summary>The icon for a severity (§9): shape as well as colour.</summary>
    public static string IconOf(Severity severity) => severity switch
    {
        Severity.Positive => "✓",
        Severity.Attention => "◆",
        Severity.Urgent => "▲",
        Severity.Critical => "✖",
        _ => "•",
    };

    /// <summary>Uncertain or unverified information: dimmer, cooler, never bright.</summary>
    public static readonly Color Uncertain = new(0.78f, 0.82f, 0.9f, 0.85f);

    /// <summary>Disabled controls.</summary>
    public static readonly Color Disabled = new(0.94f, 0.91f, 0.85f, 0.38f);

    // ---- frequent-concept icons (§9). Plain glyphs until drawn icons exist; always with a tooltip or word.

    /// <summary>Time.</summary>
    public const string IconTime = "◷";

    /// <summary>Water.</summary>
    public const string IconWater = "≈";

    /// <summary>Food.</summary>
    public const string IconFood = "✦";

    /// <summary>People and crew.</summary>
    public const string IconPeople = "☺";

    /// <summary>Wind and weather.</summary>
    public const string IconWind = "≋";

    /// <summary>The ship.</summary>
    public const string IconShip = "⚓";

    /// <summary>Money.</summary>
    public const string IconPurse = "¤";

    /// <summary>A lead or objective.</summary>
    public const string IconLead = "➤";

    // ---- spacing and sizes (§10)

    /// <summary>Spacing scale in pixels at the 1280 × 720 base resolution.</summary>
    public const int SpaceXs = 2, SpaceS = 4, SpaceM = 8, SpaceL = 12, SpaceXl = 18;

    /// <summary>Smallest interactive target (§10).</summary>
    public const int MinHit = 26;

    /// <summary>Corner radius.</summary>
    public const int Radius = 4;

    /// <summary>Type roles (§7), in pixels at the base resolution.</summary>
    public const int TextDisplay = 22, TextHeading = 15, TextBody = 13, TextMeta = 11, TextNumeric = 13;

    /// <summary>Ordinary transitions (§52), in seconds.</summary>
    public const float Transition = 0.18f;

    /// <summary>The shared theme: buttons with every state (§11), labels, panels, tooltips.</summary>
    public static Theme BuildTheme()
    {
        var theme = new Theme { DefaultFontSize = TextBody };
        theme.SetStylebox("panel", "PanelContainer", Box(Panel));
        theme.SetStylebox("panel", "TooltipPanel", Box(new Color(0.1f, 0.08f, 0.06f, 0.96f), Rule));
        theme.SetColor("font_color", "TooltipLabel", Ink);
        theme.SetFontSize("font_size", "TooltipLabel", TextMeta + 1);

        var states = new Dictionary<string, (Color Fill, Color? Border)>
        {
            ["normal"] = (new Color(0.17f, 0.14f, 0.11f, 0.78f), null),
            ["hover"] = (new Color(0.27f, 0.22f, 0.16f, 0.9f), null),
            ["pressed"] = (new Color(0.42f, 0.33f, 0.19f, 0.95f), Brass),
            ["focus"] = (new Color(0, 0, 0, 0), Brass),
            ["disabled"] = (new Color(0.12f, 0.1f, 0.08f, 0.5f), null),
            ["hover_pressed"] = (new Color(0.46f, 0.36f, 0.21f, 0.95f), Brass),
        };
        foreach (var (state, (fill, border)) in states)
        {
            var box = Box(fill, border, SpaceM, SpaceS);
            box.CornerRadiusTopLeft = box.CornerRadiusTopRight = box.CornerRadiusBottomLeft = box.CornerRadiusBottomRight = 3;
            theme.SetStylebox(state, "Button", box);
            theme.SetStylebox(state, "OptionButton", box);
        }

        theme.SetColor("font_color", "Label", Ink);
        theme.SetColor("font_color", "Button", Ink);
        theme.SetColor("font_hover_color", "Button", Ink);
        theme.SetColor("font_pressed_color", "Button", Brass);
        theme.SetColor("font_hover_pressed_color", "Button", Brass);
        theme.SetColor("font_focus_color", "Button", Ink);
        theme.SetColor("font_disabled_color", "Button", Disabled);
        theme.SetColor("font_color", "CheckBox", Ink);
        theme.SetColor("font_hover_color", "CheckBox", Brass);
        theme.SetColor("font_pressed_color", "CheckBox", Ink);
        theme.SetColor("font_hover_pressed_color", "CheckBox", Brass);
        theme.SetIcon("unchecked", "CheckBox", CheckIcon(false));
        theme.SetIcon("checked", "CheckBox", CheckIcon(true));
        var flat = new StyleBoxEmpty { ContentMarginLeft = SpaceS, ContentMarginRight = SpaceS, ContentMarginTop = SpaceXs, ContentMarginBottom = SpaceXs };
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" })
        {
            theme.SetStylebox(state, "CheckBox", flat);
        }
        theme.SetConstant("separation", "VBoxContainer", SpaceS);
        theme.SetConstant("separation", "HBoxContainer", SpaceM);
        return theme;
    }

    // A checkbox drawn in brass: an open square, or the square with a tick-mark fill.
    private static ImageTexture CheckIcon(bool on)
    {
        const int size = 16;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(new Color(0, 0, 0, 0));
        for (var y = 1; y < size - 1; y++)
        {
            for (var x = 1; x < size - 1; x++)
            {
                var edge = x == 1 || y == 1 || x == size - 2 || y == size - 2;
                if (edge)
                {
                    image.SetPixel(x, y, Brass);
                }
                else if (on && x >= 4 && y >= 4 && x <= size - 5 && y <= size - 5)
                {
                    image.SetPixel(x, y, Brass);
                }
                else
                {
                    image.SetPixel(x, y, new Color(0, 0, 0, 0.35f));
                }
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>A panel style: fill, optional border, padding.</summary>
    public static StyleBoxFlat Box(Color fill, Color? border = null, int padX = SpaceL, int padY = SpaceM)
    {
        var box = new StyleBoxFlat
        {
            BgColor = fill,
            CornerRadiusTopLeft = Radius, CornerRadiusTopRight = Radius, CornerRadiusBottomLeft = Radius, CornerRadiusBottomRight = Radius,
            ContentMarginLeft = padX, ContentMarginRight = padX, ContentMarginTop = padY, ContentMarginBottom = padY,
        };
        if (border is { } rule)
        {
            box.BorderColor = rule;
            box.BorderWidthTop = box.BorderWidthBottom = box.BorderWidthLeft = box.BorderWidthRight = 1;
        }

        return box;
    }

    /// <summary>A label in a type role.</summary>
    public static Label Text(string text = "", int size = TextBody, Color? colour = null, float wrapWidth = 0)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        if (colour is { } c) label.AddThemeColorOverride("font_color", c);
        if (wrapWidth > 0)
        {
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(wrapWidth, 0);
        }

        return label;
    }

    /// <summary>A section heading.</summary>
    public static Label Heading(string text) => Text(text, TextHeading, Brass);

    /// <summary>A button with a minimum hit size and a tooltip explaining it (§41, §45).</summary>
    public static Button Button(string text, string tooltip = "", Action? pressed = null, bool toggle = false)
    {
        var button = new Button
        {
            Text = text, TooltipText = tooltip, ToggleMode = toggle, FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, MinHit),
        };
        if (pressed is not null) button.Pressed += pressed;
        return button;
    }

    /// <summary>A panel container with a style.</summary>
    /// <summary>
    /// Puts one of the interface icons (Assets/UI/icons, white silhouettes) above a button's label, in its
    /// text colour, as the screen buttons along the bottom show it.
    /// </summary>
    public static void WithIcon(Button button, string icon, int size = 26)
    {
        var path = $"res://Assets/UI/icons/{icon}.png";
        if (!ResourceLoader.Exists(path))
        {
            return;
        }

        button.Icon = GD.Load<Texture2D>(path);
        button.IconAlignment = HorizontalAlignment.Center;
        button.VerticalIconAlignment = VerticalAlignment.Top;
        button.AddThemeConstantOverride("icon_max_width", size);
        button.AddThemeColorOverride("icon_normal_color", Ink);
        button.AddThemeColorOverride("icon_hover_color", Brass);
        button.AddThemeColorOverride("icon_pressed_color", Brass);
        button.AddThemeColorOverride("icon_hover_pressed_color", Brass);
        button.AddThemeColorOverride("icon_disabled_color", Disabled);
        button.CustomMinimumSize = new Vector2(Math.Max(button.CustomMinimumSize.X, 64), 0);
    }

    public static PanelContainer PanelOf(Color fill, Color? border = null, int padX = SpaceL, int padY = SpaceM)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Box(fill, border, padX, padY));
        return panel;
    }

    /// <summary>
    /// A status chip: icon and short text, coloured by severity, with a tooltip saying what it is, why it
    /// matters and what affects it (§41).
    /// </summary>
    public static Label Chip(string icon, string text, Severity severity, string tooltip = "")
    {
        var label = Text($"{icon} {text}", TextBody, Of(severity));
        label.TooltipText = tooltip;
        label.MouseFilter = Control.MouseFilterEnum.Stop;
        return label;
    }

    /// <summary>Updates a chip made by <see cref="Chip"/>.</summary>
    public static void SetChip(Label chip, string icon, string text, Severity severity, string tooltip)
    {
        chip.Text = $"{icon} {text}";
        chip.AddThemeColorOverride("font_color", Of(severity));
        chip.TooltipText = tooltip;
    }

    /// <summary>
    /// A status pill: a small opaque button with an icon and a few words, its border and text in the
    /// severity's colour. Clicking it goes where the matter is dealt with; the tooltip says why it matters.
    /// </summary>
    public static Button Pill()
    {
        var pill = new Button { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, MinHit) };
        pill.AddThemeFontSizeOverride("font_size", TextBody);
        return pill;
    }

    /// <summary>Updates a pill made by <see cref="Pill"/>.</summary>
    public static void SetPill(Button pill, string icon, string text, Severity severity, string tooltip)
    {
        pill.Text = icon.Length > 0 ? $"{icon} {text}" : text;
        pill.TooltipText = tooltip;
        var colour = Of(severity);
        var strong = severity is Severity.Urgent or Severity.Critical;
        foreach (var (state, lift) in new[] { ("normal", 0f), ("hover", 0.08f), ("pressed", 0.12f) })
        {
            var fill = strong ? new Color(colour.R * 0.28f + lift, colour.G * 0.12f + lift, colour.B * 0.08f + lift, 0.95f) : new Color(0.1f + lift, 0.08f + lift, 0.06f + lift, 0.94f);
            var box = Box(fill, severity == Severity.Information ? new Color(Rule, 0.45f) : colour, SpaceM, SpaceXs);
            box.CornerRadiusTopLeft = box.CornerRadiusTopRight = box.CornerRadiusBottomLeft = box.CornerRadiusBottomRight = 11;
            pill.AddThemeStyleboxOverride(state, box);
        }

        foreach (var key in new[] { "font_color", "font_hover_color", "font_pressed_color" })
        {
            pill.AddThemeColorOverride(key, colour);
        }
    }

    /// <summary>A section heading over a thin brass rule, for the parts of a screen.</summary>
    public static VBoxContainer SectionHeader(string text, string note = "")
    {
        var header = new VBoxContainer();
        header.AddThemeConstantOverride("separation", 1);
        var top = new HBoxContainer();
        header.AddChild(top);
        var title = Heading(text);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(title);
        if (note.Length > 0)
        {
            top.AddChild(Text(note, TextMeta, InkSoft));
        }

        header.AddChild(new ColorRect { Color = new Color(Rule, 0.6f), CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore });
        return header;
    }

    /// <summary>
    /// A card: a document-coloured panel with a heading, added to <paramref name="parent"/>. Returns the
    /// column inside it to fill.
    /// </summary>
    public static VBoxContainer Card(Control parent, string title = "", string note = "")
    {
        var panel = PanelOf(Paper, new Color(Rule, 0.35f), SpaceL, SpaceM);
        parent.AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", SpaceS);
        panel.AddChild(column);
        if (title.Length > 0)
        {
            column.AddChild(SectionHeader(title, note));
        }

        return column;
    }

    /// <summary>Fades a control in, for panels and alerts arriving (§52).</summary>
    public static void FadeIn(Control control)
    {
        control.Modulate = new Color(1, 1, 1, 0);
        control.Visible = true;
        control.CreateTween().TweenProperty(control, "modulate:a", 1f, Transition);
    }
}

/// <summary>
/// A gauge (UI_UX_DESIGN_SYSTEM §31): what it measures, a bar coloured by how it stands, and the state in
/// a word with its number. Made once, set every refresh.
/// </summary>
public sealed class Gauge
{
    private readonly ProgressBar _bar;
    private readonly Label _value;
    private readonly StyleBoxFlat _fill;

    /// <summary>Makes a gauge and adds it to <paramref name="parent"/>.</summary>
    public Gauge(Control parent, string name, float nameWidth = 70, float valueWidth = 120)
    {
        Root = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        Root.AddThemeConstantOverride("separation", Ui.SpaceM);
        parent.AddChild(Root);
        var label = Ui.Text(name, Ui.TextMeta + 1, Ui.InkSoft);
        label.CustomMinimumSize = new Vector2(nameWidth, 0);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        Root.AddChild(label);
        _bar = new ProgressBar
        {
            MinValue = 0, MaxValue = 100, ShowPercentage = false, MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(0, 9),
        };
        var back = Ui.Box(new Color(0, 0, 0, 0.45f), new Color(Ui.Rule, 0.3f), 0, 0);
        _fill = Ui.Box(Ui.Ink, null, 0, 0);
        _bar.AddThemeStyleboxOverride("background", back);
        _bar.AddThemeStyleboxOverride("fill", _fill);
        Root.AddChild(_bar);
        _value = Ui.Text("", Ui.TextMeta + 1, Ui.Ink);
        _value.CustomMinimumSize = new Vector2(valueWidth, 0);
        _value.MouseFilter = Control.MouseFilterEnum.Ignore;
        Root.AddChild(_value);
    }

    /// <summary>The row the gauge is in.</summary>
    public HBoxContainer Root { get; }

    /// <summary>Sets the bar (0..100), the words beside it, its severity and its explanation.</summary>
    public void Set(double value, string text, Severity severity, string tooltip = "")
    {
        _bar.Value = Math.Clamp(value, 0, 100);
        var colour = severity == Severity.Information ? new Color(0.78f, 0.7f, 0.55f) : Ui.Of(severity);
        _fill.BgColor = colour;
        _value.Text = text;
        _value.AddThemeColorOverride("font_color", severity is Severity.Information or Severity.Positive ? Ui.Ink : colour);
        Root.TooltipText = tooltip;
    }
}
