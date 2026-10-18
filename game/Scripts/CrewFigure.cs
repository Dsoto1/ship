using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// Graybox figure for one character, built from their saved <see cref="Appearance"/>: height and build
/// set the body, and skin, hair, beard and clothing colours come from the appearance options. It reads
/// the character's state every frame and stores none of it. Characters without an appearance (from older
/// saves) get a plain figure.
/// </summary>
public partial class CrewFigure : Node3D
{
    private const float HeadRadius = 0.11f;
    private static readonly Color FallbackClothing = new(0.45f, 0.3f, 0.2f);
    private static readonly Color FallbackSkin = new(0.8f, 0.62f, 0.48f);

    private Node3D _model = null!;
    private readonly List<(StandardMaterial3D Material, Color Colour)> _tintable = [];
    private MeshInstance3D _ring = null!;
    private Label3D _label = null!;
    private float _height;

    /// <summary>Builds the figure for a character.</summary>
    public static CrewFigure Create(Appearance? looks, AppearanceOptions options)
    {
        var figure = new CrewFigure();
        figure._height = (looks?.HeightCm ?? 170) / 100f;
        var width = looks?.Build switch
        {
            Build.Slight => 0.19f,
            Build.Stocky => 0.27f,
            Build.Heavy => 0.31f,
            _ => 0.23f,
        };

        Color ColourOf(string? id, Color fallback) =>
            id is not null && options.Find(id)?.Colour is { } hex ? Color.FromHtml(hex) : fallback;

        figure._model = new Node3D();
        figure.AddChild(figure._model);

        var bodyHeight = figure._height - 2 * HeadRadius;
        figure.AddPart(new CapsuleMesh { Radius = width, Height = bodyHeight },
            new Vector3(0, bodyHeight / 2f, 0), ColourOf(looks?.ClothingDye, FallbackClothing));
        figure.AddPart(new CylinderMesh { TopRadius = width + 0.012f, BottomRadius = width + 0.012f, Height = 0.06f },
            new Vector3(0, bodyHeight * 0.5f, 0), ColourOf(looks?.AccentDye, FallbackClothing.Darkened(0.3f)));

        var headCentre = figure._height - HeadRadius;
        figure.AddPart(new SphereMesh { Radius = HeadRadius, Height = HeadRadius * 2 },
            new Vector3(0, headCentre, 0), ColourOf(looks?.SkinTone, FallbackSkin));

        if (looks is not null && looks.HairStyle != "style.bald")
        {
            // A cap of hair over the top and back of the head; receding hair sits further back.
            var back = looks.HairStyle == "style.receding" ? -0.04f : -0.015f;
            figure.AddPart(new SphereMesh { Radius = HeadRadius * 1.06f, Height = HeadRadius * 1.3f },
                new Vector3(0, headCentre + 0.035f, back), ColourOf(looks.HairColour, Colors.Black));
        }

        if (looks is not null && looks.FacialHair is "beard.short" or "beard.full")
        {
            var size = looks.FacialHair == "beard.full" ? 0.11f : 0.07f;
            figure.AddPart(new BoxMesh { Size = new Vector3(0.14f, size, 0.06f) },
                new Vector3(0, headCentre - HeadRadius * 0.55f, HeadRadius * 0.7f), ColourOf(looks.HairColour, Colors.Black));
        }

        figure._ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.38f, OuterRadius = 0.46f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.88f, 0.6f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
            Position = new Vector3(0, 0.03f, 0),
            Visible = false,
        };
        figure.AddChild(figure._ring);

        figure._label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            FontSize = 30,
            PixelSize = 0.007f,
            OutlineSize = 8,
            Position = new Vector3(0, figure._height + 0.35f, 0),
        };
        figure.AddChild(figure._label);
        return figure;
    }

    /// <summary>Redraws from simulation state.</summary>
    /// <param name="character">The character.</param>
    /// <param name="placement">Where they are.</param>
    /// <param name="drawTick">Current tick plus the fraction of the next one, for smooth walking.</param>
    /// <param name="selected">Whether the player has this character selected.</param>
    /// <param name="activity">Short description of what they are doing, shown above them.</param>
    /// <param name="isCaptain">Whether this is the captain the player leads as.</param>
    public void Refresh(Character character, CharacterPlacement placement, double drawTick, bool selected, string activity, bool isCaptain)
    {
        var (x, y) = placement.Movement?.PositionAt(drawTick) ?? (placement.Cell.X, placement.Cell.Y);
        Position = new Vector3((float)x, 0, (float)y);

        // Posture shows condition: lying down when incapacitated or dead, lower when resting.
        var lying = character.SurvivalState is SurvivalState.Incapacitated or SurvivalState.Dead;
        _model.RotationDegrees = lying ? new Vector3(90, 0, 0) : Vector3.Zero;
        _model.Position = lying ? new Vector3(0, 0.25f, -_height / 2f) : Vector3.Zero;
        _model.Scale = activity == "Rest" && !lying ? new Vector3(1, 0.75f, 1) : Vector3.One;

        var dead = character.SurvivalState == SurvivalState.Dead;
        foreach (var (material, colour) in _tintable)
        {
            material.AlbedoColor = dead ? colour.Darkened(0.6f).Lerp(new Color(0.3f, 0.3f, 0.32f), 0.5f) : colour;
        }

        _ring.Visible = selected;
        var name = isCaptain ? $"{character.Name} (captain)" : character.Name;
        _label.Text = string.IsNullOrEmpty(activity) ? name : $"{name}\n{activity}";
        _label.Modulate = selected ? new Color(1f, 0.95f, 0.75f) : new Color(0.92f, 0.92f, 0.92f);
    }

    private void AddPart(Mesh mesh, Vector3 position, Color colour)
    {
        var material = new StandardMaterial3D { AlbedoColor = colour, Roughness = 0.85f };
        _tintable.Add((material, colour));
        _model.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = material, Position = position });
    }
}
