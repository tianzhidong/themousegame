#nullable enable annotations

using Godot;
using NewGameProject.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewGameProject.Days;

public partial class Day9Laboratory : Node2D
{
    private const string MusicPath = "res://assets/audio/voice/day9/day9_bgm.mp3";
    private static readonly Dictionary<string, string> SoundPaths = new()
    {
        ["land"] = "res://assets/audio/voice/day9/box_land.mp3",
        ["open"] = "res://assets/audio/voice/day9/box_open.mp3",
        ["close"] = "res://assets/audio/voice/day4/toy_place.mp3",
        ["book"] = "res://assets/audio/voice/day6/paper_rustle.mp3",
        ["bottle"] = "res://assets/audio/voice/day9/bottle_place.mp3",
        ["specimen"] = "res://assets/audio/voice/day4/leaf_rustle.mp3"
    };

    [Export] public float ItemSize { get; set; } = 72f;
    [Export] public float SnapDistance { get; set; } = 52f;
    [Export] public float TargetOpacity { get; set; } = 0.28f;
    [Export] public float MoveDuration { get; set; } = 0.2f;
    [Export] public float GroupDelay { get; set; } = 0.7f;
    [Export] public float TiltVariationDegrees { get; set; } = 3f;
    [Export] public float SizeVariation { get; set; } = 0.04f;
    [Export] public float BoxDropHeight { get; set; } = 1150f;
    [Export] public float BoxDropDuration { get; set; } = 0.55f;

    private readonly Dictionary<string, LabItem> _assets = new();
    private readonly Dictionary<string, AudioStream> _sounds = new();
    private AudioStreamPlayer _musicPlayer = null!;
    private readonly List<LabItem> _contents = [];
    private readonly Dictionary<string, Sprite2D[]> _targetGroups = new();
    private readonly RandomNumberGenerator _random = new();
    private TaskManager _tasks = null!;
    private AudioManager _audio = null!;
    private Sprite2D _closed = null!;
    private Sprite2D _open = null!;
    private Node2D _box = null!;
    private Sprite2D _boxGlow = null!;
    private Vector2 _boxHome;
    private Node2D _contentsAnchor = null!;
    private Node2D _placedItems = null!;
    private Image _closedPixels = null!;
    private Image _openPixels = null!;
    private LabItem? _dragged;
    private Vector2 _grabOffset;
    private string _groupId = string.Empty;
    private double _nextGroupCountdown;
    private bool _changingGroup;
    private bool _animating;
    private Tween? _movement;
    private Tween? _boxArrival;
    private Tween? _glowPulse;
    private int _generation;

    public override void _EnterTree()
    {
        // Set before the child taskbar loads, including when this scene is run with F6.
        GetNode<Node>("/root/GameState").Call("set_selected_day", 9);
    }

    public override void _Ready()
    {
        _random.Randomize();
        _tasks = GetNode<TaskManager>("/root/TaskManager");
        _audio = GetNode<AudioManager>("/root/AudioManager");
        _closed = GetNode<Sprite2D>("Day9Objects/SupplyBox/Closed");
        _open = GetNode<Sprite2D>("Day9Objects/SupplyBox/Open");
        _box = _closed.GetParent<Node2D>();
        _boxHome = _box.Position;
        CreateBoxGlow();
        _contentsAnchor = GetNode<Node2D>("Day9Objects/SupplyBox/ContentsAnchor");
        _closedPixels = _closed.Texture.GetImage();
        _openPixels = _open.Texture.GetImage();
        foreach (Node2D group in GetNode<Node2D>("Day9Targets").GetChildren())
        {
            string taskId = group.GetMeta("task_id").AsString();
            _targetGroups.Add(taskId,
            [
                group.GetNode<Sprite2D>("Target1"),
                group.GetNode<Sprite2D>("Target2"),
                group.GetNode<Sprite2D>("Target3")
            ]);
            group.Show();
            foreach (Sprite2D target in _targetGroups[taskId]) target.Hide();
        }
        _placedItems = new Node2D { Name = "Day9PlacedItems", ZIndex = 1 };
        AddChild(_placedItems);
        foreach (string category in new[] { "Books", "Bottles", "Specimens" })
        {
            foreach (Sprite2D sprite in GetNode<Node2D>($"Day9Objects/{category}").GetChildren())
            {
                string id = sprite.GetMeta("asset_id").AsString();
                _assets.Add(id, new LabItem(id, category, sprite, sprite.Texture.GetImage()));
                sprite.Hide();
            }
        }
        _tasks.OnTaskActivated += HandleGroupActivated;
        _tasks.OnDayReset += HandleReset;
        if (!_tasks.LoadDay(9) || _tasks.Tasks.Count != 8
            || _tasks.Tasks.Any(group => !_targetGroups.ContainsKey(group.Definition.Id)))
        {
            GD.PushError("Day9Laboratory: expected 8 task groups with three scene-authored targets each.");
            SetProcess(false);
            SetProcessInput(false);
            SetProcessUnhandledInput(false);
            return;
        }
        foreach ((string action, string path) in SoundPaths)
        {
            if (!ResourceLoader.Exists(path)) continue;
            AudioStream? sound = GD.Load<AudioStream>(path);
            if (sound != null) _sounds[action] = sound;
        }
        AudioStreamMP3 music = (AudioStreamMP3)GD.Load<AudioStreamMP3>(MusicPath).Duplicate();
        music.Loop = true;
        _musicPlayer = new AudioStreamPlayer { Name = "Day9MusicPlayer", Bus = "Music", Stream = music };
        AddChild(_musicPlayer);
        _audio.SetBackgroundMusicPaused(true);
        _musicPlayer.Play();
        ShowGroup();
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_musicPlayer)) _musicPlayer.Stop();
        if (IsInstanceValid(_audio)) _audio.SetBackgroundMusicPaused(false);
        _movement?.Kill();
        _boxArrival?.Kill();
        _glowPulse?.Kill();
        if (IsInstanceValid(_tasks))
        {
            _tasks.OnTaskActivated -= HandleGroupActivated;
            _tasks.OnDayReset -= HandleReset;
        }
    }

    public override void _Process(double delta)
    {
        if (HasBlockingOverlay() || !GetWindow().HasFocus())
        {
            CancelDrag();
            return;
        }
        if (_dragged != null && !Input.IsMouseButtonPressed(MouseButton.Left)) CancelDrag();
        if (!_changingGroup || _animating) return;
        _nextGroupCountdown -= delta;
        if (_nextGroupCountdown <= 0) ShowGroup();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_animating || _changingGroup || HasBlockingOverlay()
            || @event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } button)
            return;
        TaskRuntimeState? group = _tasks.GetActiveTask();
        if (group == null || group.Definition.Id != _groupId) return;
        Vector2 mouse = GetGlobalMousePosition();
        if (group.Progress == 0 && button.DoubleClick && ContainsOpaquePoint(_closed, _closedPixels, mouse))
        {
            if (Submit("double_click", "supply_box", "box"))
            {
                StopBoxGlow();
                _closed.Hide();
                _open.Show();
                foreach (LabItem item in _contents) item.Sprite.Show();
                RefreshTargets();
                PlayLabSound("open");
            }
            GetViewport().SetInputAsHandled();
        }
        else if (group.Progress is >= 1 and <= 3 && !button.DoubleClick)
        {
            LabItem? item = _contents.LastOrDefault(candidate => !candidate.Placed
                && ContainsOpaquePoint(candidate.Sprite, candidate.Pixels, mouse));
            if (item == null) return;
            _dragged = item;
            _grabOffset = item.Sprite.GlobalPosition - mouse;
            item.Sprite.ZIndex = 20;
            GetViewport().SetInputAsHandled();
        }
        else if (group.Progress == 4 && !button.DoubleClick
            && ContainsOpaquePoint(_open, _openPixels, mouse)
            && PixelPoint(_open, mouse).Y < _openPixels.GetHeight() * 0.55f)
        {
            if (Submit("click", "supply_box", "box"))
            {
                _open.Hide();
                _closed.Show();
                PlayLabSound("close");
            }
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_dragged == null) return;
        if (HasBlockingOverlay()) { CancelDrag(); return; }
        if (@event is InputEventMouseMotion)
        {
            _dragged.Sprite.GlobalPosition = GetGlobalMousePosition() + _grabOffset;
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            LabItem item = _dragged;
            _dragged = null;
            Vector2 release = GetGlobalMousePosition();
            item.Sprite.GlobalPosition = release + _grabOffset;
            bool correct = IsCorrectDestination(item);
            MoveItem(item, correct ? item.Target.GlobalPosition : item.Home, correct);
            GetViewport().SetInputAsHandled();
        }
    }

    private void ShowGroup()
    {
        _generation++;
        _movement?.Kill();
        _boxArrival?.Kill();
        StopBoxGlow();
        _box.Position = _boxHome;
        _animating = false;
        _changingGroup = false;
        _dragged = null;
        foreach (LabItem item in _assets.Values) item.Sprite.Hide();
        foreach (Sprite2D[] targets in _targetGroups.Values)
            foreach (Sprite2D target in targets) target.Hide();
        _contents.Clear();
        _open.Hide();
        _closed.Show();
        TaskRuntimeState? group = _tasks.GetActiveTask();
        if (group == null) return;
        _groupId = group.Definition.Id;
        if (group.Definition.Steps.Count != 5)
        {
            GD.PushError($"Day9Laboratory: group '{_groupId}' needs five ordered actions.");
            return;
        }
        for (int index = 0; index < 3; index++)
        {
            TaskDefinition action = group.Definition.Steps[index + 1];
            List<string> available = action.TargetIds.Where(_assets.ContainsKey).ToList();
            if (available.Count == 0)
            {
                GD.PushError($"Day9Laboratory: no sprite matches step {index + 2} of '{_groupId}'.");
                return;
            }
            LabItem item = _assets[available[_random.RandiRange(0, available.Count - 1)]];
            item.Target = _targetGroups[_groupId][index];
            float previousSize = Math.Max(item.Target.Texture.GetWidth(), item.Target.Texture.GetHeight());
            item.Target.Texture = item.Sprite.Texture;
            // The random final group can choose assets with different source resolutions.
            // Keep the visible size authored in the editor when swapping its texture.
            item.Target.Scale *= previousSize / Math.Max(item.Target.Texture.GetWidth(), item.Target.Texture.GetHeight());
            float size = ItemSize * _random.RandfRange(1f - SizeVariation, 1f + SizeVariation);
            item.Sprite.Scale = Vector2.One * size / Math.Max(item.Sprite.Texture.GetWidth(), item.Sprite.Texture.GetHeight());
            item.Sprite.RotationDegrees = _random.RandfRange(-TiltVariationDegrees, TiltVariationDegrees);
            float brightness = _random.RandfRange(0.97f, 1f);
            item.Sprite.Modulate = new Color(brightness, brightness, brightness);
            item.Sprite.ZIndex = 2;
            Vector2 bottom = _contentsAnchor.GlobalPosition + new Vector2((index - 1) * 78f, _random.RandfRange(-2f, 2f));
            item.Sprite.GlobalPosition = bottom;
            item.Sprite.GlobalPosition += bottom - item.Sprite.ToGlobal(new Vector2(0, item.Sprite.Texture.GetHeight() * 0.5f));
            item.Home = item.Sprite.GlobalPosition;
            item.HomeScale = item.Sprite.GlobalScale;
            item.HomeRotation = item.Sprite.GlobalRotation;
            item.Placed = false;
            item.Target.Modulate = new Color(1, 1, 1, TargetOpacity);
            _contents.Add(item);
        }
        DropNewBox();
    }

    private void CreateBoxGlow()
    {
        Gradient gradient = new()
        {
            Offsets = new float[] { 0f, 0.45f, 1f },
            Colors = new Color[]
            {
                new(1f, 0.85f, 0.36f, 0.65f),
                new(1f, 0.85f, 0.36f, 0.35f),
                new(1f, 0.85f, 0.36f, 0f)
            }
        };
        GradientTexture2D texture = new()
        {
            Width = 256, Height = 256, Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f)
        };
        _boxGlow = new Sprite2D
        {
            Name = "BoxGlow", Texture = texture, Position = _closed.Position,
            Scale = _closed.Texture.GetSize() * _closed.Scale * new Vector2(1.55f, 1.32f) / 256f,
            ZIndex = -1, Visible = false
        };
        _box.AddChild(_boxGlow);
    }

    private void DropNewBox()
    {
        _animating = true;
        int generation = _generation;
        _box.Position = _boxHome - new Vector2(0, Math.Max(BoxDropHeight, 0f));
        _boxArrival = CreateTween();
        _boxArrival.TweenProperty(_box, "position", _boxHome, Math.Max(BoxDropDuration, 0.01f))
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        _boxArrival.TweenCallback(Callable.From(() =>
        {
            if (generation == _generation) PlayLabSound("land");
        }));
        _boxArrival.TweenProperty(_box, "position", _boxHome - new Vector2(0, 16f), 0.1f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _boxArrival.TweenProperty(_box, "position", _boxHome, 0.12f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        _boxArrival.TweenCallback(Callable.From(() =>
        {
            if (generation != _generation) return;
            _animating = false;
            _boxGlow.Show();
            _boxGlow.Modulate = Colors.White;
            _glowPulse = CreateTween().SetLoops().SetTrans(Tween.TransitionType.Sine);
            _glowPulse.SetParallel();
            _glowPulse.TweenProperty(_boxGlow, "modulate:a", 0.4f, 0.8f);
            _glowPulse.TweenProperty(_closed, "self_modulate", new Color(1.1f, 1.07f, 1f), 0.8f);
            _glowPulse.Chain().TweenProperty(_boxGlow, "modulate:a", 1f, 0.8f);
            _glowPulse.Parallel().TweenProperty(_closed, "self_modulate", Colors.White, 0.8f);
        }));
    }

    private void StopBoxGlow()
    {
        _glowPulse?.Kill();
        _boxGlow.Hide();
        _closed.SelfModulate = Colors.White;
    }

    private bool IsCorrectDestination(LabItem item) =>
        item.Sprite.GlobalPosition.DistanceTo(item.Target.GlobalPosition) <= SnapDistance;

    private void MoveItem(LabItem item, Vector2 destination, bool accept)
    {
        _animating = true;
        int generation = _generation;
        Tween tween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _movement = tween;
        tween.SetParallel();
        tween.TweenProperty(item.Sprite, "global_position", destination, Math.Max(MoveDuration, 0.01f));
        tween.TweenProperty(item.Sprite, "global_scale", accept ? item.Target.GlobalScale : item.HomeScale, Math.Max(MoveDuration, 0.01f));
        tween.TweenProperty(item.Sprite, "global_rotation", accept ? item.Target.GlobalRotation : item.HomeRotation, Math.Max(MoveDuration, 0.01f));
        tween.Chain().TweenCallback(Callable.From(() =>
        {
            if (generation != _generation) return;
            _animating = false;
            item.Sprite.ZIndex = 2;
            if (accept && Submit("drag", item.Id, ItemType(item)))
            {
                item.Placed = true;
                PreservePlacedItem(item);
                PlayLabSound(ItemType(item));
                RefreshTargets();
            }
            else if (accept) MoveItem(item, item.Home, false);
        }));
    }

    private void PlayLabSound(string action)
    {
        if (_sounds.TryGetValue(action, out AudioStream? sound)) _audio.PlayEffect(sound);
        else _audio.PlayDefaultInteractionEffect();
    }

    private bool Submit(string operation, string id, string type) => _tasks.TryHandleInteraction(new()
    {
        ["operation"] = operation, ["object_id"] = id, ["object_type"] = type,
        ["object_color"] = string.Empty, ["object_tags"] = new string[] { "laboratory_item" },
        ["timestamp"] = (long)Time.GetTicksMsec()
    });

    private void PreservePlacedItem(LabItem item)
    {
        // Later groups reuse the source sprite, so retain its accepted appearance separately.
        Sprite2D placed = new()
        {
            Name = $"{item.Id}_{_placedItems.GetChildCount() + 1}",
            Texture = item.Sprite.Texture,
            Centered = item.Sprite.Centered,
            Offset = item.Sprite.Offset,
            Modulate = item.Sprite.Modulate
        };
        _placedItems.AddChild(placed);
        placed.GlobalTransform = item.Sprite.GlobalTransform;
        item.Sprite.Hide();
    }

    private void RefreshTargets()
    {
        foreach (LabItem item in _contents) item.Target.Visible = !item.Placed;
    }

    private void CancelDrag()
    {
        if (_dragged == null) return;
        _dragged.Sprite.GlobalPosition = _dragged.Home;
        _dragged.Sprite.ZIndex = 2;
        _dragged = null;
    }

    private void HandleGroupActivated(string _id)
    {
        _changingGroup = true;
        _nextGroupCountdown = GroupDelay;
    }

    private void HandleReset(int _day)
    {
        foreach (Node item in _placedItems.GetChildren())
        {
            _placedItems.RemoveChild(item);
            item.QueueFree();
        }
        ShowGroup();
    }

    private bool HasBlockingOverlay() => GetTree().Paused
        || GetChildren().OfType<CanvasLayer>().Any(layer => layer.Layer >= 50);

    private static string ItemType(LabItem item) => item.Category switch
    {
        "Books" => "book", "Bottles" => "bottle", _ => "specimen"
    };

    private static Vector2 PixelPoint(Sprite2D sprite, Vector2 point) =>
        sprite.ToLocal(point) + sprite.Texture.GetSize() * 0.5f - sprite.Offset;

    private static bool ContainsOpaquePoint(Sprite2D sprite, Image pixels, Vector2 point)
    {
        if (!sprite.IsVisibleInTree()) return false;
        Vector2 pixel = PixelPoint(sprite, point);
        int x = (int)Math.Floor(pixel.X), y = (int)Math.Floor(pixel.Y);
        return x >= 0 && y >= 0 && x < pixels.GetWidth() && y < pixels.GetHeight()
            && pixels.GetPixel(x, y).A > 0.1f;
    }

    private sealed class LabItem(string id, string category, Sprite2D sprite, Image pixels)
    {
        public string Id { get; } = id;
        public string Category { get; } = category;
        public Sprite2D Sprite { get; } = sprite;
        public Sprite2D Target { get; set; } = null!;
        public Image Pixels { get; } = pixels;
        public Vector2 Home { get; set; }
        public Vector2 HomeScale { get; set; }
        public float HomeRotation { get; set; }
        public bool Placed { get; set; }
    }
}
