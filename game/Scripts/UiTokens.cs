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
/// Design tokens for the whole interface (UI_UX_DESIGN_SYSTEM.md §5–11, and the style guide in
/// docs/UI_STYLE.md): one palette, three type families, a spacing scale and hit size, the surfaces
/// (framed navy panels, parchment, insets, notices), the buttons, the icons, and the theme built from
/// them. Every panel in every phase uses these, so the port, the voyage and the settlement read as one
/// interface family. Colours carry meaning only together with an icon or word (§8).
/// </summary>
public static class Ui
{
    // ---- palette (the style guide's §1)

    /// <summary>Deep navy: the bars and the ground of every panel.</summary>
    public static readonly Color Navy = new("#0E2A3A");

    /// <summary>Teal: secondary fills, the sea on a chart.</summary>
    public static readonly Color Teal = new("#1E4B5A");

    /// <summary>Parchment: text on navy, and the ground of charts, logs and letters.</summary>
    public static readonly Color Parchment = new("#EBD7B2");

    /// <summary>Gold: frames, headings, the selected state.</summary>
    public static readonly Color Gold = new("#D4AF37");

    /// <summary>Wood, leather, stone and cloud: secondary tones for materials and quiet fills.</summary>
    public static readonly Color Wood = new("#5A3E2B"), Leather = new("#6B4B35"), Stone = new("#8B8B8B"), Cloud = new("#C7D6E3");

    /// <summary>Map colours: sea, known land, explored, unknown.</summary>
    public static readonly Color MapSea = new("#3A6F8F"), MapLand = new("#D9C28F"), MapExplored = new("#A8C5A0"), MapUnknown = new("#6C6C6C");

    /// <summary>Panel grounds: the panel, a lighter band in it, an inset well.</summary>
    public static readonly Color PanelGround = new("#1A2A35"), PanelLight = new("#243A47"), InsetGround = new("#0F1D26");

    /// <summary>Main text, on navy: parchment-cream.</summary>
    public static readonly Color Ink = new("#EDE2C8");

    /// <summary>Secondary text: metadata, captions.</summary>
    public static readonly Color InkSoft = new(0.93f, 0.89f, 0.78f, 0.66f);

    /// <summary>Ink on parchment: the brown-black of a written chart.</summary>
    public static readonly Color InkDark = new("#2B2117");

    /// <summary>Secondary ink on parchment.</summary>
    public static readonly Color InkDarkSoft = new(0.17f, 0.13f, 0.09f, 0.72f);

    /// <summary>Headings and the selected state: gold (the old name, kept).</summary>
    public static readonly Color Brass = Gold;

    /// <summary>Quiet HUD panels.</summary>
    public static readonly Color Panel = new(0.1f, 0.165f, 0.208f, 0.93f);

    /// <summary>Management drawers: denser, more opaque.</summary>
    public static readonly Color Drawer = new(0.09f, 0.15f, 0.19f, 0.97f);

    /// <summary>Cards inside a panel: the inset well.</summary>
    public static readonly Color Paper = new(0.059f, 0.114f, 0.149f, 0.9f);

    /// <summary>Thin rules and borders: gold, softened.</summary>
    public static readonly Color Rule = new(0.83f, 0.69f, 0.22f, 0.55f);

    /// <summary>Status colours (the style guide's "Good", "Warning", "Danger", "Info"), a little lighter for text on navy.</summary>
    public static readonly Color Good = new("#4DB86E"), Warning = new("#E6A23C"), Danger = new("#E0605A"), Info = new("#4AA3DF");

    /// <summary>Colour for a severity, always paired with an icon or word.</summary>
    public static Color Of(Severity severity) => severity switch
    {
        Severity.Positive => Good,
        Severity.Attention => Warning,
        Severity.Urgent => new Color("#EA7A3C"),
        Severity.Critical => Danger,
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
    public static readonly Color Uncertain = new(0.72f, 0.8f, 0.9f, 0.85f);

    /// <summary>Disabled controls.</summary>
    public static readonly Color Disabled = new(0.93f, 0.89f, 0.78f, 0.36f);

    // ---- glyphs used inside running text (the drawn icons are in Assets/UI/icons; see IconTexture)

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

    /// <summary>Spirits: how the company bears it.</summary>
    public const string IconMorale = "☻";

    /// <summary>The sick and hurt.</summary>
    public const string IconSick = "✚";

    /// <summary>The season ashore.</summary>
    public const string IconSeason = "☼";

    // The drawn icon and the short caption for each glyph, for the top bar's readings.
    private static readonly Dictionary<string, (string Icon, string Caption)> Glyphs = new()
    {
        [IconTime] = ("time", "Time"), [IconWater] = ("water", "Water"), [IconFood] = ("food", "Food"), [IconPeople] = ("crew", "Crew"),
        [IconWind] = ("wind", "Weather"), [IconShip] = ("ship", "Ship"), [IconPurse] = ("coins", "Purse"), [IconLead] = ("course", "Course"),
        [IconMorale] = ("morale", "Morale"), [IconSick] = ("medicine", "Sick"), [IconSeason] = ("weather", "Season"),
        ["✓"] = ("tick", ""), ["◆"] = ("alert", ""), ["▲"] = ("alert", ""), ["✖"] = ("alert", ""), ["•"] = ("info", ""),
    };

    // ---- spacing and sizes (§10)

    /// <summary>Spacing scale in pixels at the 1280 × 720 base resolution.</summary>
    public const int SpaceXs = 2, SpaceS = 4, SpaceM = 8, SpaceL = 12, SpaceXl = 18;

    /// <summary>Smallest interactive target (§10).</summary>
    public const int MinHit = 28;

    /// <summary>Corner radius.</summary>
    public const int Radius = 3;

    /// <summary>Type roles (§7), in pixels at the base resolution.</summary>
    public const int TextDisplay = 24, TextHeading = 17, TextBody = 14, TextMeta = 12, TextNumeric = 14;

    /// <summary>The bar across the top (date, speed, readings) and the tabs along the foot.</summary>
    public const int TopBarHeight = 58, TabBarHeight = 50;

    /// <summary>Where panels below the top bar begin, and where those above the tab bar end (from the foot).</summary>
    public const int BelowTopBar = TopBarHeight + SpaceM, AboveTabBar = TabBarHeight + SpaceM * 2;

    /// <summary>Ordinary transitions (§52), in seconds.</summary>
    public const float Transition = 0.18f;

    // ---- type (the style guide's §2): IM FELL for titles, Source Serif for reading, Source Sans for labels

    private static readonly Dictionary<string, Font> Fonts = [];

    private static Font LoadFont(string file)
    {
        if (Fonts.TryGetValue(file, out var font))
        {
            return font;
        }

        var path = $"res://Assets/Fonts/{file}";
        if (ResourceLoader.Exists(path) && GD.Load<FontFile>(path) is { } loaded)
        {
            // Glyphs a face lacks (the ⚓ and ≈ of running text) come from the engine's own font.
            loaded.Fallbacks = [ThemeDB.FallbackFont];
            font = loaded;
        }
        else
        {
            font = ThemeDB.FallbackFont;
        }

        Fonts[file] = font;
        return font;
    }

    /// <summary>Titles, headings and tabs: IM FELL Great Primer SC.</summary>
    public static Font DisplayFont => LoadFont("FellSC.ttf");

    /// <summary>Titles on a chart or a letter: IM FELL Great Primer, roman and italic.</summary>
    public static Font TitleFont => LoadFont("Fell.ttf");

    /// <summary>The italic, for cartouches and place names.</summary>
    public static Font TitleItalic => LoadFont("FellItalic.ttf");

    /// <summary>Reading text: events, letters, the chronicle.</summary>
    public static Font SerifFont => LoadFont("SourceSerif.ttf");

    /// <summary>Reading text, emphasised.</summary>
    public static Font SerifSemibold => LoadFont("SourceSerifSemibold.ttf");

    /// <summary>Labels, numbers, buttons.</summary>
    public static Font SansFont => LoadFont("SourceSans.ttf");

    /// <summary>Labels and buttons, emphasised.</summary>
    public static Font SansSemibold => LoadFont("SourceSansSemibold.ttf");

    /// <summary>Numbers that matter.</summary>
    public static Font SansBold => LoadFont("SourceSansBold.ttf");

    // ---- the calendar shown: the voyage's first day is a Friday in April 1501

    /// <summary>The first day of the campaign.</summary>
    public static readonly DateTime FirstDay = new(1501, 4, 12);

    /// <summary>The date on day <paramref name="day"/> (1 is the first), as "Fri, Apr 12, 1501".</summary>
    public static string DateOf(long day) => FirstDay.AddDays(day - 1).ToString("ddd, MMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture);

    // ---- icons (game-icons.net, CC BY 3.0; white, tinted where used)

    /// <summary>An interface icon by name (Assets/UI/icons/NAME.svg), or null if there is none.</summary>
    public static Texture2D? IconTexture(string name)
    {
        foreach (var extension in new[] { "svg", "png" })
        {
            var path = $"res://Assets/UI/icons/{name}.{extension}";
            if (ResourceLoader.Exists(path))
            {
                return GD.Load<Texture2D>(path);
            }
        }

        return null;
    }

    /// <summary>An icon as a control, <paramref name="size"/> pixels square, in <paramref name="colour"/>.</summary>
    public static TextureRect Icon(string name, int size = 18, Color? colour = null) => new()
    {
        Texture = IconTexture(name),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        CustomMinimumSize = new Vector2(size, size),
        SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        Modulate = colour ?? Gold,
        MouseFilter = Control.MouseFilterEnum.Ignore,
        TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
    };

    // ---- surfaces (the style guide's §7), drawn once into small textures and nine-sliced

    private static readonly Dictionary<string, Texture2D> Surfaces = [];

    private static Texture2D Surface(string key, int size, Func<int, int, Color> pixel)
    {
        if (Surfaces.TryGetValue(key, out var texture))
        {
            return texture;
        }

        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                image.SetPixel(x, y, pixel(x, y));
            }
        }

        texture = ImageTexture.CreateFromImage(image);
        Surfaces[key] = texture;
        return texture;
    }

    // How far a pixel is inside a rounded square of side size (negative outside).
    private static float Inside(int x, int y, int size, float radius)
    {
        var dx = Math.Max(Math.Max(radius - x - 0.5f, x + 0.5f - (size - radius)), 0);
        var dy = Math.Max(Math.Max(radius - y - 0.5f, y + 0.5f - (size - radius)), 0);
        var edge = Math.Min(Math.Min(x + 0.5f, size - x - 0.5f), Math.Min(y + 0.5f, size - y - 0.5f));
        return dx > 0 && dy > 0 ? radius - MathF.Sqrt(dx * dx + dy * dy) : edge;
    }

    private static Color Over(Color under, Color over, float alpha) => under.Lerp(new Color(over, 1), Math.Clamp(alpha * over.A, 0, 1));

    // The standard panel: navy, a black edge, a gold double rule and gold points at the corners.
    private static Texture2D FrameTexture(bool notice)
    {
        const int size = 48;
        var top = notice ? new Color("#3A1714") : new Color("#1F3443");
        var bottom = notice ? new Color("#24100E") : new Color("#13222C");
        var gold = notice ? new Color("#C9564A") : new Color("#C9A440");
        return Surface(notice ? "frame.notice" : "frame", size, (x, y) =>
        {
            var d = Inside(x, y, size, 4);
            if (d < 0)
            {
                return new Color(0, 0, 0, 0);
            }

            var fill = top.Lerp(bottom, y / (float)size);
            var c = d < 1 ? new Color("#05090C") : d < 3 ? gold.Lerp(new Color("#F1D88A"), 0.35f * (1 - y / (float)size)) : d < 4 ? new Color("#060B0F") : fill;
            if (d is >= 5.5f and < 6.5f)
            {
                c = Over(c, gold, 0.4f);
            }

            // A small diamond of gold in each corner.
            var cx = x < size / 2 ? 8 : size - 9;
            var cy = y < size / 2 ? 8 : size - 9;
            if (Math.Abs(x - cx) + Math.Abs(y - cy) <= 2)
            {
                c = gold.Lerp(new Color("#F6E3A0"), 0.4f);
            }

            return new Color(c, Math.Clamp(d, 0, 1) * (d >= 4 ? 0.95f : 1));
        });
    }

    // Parchment: a seamless field of fibres and stains, tiled; the edges are browned when it is drawn.
    private static Texture2D ParchmentTexture()
    {
        const int size = 256;
        if (Surfaces.TryGetValue("parchment", out var made))
        {
            return made;
        }

        var noise = new FastNoiseLite { Seed = 7, Frequency = 0.012f, FractalOctaves = 5, NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin };
        var field = noise.GetSeamlessImage(size, size);
        var grain = new FastNoiseLite { Seed = 3, Frequency = 0.5f, NoiseType = FastNoiseLite.NoiseTypeEnum.Value };
        var fine = grain.GetSeamlessImage(size, size);
        var light = new Color("#F0DFBC");
        var dark = new Color("#CDB283");
        return Surface("parchment", size, (x, y) =>
        {
            var c = light.Lerp(dark, Math.Clamp((field.GetPixel(x, y).R - 0.3f) * 0.9f, 0, 1));
            return c.Darkened((fine.GetPixel(x, y).R - 0.5f) * 0.04f);
        });
    }

    /// <summary>Draws parchment over <paramref name="rect"/> on <paramref name="canvas"/>: tiled, browned toward its edges, ruled round.</summary>
    public static void DrawParchment(CanvasItem canvas, Rect2 rect)
    {
        canvas.TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled;
        canvas.DrawTextureRect(ParchmentTexture(), rect, true);
        var burn = new Color("#7A5A34");
        const int depth = 18;
        for (var i = 0; i < depth; i++)
        {
            var inset = rect.Grow(-i);
            canvas.DrawRect(inset, new Color(burn, MathF.Pow(1 - i / (float)depth, 2.2f) * 0.5f), false, 1);
        }

        canvas.DrawRect(rect.Grow(-0.5f), new Color("#4A3520"), false, 1.2f);
    }

    // A bevelled button face: a gradient, a one-pixel border, a highlight along the top.
    private static Texture2D Bevel(string key, Color top, Color bottom, Color border, float shine = 0.35f)
    {
        const int size = 24;
        return Surface(key, size, (x, y) =>
        {
            var d = Inside(x, y, size, 3);
            if (d < 0)
            {
                return new Color(0, 0, 0, 0);
            }

            var c = d < 1 ? border : top.Lerp(bottom, Math.Clamp((y - 1) / (float)(size - 3), 0, 1));
            if (d is >= 1 and < 2 && y < size / 2)
            {
                c = Over(c, new Color(1, 1, 1), shine * 0.6f);
            }

            return new Color(c, Math.Clamp(d, 0, 1) * (d < 1 ? border.A : Math.Max(top.A, bottom.A)));
        });
    }

    private static StyleBoxTexture Sliced(Texture2D texture, int margin, int padX, int padY, bool tile = false) => new()
    {
        Texture = texture,
        TextureMarginLeft = margin, TextureMarginRight = margin, TextureMarginTop = margin, TextureMarginBottom = margin,
        ContentMarginLeft = padX, ContentMarginRight = padX, ContentMarginTop = padY, ContentMarginBottom = padY,
        AxisStretchHorizontal = tile ? StyleBoxTexture.AxisStretchMode.Tile : StyleBoxTexture.AxisStretchMode.Stretch,
        AxisStretchVertical = tile ? StyleBoxTexture.AxisStretchMode.Tile : StyleBoxTexture.AxisStretchMode.Stretch,
    };

    /// <summary>The standard panel: framed navy with a gold double rule.</summary>
    public static StyleBox FrameStyle(int padX = SpaceL, int padY = SpaceM) => Sliced(FrameTexture(false), 12, padX + 4, padY + 4);

    /// <summary>A notification panel: dark red, framed, for warnings that need the captain.</summary>
    public static StyleBox NoticeStyle(int padX = SpaceL, int padY = SpaceM) => Sliced(FrameTexture(true), 12, padX + 4, padY + 4);

    /// <summary>The padding of a parchment panel (its ground is drawn by <see cref="DrawParchment"/>).</summary>
    public static StyleBox ParchmentStyle(int padX = SpaceL, int padY = SpaceM) => new StyleBoxEmpty
    {
        ContentMarginLeft = padX + 6, ContentMarginRight = padX + 6, ContentMarginTop = padY + 6, ContentMarginBottom = padY + 6,
    };

    /// <summary>An inset well inside a panel: for sub-sections and secondary information.</summary>
    public static StyleBoxFlat InsetStyle(int padX = SpaceL, int padY = SpaceM)
    {
        var box = Box(Paper, new Color(Gold, 0.28f), padX, padY);
        box.ShadowColor = new Color(0, 0, 0, 0.25f);
        box.ShadowSize = 2;
        return box;
    }

    /// <summary>A framed panel container.</summary>
    public static PanelContainer Frame(int padX = SpaceL, int padY = SpaceM)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", FrameStyle(padX, padY));
        return panel;
    }

    /// <summary>A parchment panel container. Text on it should be <see cref="InkDark"/>.</summary>
    public static PanelContainer ParchmentPanel(int padX = SpaceL, int padY = SpaceM)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Box(new Color(0, 0, 0, 0), null, 0, 0));
        // The fill is a child under the contents, the size of the whole panel.
        var fill = new ParchmentFill { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddChild(fill);
        fill.Resized += fill.QueueRedraw;
        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        foreach (var (side, by) in new[] { ("left", padX), ("right", padX), ("top", padY), ("bottom", padY) })
        {
            margin.AddThemeConstantOverride($"margin_{side}", by + 6);
        }

        panel.AddChild(margin);
        panel.SetMeta("content", margin);
        return panel;
    }

    /// <summary>Where to put the contents of a panel made by <see cref="ParchmentPanel"/>.</summary>
    public static Container ContentOf(PanelContainer parchment) => parchment.GetMeta("content").As<Container>();

    // ---- buttons (the style guide's §6)

    /// <summary>The kinds of button.</summary>
    public enum ButtonKind
    {
        /// <summary>Navy with a gold rule: most buttons.</summary>
        Secondary,

        /// <summary>Gold: the one thing to do here (hire, place, set sail).</summary>
        Primary,

        /// <summary>Slate: quiet, less important.</summary>
        Tertiary,

        /// <summary>Blue: an action taken in the world (take the sight, send them).</summary>
        Action,

        /// <summary>A tab along the foot or a segment: active in gold.</summary>
        Tab,
    }

    private static (Color Top, Color Bottom, Color Border, Color Text) Face(ButtonKind kind, string state) => (kind, state) switch
    {
        (ButtonKind.Primary, "hover") => (new("#F7DE8C"), new("#C9973A"), new("#6B4C14"), new("#1A2128")),
        (ButtonKind.Primary, "pressed" or "hover_pressed") => (new("#C9973A"), new("#A87A26"), new("#5A3E0E"), new("#1A2128")),
        (ButtonKind.Primary, _) => (new("#EDCB6E"), new("#B8862E"), new("#5E4210"), new("#1A2128")),
        (ButtonKind.Tertiary, "hover") => (new("#66727D"), new("#47515A"), new("#929DA6"), new("#F0EADC")),
        (ButtonKind.Tertiary, "pressed" or "hover_pressed") => (new("#3E474F"), new("#2F363C"), new("#B9C2C9"), new("#F0EADC")),
        (ButtonKind.Tertiary, _) => (new("#56616B"), new("#3B444C"), new("#7D8891"), new("#E6E0D2")),
        (ButtonKind.Action, "hover") => (new("#4A92D8"), new("#2563A8"), new("#A6D2F5"), new("#FFFFFF")),
        (ButtonKind.Action, "pressed" or "hover_pressed") => (new("#2462A8"), new("#1A4A80"), new("#C6E2F8"), new("#FFFFFF")),
        (ButtonKind.Action, _) => (new("#3B82C9"), new("#1F5596"), new("#8CC4F0"), new("#F4F8FC")),
        (_, "pressed" or "hover_pressed") => (new("#8A6628"), new("#4E3814"), new("#EACB6A"), new("#F6DD8E")),
        (_, "hover") => (new("#28495F"), new("#183245"), new("#E2C062"), new("#F6EBD2")),
        (ButtonKind.Tab, _) => (new("#1A3244"), new("#0F2131"), new Color("#B8933A", 0.75f), Ink),
        _ => (new("#1D3A4F"), new("#122637"), new Color("#B8933A", 0.9f), Ink),
    };

    private static readonly string[] ButtonStates = ["normal", "hover", "pressed", "hover_pressed", "disabled", "focus"];

    // The styles for one kind of button, by state.
    private static IEnumerable<(string State, StyleBox Box, Color Text)> ButtonStyles(ButtonKind kind, int padX = SpaceM + 2, int padY = SpaceS)
    {
        foreach (var state in ButtonStates)
        {
            if (state == "focus")
            {
                yield return (state, new StyleBoxEmpty(), Ink);
                continue;
            }

            var (top, bottom, border, text) = state == "disabled"
                ? (new Color("#1A2731", 0.85f), new Color("#141E26", 0.85f), new Color("#3A4A56", 0.8f), Disabled)
                : Face(kind, state);
            yield return (state, Sliced(Bevel($"button.{kind}.{state}", top, bottom, border, kind == ButtonKind.Primary ? 0.6f : 0.3f), 5, padX, padY), text);
        }
    }

    /// <summary>Gives a button the look of a kind (primary gold, tertiary slate, action blue, tab).</summary>
    public static T Styled<T>(T button, ButtonKind kind) where T : BaseButton
    {
        foreach (var (state, box, text) in ButtonStyles(kind))
        {
            button.AddThemeStyleboxOverride(state, box);
            var key = state switch { "normal" => "font_color", "focus" => "font_focus_color", _ => $"font_{state}_color" };
            button.AddThemeColorOverride(key, text);
            if (state != "focus" && button is Button withIcon)
            {
                withIcon.AddThemeColorOverride(state == "normal" ? "icon_normal_color" : $"icon_{state}_color", state is "pressed" or "hover_pressed" ? Face(kind, state).Text : kind is ButtonKind.Primary ? text : state == "disabled" ? Disabled : Gold);
            }
        }

        if (kind == ButtonKind.Tab)
        {
            button.AddThemeFontOverride("font", DisplayFont);
        }

        return button;
    }

    /// <summary>The shared theme: every control in the palette and type of the style guide.</summary>
    public static Theme BuildTheme()
    {
        var theme = new Theme { DefaultFontSize = TextBody, DefaultFont = SansFont };
        theme.SetStylebox("panel", "PanelContainer", FrameStyle());
        theme.SetStylebox("panel", "Panel", FrameStyle());
        theme.SetStylebox("panel", "TooltipPanel", Sliced(FrameTexture(false), 12, SpaceL, SpaceM));
        theme.SetColor("font_color", "TooltipLabel", Ink);
        theme.SetFontSize("font_size", "TooltipLabel", TextMeta + 1);
        theme.SetFont("font", "TooltipLabel", SansFont);

        foreach (var type in new[] { "Button", "OptionButton", "MenuButton" })
        {
            foreach (var (state, box, text) in ButtonStyles(ButtonKind.Secondary))
            {
                theme.SetStylebox(state, type, box);
                theme.SetColor(state switch { "normal" => "font_color", "focus" => "font_focus_color", _ => $"font_{state}_color" }, type, text);
            }

            theme.SetFont("font", type, SansSemibold);
            theme.SetColor("icon_normal_color", type, Gold);
            theme.SetColor("icon_hover_color", type, new Color("#F6E3A0"));
            theme.SetColor("icon_pressed_color", type, new Color("#F6DD8E"));
            theme.SetColor("icon_hover_pressed_color", type, new Color("#F6DD8E"));
            theme.SetColor("icon_disabled_color", type, Disabled);
        }

        theme.SetColor("font_color", "Label", Ink);
        theme.SetFont("font", "Label", SansFont);
        theme.SetFont("normal_font", "RichTextLabel", SerifFont);
        theme.SetFont("bold_font", "RichTextLabel", SerifSemibold);
        theme.SetFont("italics_font", "RichTextLabel", LoadFont("SourceSerifItalic.ttf"));
        theme.SetColor("default_color", "RichTextLabel", Ink);

        theme.SetColor("font_color", "CheckBox", Ink);
        theme.SetColor("font_hover_color", "CheckBox", new Color("#F6E3A0"));
        theme.SetColor("font_pressed_color", "CheckBox", Ink);
        theme.SetColor("font_hover_pressed_color", "CheckBox", new Color("#F6E3A0"));
        theme.SetColor("font_disabled_color", "CheckBox", Disabled);
        theme.SetIcon("unchecked", "CheckBox", CheckIcon(false));
        theme.SetIcon("checked", "CheckBox", CheckIcon(true));
        theme.SetIcon("unchecked_disabled", "CheckBox", CheckIcon(false));
        theme.SetIcon("checked_disabled", "CheckBox", CheckIcon(true));
        theme.SetFont("font", "CheckBox", SansFont);
        var flat = new StyleBoxEmpty { ContentMarginLeft = SpaceS, ContentMarginRight = SpaceS, ContentMarginTop = SpaceXs, ContentMarginBottom = SpaceXs };
        foreach (var state in ButtonStates)
        {
            theme.SetStylebox(state, "CheckBox", flat);
        }

        // Fields: an inset with a gold rule, brighter when typing.
        var field = Box(InsetGround, new Color(Gold, 0.35f), SpaceM, SpaceS);
        var focus = Box(InsetGround, Gold, SpaceM, SpaceS);
        foreach (var type in new[] { "LineEdit", "TextEdit" })
        {
            theme.SetStylebox("normal", type, field);
            theme.SetStylebox("focus", type, focus);
            theme.SetStylebox("read_only", type, Box(new Color(InsetGround, 0.6f), new Color(Gold, 0.18f), SpaceM, SpaceS));
            theme.SetColor("font_color", type, Ink);
            theme.SetColor("font_placeholder_color", type, InkSoft);
            theme.SetColor("caret_color", type, Gold);
            theme.SetColor("selection_color", type, new Color(Gold, 0.35f));
            theme.SetFont("font", type, SansFont);
        }

        // Menus: a framed panel, the hovered line in gold.
        theme.SetStylebox("panel", "PopupMenu", Sliced(FrameTexture(false), 12, SpaceM, SpaceM));
        theme.SetStylebox("hover", "PopupMenu", Box(new Color(Gold, 0.22f), new Color(Gold, 0.6f), SpaceM, SpaceXs));
        theme.SetColor("font_color", "PopupMenu", Ink);
        theme.SetColor("font_hover_color", "PopupMenu", new Color("#F6E3A0"));
        theme.SetColor("font_disabled_color", "PopupMenu", Disabled);
        theme.SetColor("font_separator_color", "PopupMenu", Gold);
        theme.SetFont("font", "PopupMenu", SansFont);
        theme.SetFont("font_separator", "PopupMenu", DisplayFont);
        theme.SetFontSize("font_size", "PopupMenu", TextBody);
        theme.SetIcon("checked", "PopupMenu", CheckIcon(true));
        theme.SetIcon("unchecked", "PopupMenu", CheckIcon(false));
        theme.SetIcon("radio_checked", "PopupMenu", CheckIcon(true));
        theme.SetIcon("radio_unchecked", "PopupMenu", CheckIcon(false));
        theme.SetConstant("v_separation", "PopupMenu", SpaceS + 2);
        theme.SetStylebox("separator", "PopupMenu", Rule1());
        theme.SetStylebox("labeled_separator_left", "PopupMenu", Rule1());
        theme.SetStylebox("labeled_separator_right", "PopupMenu", Rule1());

        // Rules and scrollbars.
        theme.SetStylebox("separator", "HSeparator", Rule1());
        theme.SetConstant("separation", "HSeparator", SpaceM);
        foreach (var type in new[] { "VScrollBar", "HScrollBar" })
        {
            theme.SetStylebox("scroll", type, Box(new Color(InsetGround, 0.7f), null, 3, 3));
            theme.SetStylebox("grabber", type, Box(new Color(Gold, 0.55f), null, 3, 3));
            theme.SetStylebox("grabber_highlight", type, Box(new Color(Gold, 0.8f), null, 3, 3));
            theme.SetStylebox("grabber_pressed", type, Box(Gold, null, 3, 3));
        }

        theme.SetStylebox("background", "ProgressBar", Box(InsetGround, new Color(Gold, 0.25f), 0, 0));
        theme.SetStylebox("fill", "ProgressBar", Box(Gold, null, 0, 0));
        theme.SetColor("font_color", "ProgressBar", Ink);

        theme.SetConstant("separation", "VBoxContainer", SpaceS);
        theme.SetConstant("separation", "HBoxContainer", SpaceM);
        return theme;
    }

    // A thin gold rule, for separators.
    private static StyleBoxLine Rule1() => new() { Color = new Color(Gold, 0.4f), Thickness = 1, GrowBegin = -2, GrowEnd = -2 };

    // A checkbox drawn in gold: an inset square, ticked with a gold mark.
    private static Texture2D CheckIcon(bool on)
    {
        const int size = 18;
        return Surface(on ? "check.on" : "check.off", size, (x, y) =>
        {
            var d = Inside(x, y, size, 2);
            if (d < 0.5f)
            {
                return new Color(0, 0, 0, 0);
            }

            if (d < 1.6f)
            {
                return Gold;
            }

            // The tick: two strokes, short down-right then long up-right.
            var tick = on && (Math.Abs((y - 9) - (x - 4)) <= 1.2f && x is >= 4 and <= 8 || Math.Abs((y - 13) + (x - 8) * 1.1f) <= 1.3f && x is >= 7 and <= 14);
            return tick ? new Color("#F6E3A0") : new Color(InsetGround, 0.9f);
        });
    }

    /// <summary>A flat panel style: fill, optional border, padding.</summary>
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
        // A heading in gold is set in the display face, wherever it is made.
        if (colour == Gold && size >= TextHeading) label.AddThemeFontOverride("font", DisplayFont);
        if (wrapWidth > 0)
        {
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(wrapWidth, 0);
        }

        return label;
    }

    /// <summary>Reading text, in the serif: events, letters, the record.</summary>
    public static Label Prose(string text = "", int size = TextBody, Color? colour = null, float wrapWidth = 0)
    {
        var label = Text(text, size + 1, colour, wrapWidth);
        label.AddThemeFontOverride("font", SerifFont);
        return label;
    }

    /// <summary>A title in the display face (IM FELL small capitals).</summary>
    public static Label Title(string text, int size = TextDisplay, Color? colour = null)
    {
        var label = Text(text, size, colour ?? Gold);
        label.AddThemeFontOverride("font", DisplayFont);
        return label;
    }

    /// <summary>A section heading.</summary>
    public static Label Heading(string text) => Title(text, TextHeading, Gold);

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

    /// <summary>A button of a kind (see <see cref="ButtonKind"/>).</summary>
    public static Button Button(ButtonKind kind, string text, string tooltip = "", Action? pressed = null, bool toggle = false) =>
        Styled(Button(text, tooltip, pressed, toggle), kind);

    /// <summary>
    /// Puts one of the interface icons on a button: beside its label (a tab), or above it.
    /// </summary>
    public static void WithIcon(Button button, string icon, int size = 22, bool above = false)
    {
        if (IconTexture(icon) is not { } texture)
        {
            return;
        }

        button.Icon = texture;
        button.TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps;
        button.IconAlignment = HorizontalAlignment.Left;
        button.VerticalIconAlignment = above ? VerticalAlignment.Top : VerticalAlignment.Center;
        if (above) button.IconAlignment = HorizontalAlignment.Center;
        button.AddThemeConstantOverride("icon_max_width", size);
        button.AddThemeConstantOverride("h_separation", SpaceM);
    }

    /// <summary>A flat panel container with a style (for wells and bands inside a framed panel).</summary>
    public static PanelContainer PanelOf(Color fill, Color? border = null, int padX = SpaceL, int padY = SpaceM)
    {
        var panel = new PanelContainer();
        // The HUD's own surfaces are framed; a card inside a panel is an inset.
        panel.AddThemeStyleboxOverride("panel", fill == Panel || fill == Drawer ? FrameStyle(padX, padY) : fill == Paper ? InsetStyle(padX, padY) : Box(fill, border, padX, padY));
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
    /// A reading in the top bar (the style guide's §11): a drawn icon, a caption and its value in two
    /// lines, coloured by severity. Clicking it goes where the matter is dealt with; the tooltip says why
    /// it matters.
    /// </summary>
    public static Button Pill()
    {
        var pill = new Button { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, TopBarHeight - 18) };
        var empty = new StyleBoxEmpty();
        pill.AddThemeStyleboxOverride("normal", empty);
        pill.AddThemeStyleboxOverride("pressed", empty);
        pill.AddThemeStyleboxOverride("focus", empty);
        pill.AddThemeStyleboxOverride("hover", Box(new Color(Gold, 0.1f), new Color(Gold, 0.35f), 0, 0));
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", SpaceS + 2);
        row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        row.OffsetLeft = SpaceS;
        row.OffsetRight = -SpaceS;
        pill.AddChild(row);
        row.AddChild(Icon("info", 22));
        var lines = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        lines.AddThemeConstantOverride("separation", -3);
        row.AddChild(lines);
        lines.AddChild(Text("", TextMeta + 1, Ink));
        var value = Text("", TextBody, Ink);
        value.AddThemeFontOverride("font", SansSemibold);
        lines.AddChild(value);
        row.Resized += () => pill.CustomMinimumSize = new Vector2(row.GetCombinedMinimumSize().X + SpaceS * 2, pill.CustomMinimumSize.Y);
        return pill;
    }

    /// <summary>Updates a pill made by <see cref="Pill"/>.</summary>
    public static void SetPill(Button pill, string icon, string text, Severity severity, string tooltip)
    {
        pill.TooltipText = tooltip;
        var (name, caption) = Glyphs.TryGetValue(icon, out var known) ? known : ("info", "");
        // "Water ~21–27 d" reads as its caption over its value; "Sails in 92 h" keeps its words.
        // A first word and a short value ("Crew 0/4", "Sails in 92 h") are caption and value; otherwise
        // the icon's own caption stands over the whole text.
        var value = text;
        var split = text.IndexOf(' ');
        if (split > 2 && text[..split].All(char.IsLetter) && text[..split] != "The" && text.Length - split - 1 <= 12)
        {
            (caption, value) = (text[..split], text[(split + 1)..]);
        }

        var row = pill.GetChild<HBoxContainer>(0);
        var picture = row.GetChild<TextureRect>(0);
        picture.Texture = IconTexture(name);
        var colour = Of(severity);
        picture.Modulate = severity == Severity.Information ? Gold : colour;
        var lines = row.GetChild<VBoxContainer>(1);
        var top = lines.GetChild<Label>(0);
        top.Text = caption;
        top.Visible = caption.Length > 0;
        top.AddThemeColorOverride("font_color", InkSoft);
        var bottom = lines.GetChild<Label>(1);
        bottom.Text = value;
        bottom.AddThemeColorOverride("font_color", severity == Severity.Information ? Ink : colour);
        pill.CustomMinimumSize = new Vector2(row.GetCombinedMinimumSize().X + SpaceS * 2, pill.CustomMinimumSize.Y);
    }

    /// <summary>Shows or hides a pill's caption, for when the bar is crowded.</summary>
    public static void CompactPill(Button pill, bool compact)
    {
        var row = pill.GetChild<HBoxContainer>(0);
        var caption = row.GetChild<VBoxContainer>(1).GetChild<Label>(0);
        var wanted = !compact && caption.Text.Length > 0;
        if (caption.Visible != wanted)
        {
            caption.Visible = wanted;
            pill.CustomMinimumSize = new Vector2(row.GetCombinedMinimumSize().X + SpaceS * 2, pill.CustomMinimumSize.Y);
        }
    }

    /// <summary>A section heading over a thin gold rule, for the parts of a screen.</summary>
    public static VBoxContainer SectionHeader(string text, string note = "")
    {
        var header = new VBoxContainer();
        header.AddThemeConstantOverride("separation", 2);
        var top = new HBoxContainer();
        header.AddChild(top);
        var title = Heading(text);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(title);
        if (note.Length > 0)
        {
            var aside = Text(note, TextMeta, InkSoft);
            aside.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            top.AddChild(aside);
        }

        header.AddChild(new ColorRect { Color = new Color(Gold, 0.4f), CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore });
        return header;
    }

    /// <summary>
    /// A card: an inset panel with a heading, added to <paramref name="parent"/>. Returns the column
    /// inside it to fill.
    /// </summary>
    public static VBoxContainer Card(Control parent, string title = "", string note = "")
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", InsetStyle(SpaceL, SpaceM));
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

/// <summary>The control under a parchment panel's contents that draws its ground.</summary>
public partial class ParchmentFill : Control
{
    public override void _Draw() => Ui.DrawParchment(this, new Rect2(Vector2.Zero, Size));
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
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(48, 9),
        };
        var back = Ui.Box(Ui.InsetGround, new Color(Ui.Gold, 0.22f), 0, 0);
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
        var colour = severity == Severity.Information ? new Color("#C9A86A") : Ui.Of(severity);
        _fill.BgColor = colour;
        _value.Text = text;
        _value.AddThemeColorOverride("font_color", severity is Severity.Information or Severity.Positive ? Ui.Ink : colour);
        Root.TooltipText = tooltip;
    }
}
