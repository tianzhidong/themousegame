using Godot;

namespace NewGameProject.Days;

public partial class Day8TireTracks : Node2D
{
    private Transform2D _spriteTransform;
    private Vector2[] _wheelContacts = [];
    private float _distance;
    private float _progress;

    public void Initialize(Sprite2D sprite, float distance)
    {
        Rect2 bounds = sprite.GetRect();
        float rearX = bounds.Position.X + bounds.Size.X * (sprite.FlipH ? 0.86f : 0.14f);
        _spriteTransform = sprite.Transform;
        _distance = distance;
        // Two tire paths follow the perspective of the toy artwork, just below its wheels.
        _wheelContacts =
        [
            new(rearX, bounds.Position.Y + bounds.Size.Y * 0.71f),
            new(rearX, bounds.Position.Y + bounds.Size.Y * 0.85f)
        ];
    }

    public void SetProgress(float progress)
    {
        _progress = Mathf.Clamp(progress, 0.0f, 1.0f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float length = Mathf.Abs(_distance) * _progress;
        if (length < 0.1f)
        {
            return;
        }

        float direction = Mathf.Sign(_distance);
        foreach (Vector2 contact in _wheelContacts)
        {
            Vector2 start = _spriteTransform * contact;
            DrawLine(start, start + Vector2.Right * direction * length, new Color(0.22f, 0.18f, 0.12f, 0.18f), 5.0f, true);
            for (float offset = 0.0f; offset < length; offset += 7.0f)
            {
                float end = Mathf.Min(offset + 3.5f, length);
                DrawLine(start + Vector2.Right * direction * offset, start + Vector2.Right * direction * end,
                    new Color(0.22f, 0.18f, 0.12f, 0.48f), 4.0f, true);
            }
        }
    }
}
