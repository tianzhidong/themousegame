#nullable enable annotations

using Godot;

namespace NewGameProject.Days;

public partial class Day7BackgroundScroll : Sprite2D
{
    [Export(PropertyHint.Range, "50,400,10")]
    public float ScrollStep { get; set; } = 200.0f;

    [Export(PropertyHint.Range, "0.05,0.5,0.01")]
    public float ScrollDuration { get; set; } = 0.16f;

    private Tween? _scrollTween;
    private float _minimumY;
    private float _maximumY;
    private float _targetY;
    private float _fixedX;

    public override void _Ready()
    {
        SetProcessInput(true);
        _fixedX = Position.X;
        RecalculateBounds();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseEvent || !mouseEvent.Pressed)
        {
            return;
        }

        float movement;
        if (mouseEvent.ButtonIndex == MouseButton.WheelDown)
        {
            // Move the background upward so the viewport travels down the tall image.
            movement = -ScrollStep;
        }
        else if (mouseEvent.ButtonIndex == MouseButton.WheelUp)
        {
            movement = ScrollStep;
        }
        else
        {
            return;
        }

        float nextY = Mathf.Clamp(_targetY + movement, _minimumY, _maximumY);
        if (Mathf.IsEqualApprox(nextY, _targetY))
        {
            return;
        }

        _targetY = nextY;
        AnimateToTarget();
        GetViewport().SetInputAsHandled();
    }

    private void RecalculateBounds()
    {
        float viewportHeight = GetViewportRect().Size.Y;
        float scaledTextureHeight = Texture == null
            ? 0.0f
            : Texture.GetHeight() * Mathf.Abs(Scale.Y);

        if (scaledTextureHeight <= viewportHeight)
        {
            _minimumY = viewportHeight * 0.5f;
            _maximumY = _minimumY;
        }
        else
        {
            _maximumY = scaledTextureHeight * 0.5f;
            _minimumY = viewportHeight - scaledTextureHeight * 0.5f;
        }

        _targetY = Mathf.Clamp(Position.Y, _minimumY, _maximumY);
        Position = new Vector2(_fixedX, _targetY);
    }

    private void AnimateToTarget()
    {
        if (_scrollTween != null && _scrollTween.IsValid())
        {
            _scrollTween.Kill();
        }

        _scrollTween = CreateTween();
        _scrollTween.SetTrans(Tween.TransitionType.Quad);
        _scrollTween.SetEase(Tween.EaseType.Out);
        _scrollTween.TweenProperty(
            this,
            new NodePath("position"),
            new Vector2(_fixedX, _targetY),
            ScrollDuration);
    }
}
