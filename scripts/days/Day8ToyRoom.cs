#nullable enable annotations

using Godot;
using NewGameProject.Tasks;
using System.Collections.Generic;

namespace NewGameProject.Days;

public partial class Day8ToyRoom : Node2D
{
    private const string SoundRoot = "res://assets/audio/voice/day8/";
    private static readonly Dictionary<string, string> SoundFiles = new()
    {
        ["box"] = "打开盒子.mp3",
        ["car"] = "汽车.mp3",
        ["airplane"] = "飞机.mp3",
        ["robot"] = "机器人.mp3",
        ["egg"] = "蛋壳破裂.mp3",
        ["candy"] = "糖纸.mp3",
        ["rocket"] = "火箭.mp3",
        ["picture_book"] = "翻书.mp3"
    };

    [Export(PropertyHint.Range, "0.2,1.8,0.05")]
    public float ActionDuration { get; set; } = 0.8f;

    [Export(PropertyHint.Range, "10,100,1")]
    public float TravelDistance { get; set; } = 45.0f;

    [Export(PropertyHint.Range, "10,100,1")]
    public float FlightHeight { get; set; } = 40.0f;

    [Export(PropertyHint.Range, "150,1000,10")]
    public float RocketHeight { get; set; } = 650.0f;

    [Export(PropertyHint.Range, "0,0.1,0.01")]
    public float SizeVariation { get; set; } = 0.04f;

    [Export(PropertyHint.Range, "0,5,0.5")]
    public float TiltVariationDegrees { get; set; } = 2.0f;

    [Export(PropertyHint.Range, "-30,0,1")]
    public float ToySoundVolumeDb { get; set; } = 0.0f;

    private readonly RandomNumberGenerator _random = new();
    private readonly Dictionary<string, ToyState> _toys = new();
    private readonly Dictionary<string, AudioStream> _soundStreams = new();
    private readonly HashSet<AudioStreamPlayer> _playingSounds = [];
    private TaskManager _taskManager = null!;

    public override void _EnterTree()
    {
        // Select the correct task configuration before child nodes initialize, including F6.
        GetNode<Node>("/root/GameState").Call("set_selected_day", 8);
    }

    public override void _Ready()
    {
        _random.Randomize();
        _taskManager = GetNode<TaskManager>("/root/TaskManager");
        foreach ((string objectType, string filename) in SoundFiles)
        {
            AudioStream? stream = GD.Load<AudioStream>(SoundRoot + filename);
            if (stream == null)
            {
                GD.PushError($"Day8ToyRoom: failed to load sound '{SoundRoot + filename}'.");
                continue;
            }
            _soundStreams.Add(objectType, stream);
        }

        foreach (Node group in GetNode<Node2D>("Toys").GetChildren())
        {
            foreach (Node child in group.GetChildren())
            {
                InteractiveObject item = (InteractiveObject)child;
                Sprite2D sprite = item.GetNode<Sprite2D>("Sprite2D");
                item.Scale *= _random.RandfRange(1.0f - SizeVariation, 1.0f + SizeVariation);
                item.RotationDegrees += _random.RandfRange(-TiltVariationDegrees, TiltVariationDegrees);
                item.Modulate = Color.FromHsv(_random.Randf(), 0.02f, _random.RandfRange(0.97f, 1.0f));
                Texture2D? openedTexture = item.HasMeta("opened_texture")
                    ? item.GetMeta("opened_texture").AsGodotObject() as Texture2D
                    : null;
                Texture2D? litTexture = item.HasMeta("lit_texture")
                    ? item.GetMeta("lit_texture").AsGodotObject() as Texture2D
                    : null;
                _toys.Add(item.ObjectId, new ToyState(item, sprite, openedTexture, litTexture));
                item.InteractionAccepted += HandleInteractionAccepted;
            }
        }

        _taskManager.OnDayReset += HandleDayReset;
        // Re-entering this scene starts a fresh round with the authored toy states.
        _taskManager.LoadDay(8);
    }

    public override void _ExitTree()
    {
        StopToySounds();
        if (IsInstanceValid(_taskManager))
        {
            _taskManager.OnDayReset -= HandleDayReset;
        }

        foreach (ToyState toy in _toys.Values)
        {
            if (IsInstanceValid(toy.Item))
            {
                toy.Item.InteractionAccepted -= HandleInteractionAccepted;
            }
            StopAnimation(toy);
        }
    }

    private void HandleInteractionAccepted(string operation, string objectId)
    {
        if (operation != "double_click" || !_toys.TryGetValue(objectId, out ToyState? toy) || toy.Completed)
        {
            return;
        }

        // InteractiveObject has already validated the active group and counted this unique object.
        toy.Completed = true;
        Tween tween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);
        toy.Animation = tween;
        float duration = Mathf.Max(ActionDuration, 0.2f);
        PlayToySound(toy.Item.ObjectType);

        if (toy.OpenedTexture != null)
        {
            OpenToy(toy, tween, duration);
            return;
        }

        switch (toy.Item.ObjectType)
        {
            case "car":
                DriveCar(toy, tween, duration);
                break;
            case "airplane":
                tween.TweenProperty(toy.Sprite, "position", toy.SpritePosition + new Vector2(TravelDistance, FlightHeight), duration * 0.5f);
                tween.TweenProperty(toy.Sprite, "position", toy.SpritePosition, duration * 0.5f);
                break;
            case "robot":
                // The lit atlas uses the original crop, so the face and body stay aligned.
                toy.Sprite.Texture = toy.LitTexture ?? toy.Texture;
                // A short wobble and hop visibly starts the toy without needing a frame animation.
                tween.TweenProperty(toy.Sprite, "rotation", toy.SpriteRotation - 0.12f, duration * 0.2f);
                tween.TweenProperty(toy.Sprite, "rotation", toy.SpriteRotation + 0.12f, duration * 0.2f);
                tween.TweenProperty(toy.Sprite, "rotation", toy.SpriteRotation, duration * 0.2f);
                tween.TweenProperty(toy.Sprite, "position", toy.SpritePosition + Vector2.Up * 12.0f, duration * 0.2f);
                tween.TweenProperty(toy.Sprite, "position", toy.SpritePosition, duration * 0.2f);
                break;
            case "rocket":
                tween.TweenProperty(toy.Sprite, "position", GetRocketExitPosition(toy), duration)
                    .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
                tween.TweenCallback(Callable.From(toy.Sprite.Hide));
                break;
        }
    }

    private void DriveCar(ToyState toy, Tween tween, float duration)
    {
        float distance = Mathf.Max(TravelDistance, 0.0f) * (toy.Sprite.FlipH ? -1.0f : 1.0f);
        Day8TireTracks tracks = new()
        {
            Name = toy.Item.Name + "TireTracks",
            Transform = toy.Item.Transform,
            ZIndex = toy.Item.ZIndex - 1
        };
        toy.Item.GetParent().AddChild(tracks);
        tracks.Initialize(toy.Sprite, distance);
        toy.TireTracks = tracks;

        // Move the whole car so its authored hit area follows it; the tracks stay on the floor.
        Vector2 target = toy.ItemPosition + toy.Item.Transform.X * distance;
        tween.TweenProperty(toy.Item, "position", target, duration);
        tween.Parallel().TweenMethod(Callable.From<float>(tracks.SetProgress), 0.0f, 1.0f, duration);
    }

    private void PlayToySound(string objectType)
    {
        if (!_soundStreams.TryGetValue(objectType, out AudioStream? stream))
        {
            return;
        }

        // Each action owns its playback so another toy or a finished animation cannot cut it off.
        AudioStreamPlayer player = new()
        {
            Stream = stream,
            VolumeDb = ToySoundVolumeDb
        };
        AddChild(player);
        _playingSounds.Add(player);
        player.Finished += () =>
        {
            _playingSounds.Remove(player);
            player.QueueFree();
        };
        player.Play();
    }

    private void StopToySounds()
    {
        foreach (AudioStreamPlayer player in _playingSounds)
        {
            if (IsInstanceValid(player))
            {
                player.Stop();
                player.QueueFree();
            }
        }
        _playingSounds.Clear();
    }

    private Vector2 GetRocketExitPosition(ToyState toy)
    {
        // Measure all four corners in viewport coordinates, including authored scale and tilt.
        Rect2 bounds = toy.Sprite.GetRect();
        Transform2D spriteToViewport = toy.Sprite.GetGlobalTransformWithCanvas();
        float bottom = float.NegativeInfinity;
        Vector2[] corners =
        [
            bounds.Position,
            new(bounds.End.X, bounds.Position.Y),
            bounds.End,
            new(bounds.Position.X, bounds.End.Y)
        ];
        foreach (Vector2 corner in corners)
        {
            bottom = Mathf.Max(bottom, (spriteToViewport * corner).Y);
        }

        float distance = Mathf.Max(RocketHeight, bottom - toy.Sprite.GetViewportRect().Position.Y + 16.0f);
        Transform2D parentToViewport = toy.Item.GetGlobalTransformWithCanvas();
        Vector2 target = parentToViewport * toy.Sprite.Position + Vector2.Up * distance;
        // Convert back to the position property's local coordinates so scaled toys still exit fully.
        return parentToViewport.AffineInverse() * target;
    }

    private static void OpenToy(ToyState toy, Tween tween, float duration)
    {
        Texture2D opened = toy.OpenedTexture!;
        // Match the width and keep the bottom on the same shelf/table when switching atlases.
        float widthRatio = toy.Texture.GetWidth() / (float)opened.GetWidth();
        Vector2 openedScale = toy.SpriteScale * widthRatio;
        float bottom = toy.SpritePosition.Y + toy.Texture.GetHeight() * toy.SpriteScale.Y * 0.5f;
        toy.Sprite.Texture = opened;
        toy.Sprite.Position = new Vector2(toy.SpritePosition.X, bottom - opened.GetHeight() * openedScale.Y * 0.5f);
        toy.Sprite.Scale = openedScale * 0.9f;
        tween.TweenProperty(toy.Sprite, "scale", openedScale, duration * 0.4f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private void HandleDayReset(int dayId)
    {
        if (dayId != 8)
        {
            return;
        }

        StopToySounds();
        foreach (ToyState toy in _toys.Values)
        {
            StopAnimation(toy);
            if (IsInstanceValid(toy.TireTracks))
            {
                toy.TireTracks!.QueueFree();
            }
            toy.TireTracks = null;
            toy.Completed = false;
            toy.Item.Position = toy.ItemPosition;
            toy.Sprite.Texture = toy.Texture;
            toy.Sprite.Position = toy.SpritePosition;
            toy.Sprite.Scale = toy.SpriteScale;
            toy.Sprite.Rotation = toy.SpriteRotation;
            toy.Sprite.Modulate = toy.SpriteColor;
            toy.Sprite.Visible = toy.SpriteVisible;
        }
    }

    private static void StopAnimation(ToyState toy)
    {
        if (IsInstanceValid(toy.Animation))
        {
            toy.Animation!.Kill();
        }
        toy.Animation = null;
    }

    private sealed class ToyState(InteractiveObject item, Sprite2D sprite, Texture2D? openedTexture, Texture2D? litTexture)
    {
        public InteractiveObject Item { get; } = item;
        public Sprite2D Sprite { get; } = sprite;
        public Vector2 ItemPosition { get; } = item.Position;
        public Texture2D Texture { get; } = sprite.Texture;
        public Texture2D? OpenedTexture { get; } = openedTexture;
        public Texture2D? LitTexture { get; } = litTexture;
        public Vector2 SpritePosition { get; } = sprite.Position;
        public Vector2 SpriteScale { get; } = sprite.Scale;
        public float SpriteRotation { get; } = sprite.Rotation;
        public Color SpriteColor { get; } = sprite.Modulate;
        public bool SpriteVisible { get; } = sprite.Visible;
        public bool Completed { get; set; }
        public Tween? Animation { get; set; }
        public Day8TireTracks? TireTracks { get; set; }
    }
}
