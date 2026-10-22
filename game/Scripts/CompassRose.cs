using Godot;

namespace Expedition.Game;

/// <summary>
/// A compass rose drawn in gold: a ring, the four long points and the four short ones, each half lit and
/// half shaded, north up. The top bar's centrepiece; at sea a red pointer shows the course steered.
/// </summary>
public partial class CompassRose : Control
{
    private float? _heading;

    /// <summary>The course steered, compass degrees, shown by a red pointer; null for none.</summary>
    public float? Heading
    {
        get => _heading;
        set
        {
            if (value != _heading)
            {
                _heading = value;
                QueueRedraw();
            }
        }
    }

    public override void _Draw()
    {
        var centre = Size / 2;
        var radius = Mathf.Min(Size.X, Size.Y) / 2 - 1;
        var gold = new Color("#D9B54A");
        var light = new Color("#F4DE94");
        var shade = new Color("#8A6A22");
        var navy = new Color("#0E2A3A");

        DrawCircle(centre, radius * 0.78f, new Color(navy, 0.9f));
        DrawArc(centre, radius * 0.78f, 0, Mathf.Tau, 48, gold, 1.5f, true);
        DrawArc(centre, radius * 0.66f, 0, Mathf.Tau, 48, new Color(gold, 0.5f), 1, true);
        // Ticks round the ring, every 22.5°.
        for (var i = 0; i < 16; i++)
        {
            var a = Mathf.DegToRad(i * 22.5f) - Mathf.Pi / 2;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            DrawLine(centre + dir * radius * 0.66f, centre + dir * radius * (i % 2 == 0 ? 0.78f : 0.72f), gold, 1, true);
        }

        void Point(float degrees, float length, float width)
        {
            var a = Mathf.DegToRad(degrees) - Mathf.Pi / 2;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var side = new Vector2(-dir.Y, dir.X);
            var tip = centre + dir * radius * length;
            var left = centre + side * radius * width;
            var right = centre - side * radius * width;
            DrawColoredPolygon([centre, tip, left], light);
            DrawColoredPolygon([centre, right, tip], shade);
            DrawPolyline([left, tip, right], new Color("#3A2A0C"), 0.8f, true);
        }

        foreach (var d in new[] { 45f, 135f, 225f, 315f })
        {
            Point(d, 0.62f, 0.12f);
        }

        foreach (var d in new[] { 90f, 180f, 270f, 0f })
        {
            Point(d, d == 0 ? 1f : 0.92f, 0.16f);
        }

        if (_heading is { } course)
        {
            var a = Mathf.DegToRad(course) - Mathf.Pi / 2;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            DrawLine(centre, centre + dir * radius * 0.86f, new Color("#E0605A"), 2.5f, true);
            DrawCircle(centre + dir * radius * 0.86f, 2.5f, new Color("#E0605A"));
        }

        DrawCircle(centre, radius * 0.08f, gold);
        DrawCircle(centre, radius * 0.04f, navy);
    }
}
