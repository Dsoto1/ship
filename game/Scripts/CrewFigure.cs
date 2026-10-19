using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>What a figure is doing, chosen by <see cref="Main"/> from simulation state.</summary>
public enum Pose
{
    Standing,
    Walking,
    Working,
    Resting,
    Lying,
}

/// <summary>
/// Figure for one character, built from their saved <see cref="Appearance"/> (ART_BIBLE.md 4): legs,
/// body, arms and head in proportion to their height and build; skin, hair, beard and clothing colours
/// from the appearance options; and a hat that marks standing (a brimmed hat for officers, a knitted
/// cap for sailors). It walks, works, sits to rest and lies down when hurt, and turns to face where
/// it is going. It reads the character's state every frame and stores none of it.
/// </summary>
public partial class CrewFigure : Node3D
{
    private static readonly Color FallbackClothing = new(0.45f, 0.3f, 0.2f);
    private static readonly Color FallbackSkin = new(0.8f, 0.62f, 0.48f);
    private static readonly Color Breeches = new(0.24f, 0.2f, 0.17f);
    private static readonly Color Shoes = new(0.12f, 0.09f, 0.07f);

    private readonly List<(StandardMaterial3D Material, Color Colour)> _tintable = [];
    private Node3D _model = null!;
    private Node3D _body = null!;
    private Node3D _leftLeg = null!;
    private Node3D _rightLeg = null!;
    private Node3D _leftArm = null!;
    private Node3D _rightArm = null!;
    private MeshInstance3D _ring = null!;
    private Label3D _label = null!;
    private float _height;
    private float _legLength;
    private float _heading;
    private Vector3 _lastPosition;
    private float _phase;

    /// <summary>Builds the figure for a character.</summary>
    /// <param name="looks">Their appearance, or null for a plain figure.</param>
    /// <param name="options">The appearance options, for colours.</param>
    /// <param name="officer">True for officers and gentlemen, who wear a brimmed hat.</param>
    public static CrewFigure Create(Appearance? looks, AppearanceOptions options, bool officer)
    {
        var figure = new CrewFigure();
        var h = (looks?.HeightCm ?? 170) / 100f;
        figure._height = h;
        var bulk = looks?.Build switch
        {
            Build.Slight => 0.85f,
            Build.Stocky => 1.15f,
            Build.Heavy => 1.3f,
            _ => 1f,
        };

        Color ColourOf(string? id, Color fallback) =>
            id is not null && options.Find(id)?.Colour is { } hex ? Color.FromHtml(hex) : fallback;

        var clothing = ColourOf(looks?.ClothingDye, FallbackClothing);
        var accent = ColourOf(looks?.AccentDye, FallbackClothing.Darkened(0.3f));
        var skin = ColourOf(looks?.SkinTone, FallbackSkin);
        var hair = ColourOf(looks?.HairColour, new Color(0.15f, 0.1f, 0.07f));

        figure._legLength = h * 0.47f;
        var torso = h * 0.3f;
        var headRadius = h * 0.066f;
        var shoulder = 0.17f * bulk;

        figure._model = new Node3D();
        figure.AddChild(figure._model);
        figure._body = new Node3D { Position = new Vector3(0, figure._legLength, 0) };
        figure._model.AddChild(figure._body);

        // Legs hang from the hips: breeches, hose and shoes.
        figure._leftLeg = figure.Limb(figure._body, new Vector3(-0.07f * bulk, 0, 0));
        figure._rightLeg = figure.Limb(figure._body, new Vector3(0.07f * bulk, 0, 0));
        foreach (var leg in new[] { figure._leftLeg, figure._rightLeg })
        {
            figure.AddPart(leg, new CylinderMesh { TopRadius = 0.075f * bulk, BottomRadius = 0.05f, Height = figure._legLength - 0.06f }, new Vector3(0, -(figure._legLength - 0.06f) / 2f, 0), Breeches);
            figure.AddPart(leg, new BoxMesh { Size = new Vector3(0.09f, 0.06f, 0.2f) }, new Vector3(0, -figure._legLength + 0.03f, 0.04f), Shoes);
        }

        // Body: a doublet or shirt in the clothing dye, a belt or sash in the accent colour.
        figure.AddPart(figure._body, new CylinderMesh { TopRadius = shoulder, BottomRadius = 0.15f * bulk, Height = torso }, new Vector3(0, torso / 2f, 0), clothing);
        figure.AddPart(figure._body, new CylinderMesh { TopRadius = 0.152f * bulk, BottomRadius = 0.155f * bulk, Height = 0.05f }, new Vector3(0, 0.04f, 0), accent);
        figure.AddPart(figure._body, new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.06f, Height = 0.08f }, new Vector3(0, torso + 0.03f, 0), skin);

        // Arms hang from the shoulders, sleeves in the clothing colour, bare hands.
        var armLength = h * 0.34f;
        figure._leftArm = figure.Limb(figure._body, new Vector3(-shoulder - 0.03f, torso - 0.03f, 0));
        figure._rightArm = figure.Limb(figure._body, new Vector3(shoulder + 0.03f, torso - 0.03f, 0));
        foreach (var arm in new[] { figure._leftArm, figure._rightArm })
        {
            figure.AddPart(arm, new CylinderMesh { TopRadius = 0.05f * bulk, BottomRadius = 0.04f, Height = armLength - 0.05f }, new Vector3(0, -(armLength - 0.05f) / 2f, 0), clothing.Darkened(0.08f));
            figure.AddPart(arm, new SphereMesh { Radius = 0.038f, Height = 0.076f }, new Vector3(0, -armLength + 0.02f, 0), skin);
        }

        // Head, hair, beard.
        var headCentre = torso + 0.07f + headRadius;
        figure.AddPart(figure._body, new SphereMesh { Radius = headRadius, Height = headRadius * 2.1f }, new Vector3(0, headCentre, 0), skin);
        figure.AddPart(figure._body, new BoxMesh { Size = new Vector3(0.03f, 0.04f, 0.03f) }, new Vector3(0, headCentre - 0.005f, headRadius * 0.95f), skin.Darkened(0.08f));
        if (looks is not null && looks.HairStyle != "style.bald")
        {
            var back = looks.HairStyle == "style.receding" ? -0.035f : -0.012f;
            figure.AddPart(figure._body, new SphereMesh { Radius = headRadius * 1.07f, Height = headRadius * 1.5f }, new Vector3(0, headCentre + 0.03f, back), hair);
        }

        if (looks is not null && looks.FacialHair is "beard.short" or "beard.full")
        {
            var size = looks.FacialHair == "beard.full" ? 0.085f : 0.05f;
            figure.AddPart(figure._body, new BoxMesh { Size = new Vector3(headRadius * 1.5f, size, 0.07f) }, new Vector3(0, headCentre - headRadius * 0.6f, headRadius * 0.6f), hair);
        }

        // Hat: standing shown by headgear rather than colour coding (ART_BIBLE.md 11).
        if (officer)
        {
            var black = new Color(0.08f, 0.07f, 0.07f);
            figure.AddPart(figure._body, new CylinderMesh { TopRadius = headRadius * 1.9f, BottomRadius = headRadius * 1.9f, Height = 0.02f }, new Vector3(0, headCentre + headRadius * 0.7f, 0), black);
            figure.AddPart(figure._body, new CylinderMesh { TopRadius = headRadius * 0.95f, BottomRadius = headRadius * 1.05f, Height = 0.09f }, new Vector3(0, headCentre + headRadius * 0.7f + 0.05f, 0), black);
        }
        else
        {
            figure.AddPart(figure._body, new SphereMesh { Radius = headRadius * 1.1f, Height = headRadius * 1.3f }, new Vector3(0, headCentre + headRadius * 0.55f, -0.01f), accent.Lerp(new Color(0.55f, 0.15f, 0.12f), 0.5f));
        }

        figure._ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.36f, OuterRadius = 0.43f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.98f, 0.86f, 0.45f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
            Position = new Vector3(0, 0.03f, 0),
            Visible = false,
        };
        figure.AddChild(figure._ring);

        figure._label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            FontSize = 32,
            PixelSize = 0.0085f,
            OutlineSize = 9,
            Position = new Vector3(0, h + 0.3f, 0),
        };
        figure.AddChild(figure._label);
        return figure;
    }

    /// <summary>Redraws from simulation state.</summary>
    /// <param name="character">The character.</param>
    /// <param name="placement">Where they are.</param>
    /// <param name="drawTick">Current tick plus the fraction of the next one, for smooth walking.</param>
    /// <param name="selected">Whether the player has this character selected.</param>
    /// <param name="activity">What they are doing, shown under the name when selected.</param>
    /// <param name="isCaptain">Whether this is the captain the player leads as.</param>
    /// <param name="pose">How to stand.</param>
    /// <param name="delta">Seconds since the last frame, for animation.</param>
    /// <param name="animationRate">How fast to animate: faster at higher game speeds, zero when paused.</param>
    public void Refresh(Character character, CharacterPlacement placement, double drawTick, bool selected, string activity,
        bool isCaptain, Pose pose, float delta, float animationRate)
    {
        var (x, y) = placement.Movement?.PositionAt(drawTick) ?? (placement.Cell.X, placement.Cell.Y);
        Position = new Vector3((float)x, 0, (float)y);

        // Turn towards the direction of travel.
        var moved = Position - _lastPosition;
        _lastPosition = Position;
        if (moved.LengthSquared() > 0.0001f && moved.Length() < 2f)
        {
            var target = Mathf.Atan2(moved.X, moved.Z);
            _heading = Mathf.LerpAngle(_heading, target, Mathf.Clamp(delta * 10f, 0, 1));
        }

        _model.Rotation = new Vector3(0, _heading, 0);
        _phase += delta * animationRate;
        Animate(pose);

        var dead = character.SurvivalState == SurvivalState.Dead;
        foreach (var (material, colour) in _tintable)
        {
            material.AlbedoColor = dead ? colour.Darkened(0.6f).Lerp(new Color(0.3f, 0.3f, 0.32f), 0.5f) : colour;
        }

        _ring.Visible = selected;
        var name = isCaptain ? $"{character.Name} (captain)" : character.Name;
        _label.Text = selected && !string.IsNullOrEmpty(activity) ? $"{name}\n{activity}" : name;
        _label.Modulate = selected ? new Color(1f, 0.93f, 0.7f) : new Color(0.95f, 0.95f, 0.95f, 0.8f);
        _label.Position = new Vector3(0, pose is Pose.Resting or Pose.Lying ? 1.2f : _height + 0.3f, 0);
    }

    private void Animate(Pose pose)
    {
        var swing = Mathf.Sin(_phase * 7f);
        _model.Position = Vector3.Zero;
        _model.RotationDegrees = new Vector3(0, _model.RotationDegrees.Y, 0);
        _body.Position = new Vector3(0, _legLength, 0);
        _body.RotationDegrees = Vector3.Zero;
        switch (pose)
        {
            case Pose.Walking:
                _leftLeg.RotationDegrees = new Vector3(swing * 28f, 0, 0);
                _rightLeg.RotationDegrees = new Vector3(-swing * 28f, 0, 0);
                _leftArm.RotationDegrees = new Vector3(-swing * 22f, 0, 4f);
                _rightArm.RotationDegrees = new Vector3(swing * 22f, 0, -4f);
                _body.Position += new Vector3(0, Mathf.Abs(swing) * 0.025f, 0);
                break;
            case Pose.Working:
                // Arms forward and working in rhythm, leaning into it.
                var stroke = Mathf.Sin(_phase * 4.5f);
                _leftLeg.RotationDegrees = new Vector3(-8f, 0, 0);
                _rightLeg.RotationDegrees = new Vector3(10f, 0, 0);
                _leftArm.RotationDegrees = new Vector3(-55f - stroke * 25f, 0, 8f);
                _rightArm.RotationDegrees = new Vector3(-55f + stroke * 25f, 0, -8f);
                _body.RotationDegrees = new Vector3(10f + stroke * 4f, 0, 0);
                break;
            case Pose.Resting:
                // Sitting on the deck, legs out in front, leaning back on the hands.
                _body.Position = new Vector3(0, 0.12f, 0);
                _body.RotationDegrees = new Vector3(-12f, 0, 0);
                _leftLeg.RotationDegrees = new Vector3(-85f, 0, 4f);
                _rightLeg.RotationDegrees = new Vector3(-80f, 0, -4f);
                _leftArm.RotationDegrees = new Vector3(35f, 0, 14f);
                _rightArm.RotationDegrees = new Vector3(35f, 0, -14f);
                break;
            case Pose.Lying:
                _model.RotationDegrees = new Vector3(-90f, _model.RotationDegrees.Y, 0);
                _model.Position = new Vector3(0, 0.15f, 0);
                _leftLeg.RotationDegrees = Vector3.Zero;
                _rightLeg.RotationDegrees = Vector3.Zero;
                _leftArm.RotationDegrees = new Vector3(0, 0, 10f);
                _rightArm.RotationDegrees = new Vector3(0, 0, -10f);
                break;
            default:
                // Standing, with a little sway to keep balance on a moving deck.
                var sway = Mathf.Sin(_phase * 1.3f) * 2f;
                _leftLeg.RotationDegrees = Vector3.Zero;
                _rightLeg.RotationDegrees = Vector3.Zero;
                _leftArm.RotationDegrees = new Vector3(4f, 0, 5f);
                _rightArm.RotationDegrees = new Vector3(4f, 0, -5f);
                _body.RotationDegrees = new Vector3(0, 0, sway);
                break;
        }
    }

    private Node3D Limb(Node3D parent, Vector3 joint)
    {
        var pivot = new Node3D { Position = joint };
        parent.AddChild(pivot);
        return pivot;
    }

    private void AddPart(Node3D parent, Mesh mesh, Vector3 position, Color colour)
    {
        var material = new StandardMaterial3D { AlbedoColor = colour, Roughness = 0.85f };
        _tintable.Add((material, colour));
        parent.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = material, Position = position });
    }
}
