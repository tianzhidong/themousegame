#nullable enable annotations

using Godot;
using NewGameProject.Tasks;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NewGameProject.Days;

public partial class Day6Party : Node2D
{
    private const string ConfigPath = "res://data/days/day_06.json";
    private const string MusicPath = "res://assets/audio/voice/day6/day6_bgm.mp3";
    private static readonly Dictionary<string, string> PlacementSoundPaths = new()
    {
        ["banner"] = "res://assets/audio/voice/day4/cloth_rustle.mp3",
        ["flower"] = "res://assets/audio/voice/day4/leaf_rustle.mp3",
        ["tableware"] = "res://assets/audio/voice/day6/paper_rustle.mp3",
        ["party_supply"] = "res://assets/audio/voice/day6/paper_rustle.mp3"
    };

    [Export]
    public NodePath SpawnRegionPath { get; set; } = "Day6Bg/10空纸箱/Area2D/CollisionPolygon2D";

    [Export(PropertyHint.Range, "50,350,1")]
    public float BoxItemSize { get; set; } = 240.0f;

    [Export(PropertyHint.Range, "0.25,1,0.05")]
    public float OtherBoxItemScale { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "25,100,1")]
    public float SnapDistance { get; set; } = 65.0f;

    [Export(PropertyHint.Range, "0.15,0.7,0.01")]
    public float TargetOpacity { get; set; } = 0.55f;

    [Export(PropertyHint.Range, "0.1,0.5,0.01")]
    public float MoveDuration { get; set; } = 0.2f;

    [Export(PropertyHint.Range, "0.2,2,0.1")]
    public float GroupDelay { get; set; } = 0.7f;

    private readonly RandomNumberGenerator _random = new();
    private readonly Dictionary<string, List<PartyItem>> _groups = new();
    private readonly Dictionary<string, AudioStream> _placementSounds = new();
    private AudioStreamPlayer _musicPlayer = null!;
    private TaskManager _taskManager = null!;
    private AudioManager _audio = null!;
    private CollisionPolygon2D _spawnRegion = null!;
    private Node2D _contents = null!;
    private PartyItem? _current;
    private bool _dragging;
    private bool _animating;
    private Vector2 _grabOffset;
    private Tween? _moveTween;
    private string _pendingGroup = string.Empty;
    private double _groupCountdown;

    public override void _Ready()
    {
        _random.Randomize();
        _taskManager = GetNode<TaskManager>("/root/TaskManager");
        _audio = GetNode<AudioManager>("/root/AudioManager");
        _spawnRegion = GetNodeOrNull<CollisionPolygon2D>(SpawnRegionPath);
        if (_spawnRegion == null || _spawnRegion.Polygon.Length < 3)
        {
            GD.PushError($"Day6Party: missing or empty spawn polygon at '{SpawnRegionPath}'.");
            SetProcess(false);
            SetProcessInput(false);
            SetProcessUnhandledInput(false);
            return;
        }

        // Set the day before loading, including when running this scene directly with F6.
        GetNode<Node>("/root/GameState").Call("set_selected_day", 6);
        if (!_taskManager.LoadDay(6) || !CreateItems()) return;
        foreach ((string objectType, string path) in PlacementSoundPaths)
        {
            if (!ResourceLoader.Exists(path)) continue;
            AudioStream? sound = GD.Load<AudioStream>(path);
            if (sound != null) _placementSounds[objectType] = sound;
        }
        AudioStreamMP3 music = (AudioStreamMP3)GD.Load<AudioStreamMP3>(MusicPath).Duplicate();
        music.Loop = true;
        _musicPlayer = new AudioStreamPlayer { Name = "Day6MusicPlayer", Bus = "Music", Stream = music };
        AddChild(_musicPlayer);
        _audio.SetBackgroundMusicPaused(true);
        _musicPlayer.Play();
        _taskManager.OnTaskActivated += HandleTaskActivated;
        _taskManager.OnDayReset += HandleDayReset;
        ShowCurrentItem();
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_musicPlayer)) _musicPlayer.Stop();
        if (IsInstanceValid(_audio)) _audio.SetBackgroundMusicPaused(false);
        _moveTween?.Kill();
        if (IsInstanceValid(_taskManager))
        {
            _taskManager.OnTaskActivated -= HandleTaskActivated;
            _taskManager.OnDayReset -= HandleDayReset;
        }
    }

    public override void _Process(double delta)
    {
        if (HasBlockingOverlay() || !GetWindow().HasFocus())
        {
            if (_dragging && _current != null)
            {
                _dragging = false;
                AnimateItem(_current, false);
            }
            return;
        }
        if (_dragging && !Input.IsMouseButtonPressed(MouseButton.Left) && _current != null)
        {
            _dragging = false;
            AnimateItem(_current, false);
        }
        if (_pendingGroup.Length == 0 || _animating) return;
        _groupCountdown -= delta;
        if (_groupCountdown > 0) return;
        _pendingGroup = string.Empty;
        ShowCurrentItem();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_current == null || _current.Placed || _animating || _pendingGroup.Length > 0
            || HasBlockingOverlay()
            || @event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;
        Vector2 mouse = GetGlobalMousePosition();
        if (!ContainsOpaquePoint(_current, mouse)) return;
        _dragging = true;
        _grabOffset = _current.Sprite.GlobalPosition - mouse;
        _current.Sprite.ZIndex = 20;
        _audio.PlayUiClick();
        GetViewport().SetInputAsHandled();
    }

    public override void _Input(InputEvent @event)
    {
        if (!_dragging || _current == null) return;
        if (HasBlockingOverlay())
        {
            _dragging = false;
            AnimateItem(_current, false);
            return;
        }
        if (@event is InputEventMouseMotion)
        {
            MoveHeldItem();
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            MoveHeldItem();
            _dragging = false;
            bool accepted = _current.Sprite.GlobalPosition.DistanceTo(_current.Target.GlobalPosition) <= SnapDistance;
            AnimateItem(_current, accepted);
            GetViewport().SetInputAsHandled();
        }
    }

    private void MoveHeldItem()
    {
        if (_current == null) return;
        Vector2 desired = GetGlobalMousePosition() + _grabOffset;
        Rect2 viewport = GetViewport().GetVisibleRect();
        Transform2D canvas = GetCanvasTransform();
        Vector2 screen = canvas * desired;
        // Keep the held object's center in the viewport even if the mouse leaves the window.
        screen.X = Mathf.Clamp(screen.X, viewport.Position.X + 20, viewport.End.X - 20);
        screen.Y = Mathf.Clamp(screen.Y, viewport.Position.Y + 20, viewport.End.Y - 20);
        _current.Sprite.GlobalPosition = canvas.AffineInverse() * screen;
    }

    private bool CreateItems()
    {
        using Godot.FileAccess file = Godot.FileAccess.Open(ConfigPath, Godot.FileAccess.ModeFlags.Read);
        if (file == null) return ConfigurationError("Cannot open task configuration.");
        PartyLayout? layout;
        try { layout = JsonSerializer.Deserialize<PartyLayout>(file.GetAsText()); }
        catch (JsonException exception) { return ConfigurationError(exception.Message); }
        if (layout?.Groups == null || layout.Groups.Count != 8 || _taskManager.Tasks.Count != 8)
            return ConfigurationError("Expected eight groups.");

        _contents = new Node2D { Name = "PartyItems" };
        AddChild(_contents);
        // Editor visibility is only for arranging the scene; runtime reveals the current target.
        Node2D? targetRoot = GetNodeOrNull<Node2D>("Day6Targets");
        if (targetRoot == null) return ConfigurationError("Missing Day6Targets scene node.");
        targetRoot.Show();
        foreach (Node child in targetRoot.GetChildren())
            if (child is Node2D targetGroup) targetGroup.Show();
        foreach (TaskRuntimeState task in _taskManager.Tasks)
        {
            PartyGroup? group = layout.Groups.Find(candidate => candidate.Id == task.Definition.Id);
            if (group?.Placements == null || group.Placements.Count != 5
                || task.Definition.TargetCount != 5 || task.Definition.Operation != "drag"
                || task.Definition.TargetIds.Count != 5)
                return ConfigurationError($"Group '{task.Definition.Id}' needs five drag placements and object IDs.");
            List<PartyItem> items = [];
            _groups.Add(task.Definition.Id, items);
            for (int index = 0; index < 5; index++)
            {
                PartyPlacement placement = group.Placements[index];
                if (string.IsNullOrWhiteSpace(placement.TargetPath))
                    return ConfigurationError($"Missing target_path in '{group.Id}', item {index + 1}.");
                Sprite2D? target = GetNodeOrNull<Sprite2D>(placement.TargetPath);
                Texture2D? texture = target?.Texture;
                if (target == null || texture == null || Math.Abs(target.GlobalScale.X) < 0.0001f
                    || Math.Abs(target.GlobalScale.Y) < 0.0001f)
                    return ConfigurationError($"Invalid target sprite at '{placement.TargetPath}'.");

                Image pixels = texture.GetImage();
                Rect2I bounds = pixels.GetUsedRect();
                if (bounds.Size.X == 0 || bounds.Size.Y == 0) return ConfigurationError($"Empty image at '{placement.TargetPath}'.");
                float sizeFactor = task.Definition.TargetType is "balloon" or "banner" ? 1.0f : OtherBoxItemScale;
                float boxSize = BoxItemSize * sizeFactor * _random.RandfRange(0.97f, 1.03f);
                float rotation = target.GlobalRotation
                    + Mathf.DegToRad(_random.RandfRange(-placement.RotationVariation, placement.RotationVariation));
                string id = task.Definition.TargetIds[index];
                // Position, scale, rotation and offset come directly from the saved scene.
                target.SelfModulate = new Color(1, 1, 1, TargetOpacity);
                target.Hide();
                Sprite2D sprite = new()
                {
                    Name = id, Texture = texture, Offset = target.Offset, Centered = target.Centered,
                    FlipH = target.FlipH, FlipV = target.FlipV,
                    Scale = Vector2.One * boxSize / Math.Max(bounds.Size.X, bounds.Size.Y),
                    ZIndex = 5, Visible = false,
                    SelfModulate = new Color(_random.RandfRange(0.96f, 1), _random.RandfRange(0.96f, 1), 1)
                };
                _contents.AddChild(sprite);
                sprite.GlobalRotation = rotation;
                items.Add(new PartyItem(task.Definition, id, sprite, target, pixels)
                {
                    HomeScale = sprite.GlobalScale,
                    HomeRotation = sprite.GlobalRotation
                });
            }
        }
        return true;
    }

    private void ShowCurrentItem()
    {
        TaskRuntimeState? task = _taskManager.GetActiveTask();
        _current = null;
        if (task == null || !_groups.TryGetValue(task.Definition.Id, out List<PartyItem>? items)
            || task.Progress >= items.Count) return;
        PartyItem item = items[task.Progress];
        item.HomePosition = RandomBoxPosition();
        item.Sprite.GlobalPosition = item.HomePosition;
        item.Sprite.ZIndex = 5;
        item.Sprite.Show();
        item.Target.Show();
        _current = item;
    }

    private Vector2 RandomBoxPosition()
    {
        Vector2[] polygon = _spawnRegion.Polygon;
        Rect2 bounds = new(polygon[0], Vector2.Zero);
        foreach (Vector2 point in polygon) bounds = bounds.Expand(point);
        // Prefer the interior so each refreshed item sits in the box opening.
        Rect2 inner = new(bounds.Position + bounds.Size * 0.25f, bounds.Size * 0.5f);
        for (int attempt = 0; attempt < 128; attempt++)
        {
            Vector2 candidate = new(_random.RandfRange(inner.Position.X, inner.End.X),
                _random.RandfRange(inner.Position.Y, inner.End.Y));
            if (Geometry2D.IsPointInPolygon(candidate, polygon)) return _spawnRegion.ToGlobal(candidate);
        }
        // Polygon centroids can be outside a concave polygon; a triangle centroid is inside.
        int[] triangles = Geometry2D.TriangulatePolygon(polygon);
        if (triangles.Length >= 3)
            return _spawnRegion.ToGlobal((polygon[triangles[0]] + polygon[triangles[1]] + polygon[triangles[2]]) / 3);
        return _spawnRegion.ToGlobal(polygon[0]);
    }

    private void AnimateItem(PartyItem item, bool accept)
    {
        _animating = true;
        _moveTween?.Kill();
        Tween tween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _moveTween = tween;
        tween.SetParallel();
        tween.TweenProperty(item.Sprite, "global_position", accept ? item.Target.GlobalPosition : item.HomePosition, MoveDuration);
        tween.TweenProperty(item.Sprite, "global_scale", accept ? item.Target.GlobalScale : item.HomeScale, MoveDuration);
        tween.TweenProperty(item.Sprite, "global_rotation", accept ? item.Target.GlobalRotation : item.HomeRotation, MoveDuration);
        tween.TweenProperty(item.Sprite, "skew", accept ? item.Target.GlobalTransform.Skew : 0.0f, MoveDuration);
        tween.Chain().TweenCallback(Callable.From(() =>
        {
            _moveTween = null;
            _animating = false;
            if (!accept) { item.Sprite.ZIndex = 5; return; }
            Godot.Collections.Dictionary report = new()
            {
                ["operation"] = "drag", ["object_id"] = item.Id,
                ["object_type"] = item.Definition.TargetType, ["object_color"] = "",
                ["object_tags"] = new string[] { item.Definition.TargetTag },
                ["timestamp"] = (long)Time.GetTicksMsec()
            };
            if (!_taskManager.TryHandleInteraction(report)) { AnimateItem(item, false); return; }
            item.Sprite.GlobalTransform = item.Target.GlobalTransform;
            item.Placed = true;
            // Keep the editor-authored draw order after the target disappears.
            item.Sprite.ZAsRelative = false;
            item.Sprite.ZIndex = GetCanvasZIndex(item.Target);
            item.Target.Hide();
            _current = null;
            if (_placementSounds.TryGetValue(item.Definition.TargetType, out AudioStream? sound))
                _audio.PlayEffect(sound);
            else
                _audio.PlaySoftInteractionEffect();
            // The fifth placement triggers OnTaskActivated; wait briefly before the next group.
            if (_pendingGroup.Length == 0) ShowCurrentItem();
        }));
    }

    private void HandleTaskActivated(string taskId)
    {
        _pendingGroup = taskId;
        _groupCountdown = GroupDelay;
    }

    private void HandleDayReset(int dayId)
    {
        if (dayId != 6) return;
        _moveTween?.Kill();
        _moveTween = null;
        _dragging = false;
        _animating = false;
        _pendingGroup = string.Empty;
        foreach (List<PartyItem> items in _groups.Values)
        {
            foreach (PartyItem item in items)
            {
                item.Placed = false;
                item.Sprite.Hide();
                item.Target.Hide();
                item.Sprite.GlobalScale = item.HomeScale;
                item.Sprite.GlobalRotation = item.HomeRotation;
                item.Sprite.Skew = 0.0f;
                item.Sprite.ZAsRelative = true;
                item.Sprite.ZIndex = 5;
            }
        }
        ShowCurrentItem();
    }

    private static int GetCanvasZIndex(CanvasItem item)
    {
        int result = item.ZIndex;
        while (item.ZAsRelative && item.GetParent() is CanvasItem parent)
        {
            result += parent.ZIndex;
            item = parent;
        }
        return result;
    }

    private bool HasBlockingOverlay()
    {
        if (GetTree().Paused) return true;
        foreach (Node child in GetChildren())
            if (child is CanvasLayer { Layer: >= 50 }) return true;
        return false;
    }

    private bool ConfigurationError(string message)
    {
        GD.PushError("Day6Party: " + message);
        if (IsInstanceValid(_contents)) _contents.QueueFree();
        _groups.Clear();
        SetProcess(false);
        SetProcessInput(false);
        SetProcessUnhandledInput(false);
        return false;
    }

    private static bool ContainsOpaquePoint(PartyItem item, Vector2 globalPoint)
    {
        Vector2 pixel = item.Sprite.ToLocal(globalPoint)
            + (item.Sprite.Centered ? item.Sprite.Texture.GetSize() * 0.5f : Vector2.Zero) - item.Sprite.Offset;
        if (item.Sprite.FlipH) pixel.X = item.Pixels.GetWidth() - 1 - pixel.X;
        if (item.Sprite.FlipV) pixel.Y = item.Pixels.GetHeight() - 1 - pixel.Y;
        int x = (int)Math.Floor(pixel.X), y = (int)Math.Floor(pixel.Y);
        return x >= 0 && y >= 0 && x < item.Pixels.GetWidth() && y < item.Pixels.GetHeight()
            && item.Pixels.GetPixel(x, y).A > 0.08f;
    }

    private sealed class PartyItem(TaskDefinition definition, string id, Sprite2D sprite, Sprite2D target, Image pixels)
    {
        public TaskDefinition Definition { get; } = definition;
        public string Id { get; } = id;
        public Sprite2D Sprite { get; } = sprite;
        public Sprite2D Target { get; } = target;
        public Image Pixels { get; } = pixels;
        public Rect2I Bounds { get; } = pixels.GetUsedRect();
        public Vector2 HomePosition { get; set; }
        public Vector2 HomeScale { get; set; }
        public float HomeRotation { get; set; }
        public bool Placed { get; set; }
    }

    private sealed class PartyLayout
    {
        [JsonPropertyName("tasks")]
        public List<PartyGroup> Groups { get; set; } = [];
    }

    private sealed class PartyGroup
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        [JsonPropertyName("placements")]
        public List<PartyPlacement> Placements { get; set; } = [];
    }

    private sealed class PartyPlacement
    {
        [JsonPropertyName("target_path")]
        public string TargetPath { get; set; } = string.Empty;
        [JsonPropertyName("rotation_variation")]
        public float RotationVariation { get; set; } = 2;
    }
}
