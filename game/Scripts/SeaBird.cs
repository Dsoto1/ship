using Godot;

namespace Expedition.Game;

/// <summary>A seabird wheeling around the ship. Purely decorative life over the water.</summary>
public partial class SeaBird : Node3D
{
    private Node3D _leftWing = null!;
    private Node3D _rightWing = null!;
    private Vector3 _centre;
    private float _radius;
    private float _speed;
    private float _phase;
    private float _height;

    /// <summary>Creates a bird circling <paramref name="centre"/>.</summary>
    public static SeaBird Create(Vector3 centre, float radius, float speed, float phase, float height)
    {
        var bird = new SeaBird { _centre = centre, _radius = radius, _speed = speed, _phase = phase, _height = height };
        var material = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.9f, 0.88f), Roughness = 0.9f };
        var dark = new StandardMaterial3D { AlbedoColor = new Color(0.25f, 0.25f, 0.27f), Roughness = 0.9f };
        bird.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.07f, Height = 0.45f }, MaterialOverride = material, RotationDegrees = new Vector3(90, 0, 0) });
        bird._leftWing = new Node3D();
        bird._rightWing = new Node3D();
        bird.AddChild(bird._leftWing);
        bird.AddChild(bird._rightWing);
        bird._leftWing.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.6f, 0.02f, 0.18f) }, MaterialOverride = dark, Position = new Vector3(-0.3f, 0, 0) });
        bird._rightWing.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.6f, 0.02f, 0.18f) }, MaterialOverride = dark, Position = new Vector3(0.3f, 0, 0) });
        return bird;
    }

    /// <summary>The centre the bird circles; it follows the ship.</summary>
    public Vector3 Centre { set => _centre = value; }

    public override void _Process(double delta)
    {
        _phase += (float)delta * _speed;
        var angle = _phase;
        var drift = Mathf.Sin(_phase * 0.37f) * _radius * 0.3f;
        Position = _centre + new Vector3(Mathf.Cos(angle) * (_radius + drift), _height + Mathf.Sin(_phase * 0.8f) * 1.2f, Mathf.Sin(angle) * (_radius + drift) * 1.4f);
        var heading = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle) * 1.4f);
        LookAt(Position + heading * Mathf.Sign(_speed), Vector3.Up);
        RotateObjectLocal(Vector3.Forward, Mathf.Sign(_speed) * 0.35f);

        // Flap in bursts, then glide.
        var flap = Mathf.Sin(_phase * 3.1f) > 0.2f ? Mathf.Sin(_phase * 28f) * 0.55f : 0.08f;
        _leftWing.Rotation = new Vector3(0, 0, -flap);
        _rightWing.Rotation = new Vector3(0, 0, flap);
    }
}
