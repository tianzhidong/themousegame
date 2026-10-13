using Godot;

namespace NewGameProject.Days;

// Small outlined bubbles rise from the scene-authored mouth marker.
public partial class Day12AquariumBubbles : Node2D
{
    private readonly RandomNumberGenerator _random = new();
    private readonly Vector2[] _origins = new Vector2[7];
    private readonly float[] _radii = new float[7];
    private float _time;

    public override void _Ready()
    {
        _random.Randomize();
        for (int index = 0; index < _radii.Length; index++)
        {
            _origins[index] = new Vector2(_random.RandfRange(-12, 12), index * 7);
            _radii[index] = _random.RandfRange(4, 9);
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        if (_time > 1.35f) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float alpha = Mathf.Clamp(1 - _time / 1.35f, 0, 1);
        for (int index = 0; index < _radii.Length; index++)
        {
            Vector2 point = _origins[index] + new Vector2(Mathf.Sin(_time * 3 + index) * 6, -_time * (65 + index * 4));
            float radius = _radii[index] * Mathf.Min(1, _time * 5 + 0.2f);
            DrawCircle(point, radius, new Color(0.7f, 0.94f, 1, alpha * 0.16f));
            DrawArc(point, radius, 0, Mathf.Tau, 24, new Color(0.87f, 0.98f, 1, alpha * 0.9f), 1.6f, true);
            DrawCircle(point + new Vector2(-radius * 0.3f, -radius * 0.3f), radius * 0.17f,
                new Color(1, 1, 1, alpha));
        }
    }
}
