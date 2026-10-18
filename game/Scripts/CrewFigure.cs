using Expedition.Simulation.Characters;
using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// Graybox figure for one character. It reads the character's state every frame and never stores any
/// of it. Height, build and clothing colour vary per person so individuals can be told apart. These
/// are placeholders derived from the ID until Ticket 4c adds saved appearance data.
/// </summary>
public partial class CrewFigure : Node3D
{
    // Muted, period-plausible dye colours; deliberately not tied to roles (ART_BIBLE.md 4.6, 11).
    private static readonly Color[] Clothing =
    [
        new(0.55f, 0.24f, 0.2f), new(0.27f, 0.35f, 0.5f), new(0.72f, 0.66f, 0.54f),
        new(0.45f, 0.3f, 0.2f), new(0.62f, 0.5f, 0.25f), new(0.25f, 0.36f, 0.27f),
    ];

    private static readonly Color DeadColour = new(0.22f, 0.22f, 0.24f);

    private MeshInstance3D _body = null!;
    private StandardMaterial3D _material = null!;
    private MeshInstance3D _ring = null!;
    private Label3D _label = null!;
    private float _height;
    private Color _colour;

    /// <summary>Creates the figure for the character with ID number <paramref name="idValue"/>.</summary>
    public static CrewFigure Create(int idValue)
    {
        var figure = new CrewFigure();
        var variation = (uint)(idValue * 2654435761u);
        figure._height = 1.6f + (variation % 23) / 100f;
        var width = 0.22f + (variation / 23 % 9) / 100f;
        figure._colour = Clothing[(idValue - 1) % Clothing.Length];

        figure._material = new StandardMaterial3D { AlbedoColor = figure._colour, Roughness = 0.85f };
        figure._body = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = width, Height = figure._height },
            MaterialOverride = figure._material,
            Position = new Vector3(0, figure._height / 2f, 0),
        };
        figure.AddChild(figure._body);

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
    public void Refresh(Character character, CharacterPlacement placement, double drawTick, bool selected, string activity)
    {
        var (x, y) = placement.Movement?.PositionAt(drawTick) ?? (placement.Cell.X, placement.Cell.Y);
        Position = new Vector3((float)x, 0, (float)y);

        // Posture shows condition: lying down when incapacitated or dead, lower when resting.
        var lying = character.SurvivalState is SurvivalState.Incapacitated or SurvivalState.Dead;
        _body.RotationDegrees = lying ? new Vector3(90, 0, 0) : Vector3.Zero;
        _body.Position = lying ? new Vector3(0, 0.25f, 0) : new Vector3(0, _height / 2f, 0);
        _body.Scale = activity == "Rest" && !lying ? new Vector3(1, 0.75f, 1) : Vector3.One;
        _material.AlbedoColor = character.SurvivalState == SurvivalState.Dead ? DeadColour : _colour;

        _ring.Visible = selected;
        _label.Text = string.IsNullOrEmpty(activity) ? character.Name : $"{character.Name}\n{activity}";
        _label.Modulate = selected ? new Color(1f, 0.95f, 0.75f) : new Color(0.92f, 0.92f, 0.92f);
    }
}
