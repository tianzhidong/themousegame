using Godot;

namespace NewGameProject.Days;

public partial class Day12CelebrationBits : Node2D
{
    private readonly RandomNumberGenerator _random = new();
    private readonly Vector2[] _velocities = new Vector2[20];
    private readonly Color[] _colors = new Color[20];
    private float _time;

    public override void _Ready()
    {
        _random.Randomize();
        for (int index = 0; index < _velocities.Length; index++)
        {
            _velocities[index] = new Vector2(_random.RandfRange(-180, 180), _random.RandfRange(-270, -80));
            _colors[index] = Color.FromHsv(_random.Randf(), 0.62f, 0.96f);
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        if (_time > 0.9f) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        for (int index = 0; index < _velocities.Length; index++)
        {
            Vector2 point = _velocities[index] * _time + new Vector2(0, 250 * _time * _time);
            Color color = _colors[index];
            color.A = Mathf.Clamp(1 - _time / 0.9f, 0, 1);
            DrawCircle(point, 5 + index % 3, color);
        }
    }
}

