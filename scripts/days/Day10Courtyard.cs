#nullable enable annotations

using Godot;
using NewGameProject.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewGameProject.Days;

public partial class Day10Courtyard : Node2D
{
    private const string AssetRoot = "res://assets/images/objects/day10/";
    private const string MusicPath = "res://assets/audio/voice/day10/day10_bgm.mp3";
    private static readonly Dictionary<string, string> InteractionSoundPaths = new()
    {
        ["leaf"] = "res://assets/audio/voice/day4/leaf_rustle.mp3",
        ["paper"] = "res://assets/audio/voice/day6/paper_rustle.mp3",
        ["tool"] = "res://assets/audio/voice/day4/toy_place.mp3",
        ["sign"] = "res://assets/audio/voice/day5/part_install.mp3",
        ["packet"] = "res://assets/audio/voice/day10/seed_packet_open.mp3",
        ["seed"] = "res://assets/audio/voice/generic_soft.mp3",
        ["pot"] = "res://assets/audio/voice/day10/watering.mp3",
        ["pinwheel"] = "res://assets/audio/voice/day10/pinwheel_spin.mp3"
    };
    private static readonly string[] GroupIds =
        ["click_leaves", "click_paper", "drag_tools", "open_packets", "plant_seeds", "water_flowers", "drag_signs", "start_pinwheels"];
    private static readonly string[][] Assets =
    [
        ["leaf_green_oval", "leaf_yellow_willow", "leaf_amber_oak", "leaf_orange_maple", "leaf_green_heart"],
        ["paper_ball_white", "paper_ball_cream", "paper_ball_blue", "paper_ball_pink", "paper_ball_flat"],
        ["tool_trowel", "tool_hand_rake", "tool_watering_can", "tool_broom", "tool_bucket"],
        ["seed_packet_red_tulip_closed", "seed_packet_yellow_sunflower_closed", "seed_packet_blue_flower_closed", "seed_packet_pink_daisy_closed", "seed_packet_purple_violet_closed"],
        ["seeds_red_tulip", "seeds_yellow_sunflower", "seeds_blue_flower", "seeds_pink_daisy", "seeds_purple_violet"],
        ["flower_red_tulip", "flower_yellow_sunflower", "flower_blue", "flower_pink_daisy", "flower_purple_violet"],
        ["sign_flower_shop", "sign_bakery", "sign_lake", "sign_playground", "sign_forest"],
        ["pinwheel_red_rotor", "pinwheel_blue_rotor", "pinwheel_yellow_rotor", "pinwheel_pink_rotor", "pinwheel_purple_rotor"]
    ];

    // Layout lives in level_10.tscn. Saved node transforms are the source of truth.
    private static readonly string[] SceneGroups =
        ["Leaves", "Paper", "Tools", "SeedPackets", "Seeds", "", "Signs", "Pinwheels"];

    [Export(PropertyHint.Range, "25,120,1")] public float SnapDistance { get; set; } = 75;
    [Export(PropertyHint.Range, "0.1,0.6,0.01")] public float TargetOpacity { get; set; } = 0.5f;
    [Export(PropertyHint.Range, "0.15,0.8,0.05")] public float ActionDuration { get; set; } = 0.35f;
    [Export(PropertyHint.Range, "0.3,1.5,0.1")] public float GroupDelay { get; set; } = 0.6f;
    [Export(PropertyHint.Range, "30,300,10")] public float PinwheelSpeed { get; set; } = 150;
    private readonly Dictionary<string, List<YardItem>> _groups = new();
    private readonly Dictionary<string, AudioStream> _interactionSounds = new();
    private AudioStreamPlayer _musicPlayer = null!;
    private readonly List<Tween> _animations = [];
    private readonly List<Node2D> _effects = [];
    private TaskManager _tasks = null!;
    private AudioManager _audio = null!;
    private Sprite2D _background = null!;
    private Node2D _objects = null!;
    private Node _taskbar = null!;
    private YardItem? _dragged;
    private Vector2 _grabOffset;
    private string _activeGroup = "";
    private string _pendingGroup = "";
    private double _groupCountdown;
    private bool _busy;
    private bool _restoreTaskbar;

    public override void _EnterTree() =>
        GetNode<Node>("/root/GameState").Call("set_selected_day", 10);

    public override void _Ready()
    {
        _tasks = GetNode<TaskManager>("/root/TaskManager");
        _audio = GetNode<AudioManager>("/root/AudioManager");
        _background = GetNode<Sprite2D>("Day10Bg");
        _taskbar = GetNode<Node>("Taskbar");
        // Hidden in the saved scene so the editor can access props behind the taskbar.
        _taskbar.GetNode<CanvasLayer>("CanvasLayer").Show();
        _objects = GetNode<Node2D>("CourtyardObjects");
        if (!_tasks.LoadDay(10)) return;
        try { CreateItems(); }
        catch (InvalidOperationException error)
        {
            GD.PushError($"Day10: {error.Message}");
            SetProcess(false);
            SetProcessUnhandledInput(false);
            return;
        }
        ConfigureAudio();
        _tasks.OnTaskActivated += ActivateGroup;
        _tasks.OnDayReset += ResetDay;
        RestoreItems();
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_musicPlayer))
        {
            _musicPlayer.Stop();
            if (IsInstanceValid(_audio)) _audio.SetBackgroundMusicPaused(false);
        }
        if (IsInstanceValid(_tasks))
        {
            _tasks.OnTaskActivated -= ActivateGroup;
            _tasks.OnDayReset -= ResetDay;
        }
        StopAnimations();
        if (IsInstanceValid(_audio)) _audio.StopTaskHint();
        foreach (List<YardItem> group in _groups.Values)
            foreach (YardItem item in group) item.Pixels.Dispose();
        _groups.Clear();
    }

    private void ConfigureAudio()
    {
        foreach ((string objectType, string path) in InteractionSoundPaths)
        {
            if (!ResourceLoader.Exists(path)) continue;
            AudioStream? sound = GD.Load<AudioStream>(path);
            if (sound != null) _interactionSounds[objectType] = sound;
        }
        AudioStreamMP3? source = GD.Load<AudioStreamMP3>(MusicPath);
        if (source == null) return;
        AudioStreamMP3 music = (AudioStreamMP3)source.Duplicate();
        music.Loop = true;
        _musicPlayer = new AudioStreamPlayer { Name = "Day10MusicPlayer", Bus = "Music", Stream = music };
        AddChild(_musicPlayer);
        _audio.SetBackgroundMusicPaused(true);
        _musicPlayer.Play();
    }

    public override void _Process(double delta)
    {
        if (HasBlockingOverlay() || !GetWindow().HasFocus())
        {
            CancelDrag();
            _audio.StopTaskHint();
            return;
        }
        // A release outside the window cannot leave a sprite stuck to the cursor.
        if (_dragged != null && !Input.IsMouseButtonPressed(MouseButton.Left)) CancelDrag();
        foreach (List<YardItem> group in _groups.Values)
            foreach (YardItem item in group)
                if (item.GroupIndex == 7 && item.Done)
                    item.Sprite.Rotation += Mathf.DegToRad(PinwheelSpeed) * (float)delta;
        if (_pendingGroup.Length == 0 || _busy) return;
        _groupCountdown -= delta;
        if (_groupCountdown > 0) return;
        string id = _pendingGroup;
        _pendingGroup = "";
        ShowGroup(id);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (_busy || _pendingGroup.Length > 0 || HasBlockingOverlay() || !GetWindow().HasFocus()
            || input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } button
            || !_groups.TryGetValue(_activeGroup, out List<YardItem>? items)) return;
        Vector2 mouse = ToLocal(GetGlobalMousePosition());
        for (int index = items.Count - 1; index >= 0; index--)
        {
            YardItem item = items[index];
            if (item.Done || !HitTest(item, mouse) || !_tasks.CanInteract(Report(item))) continue;
            if (item.Definition.Operation == "double_click" && !button.DoubleClick)
            {
                // Consume the first press, but only the native double-click event completes a target.
                GetViewport().SetInputAsHandled();
                return;
            }
            if (item.Definition.Operation == "drag")
            {
                _dragged = item;
                _grabOffset = ToLocal(item.Sprite.GlobalPosition) - mouse;
                item.Sprite.ZIndex = 20;
                _audio.PlayUiClick();
            }
            else
            {
                CompleteItem(item);
            }
            GetViewport().SetInputAsHandled();
            return;
        }
    }

    public override void _Input(InputEvent input)
    {
        if (_dragged == null) return;
        if (HasBlockingOverlay() || !GetWindow().HasFocus()) { CancelDrag(); return; }
        if (input is InputEventMouseMotion)
        {
            MoveDragged();
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            MoveDragged();
            YardItem item = _dragged;
            _dragged = null;
            if (IsMatchingDrop(item))
                CompleteItem(item);
            else
                ReturnItem(item);
            GetViewport().SetInputAsHandled();
        }
    }

    private void CreateItems()
    {
        if (_tasks.Tasks.Count != Assets.Length) throw new InvalidOperationException("Expected 8 task groups.");
        _objects.Show();
        ShowLayoutContainers(_objects);
        for (int groupIndex = 0; groupIndex < Assets.Length; groupIndex++)
        {
            // Match identity rather than display order, so reordering tasks keeps their correct assets/behavior.
            TaskDefinition definition = _tasks.Tasks.Single(task => task.Definition.Id == GroupIds[groupIndex]).Definition;
            List<YardItem> items = [];
            _groups.Add(definition.Id, items);
            for (int index = 0; index < 5; index++)
            {
                string spritePath = groupIndex switch
                {
                    5 => $"PlantingSpots/Pot{index + 1}/Flower",
                    7 => $"Pinwheels/Pinwheel{index + 1}/Rotor",
                    _ => $"{SceneGroups[groupIndex]}/{Assets[groupIndex][index]}"
                };
                Sprite2D sprite = RequireNode<Sprite2D>(spritePath);
                Texture2D texture = sprite.Texture ?? throw new InvalidOperationException($"Missing texture on {spritePath}.");
                YardItem item = new(definition, definition.TargetIds[index], groupIndex, index, sprite, texture.GetImage());
                items.Add(item);
                if (groupIndex is 2 or 4 or 6)
                {
                    string targetPath = groupIndex switch
                    {
                        2 => $"PlacementTargets/ToolSlots/{Assets[groupIndex][index]}",
                        4 => $"PlantingSpots/Pot{index + 1}/SeedTarget",
                        _ => $"PlacementTargets/SignSlots/{Assets[groupIndex][index]}"
                    };
                    item.Target = RequireNode<Sprite2D>(targetPath);
                    item.Target.Modulate = new Color(0.4f, 0.8f, 1, TargetOpacity);
                    // Convert the target's transform into the movable sprite's parent space.
                    Transform2D destination = sprite.GetParent<Node2D>().GlobalTransform.AffineInverse() * item.Target.GlobalTransform;
                    item.TargetPosition = destination.Origin;
                    item.TargetScale = destination.Scale;
                    item.TargetRotation = destination.Rotation;
                }
                if (groupIndex == 5)
                    item.Marker = RequireNode<Marker2D>($"PlantingSpots/Pot{index + 1}/WaterClickArea");
                if (groupIndex == 3)
                    item.OpenedTexture = LoadTexture(Assets[groupIndex][index].Replace("_closed", "_opened"));
                if (groupIndex == 7)
                    item.Support = RequireNode<Sprite2D>($"Pinwheels/Pinwheel{index + 1}/Stick");
            }
        }
    }

    private T RequireNode<T>(string path) where T : Node =>
        _objects.GetNodeOrNull<T>(path) ?? throw new InvalidOperationException($"Missing layout node: CourtyardObjects/{path}");

    // The editor's eye icons can hide whole groups while arranging the layout.
    private static void ShowLayoutContainers(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is Node2D container && child is not Sprite2D && child is not Line2D) container.Show();
            ShowLayoutContainers(child);
        }
    }

    private void ShowGroup(string id)
    {
        if (!_groups.TryGetValue(id, out List<YardItem>? current)) return;
        RestoreTaskbar();
        _activeGroup = id;
        if (current[0].GroupIndex == 2 && !_taskbar.Get("_is_collapsed").AsBool())
        {
            _taskbar.Call("_set_collapsed", true);
            _restoreTaskbar = true;
        }
        foreach (List<YardItem> group in _groups.Values)
        {
            foreach (YardItem item in group)
            {
                bool active = group == current;
                // Keep installed tools, planted seeds, grown flowers and repaired signs in place.
                bool persistent = item.Done && item.GroupIndex is 2 or 4 or 5 or 6 or 7;
                if (item.GroupIndex == 4 && _groups["water_flowers"][item.Index].Done) persistent = false;
                // All courtyard clutter is visible from the start; the taskbar still guides the order.
                bool sharedClutter = !item.Done && item.GroupIndex is 0 or 1 or 2 or 6;
                item.Sprite.Visible = item.GroupIndex == 7 || persistent || sharedClutter
                    || (active && !item.Done && item.GroupIndex != 5);
                if (item.Target != null) item.Target.Visible = active && !item.Done;
                if (item.Marker != null) item.Marker.Visible = active && !item.Done;
                if (item.Support != null) item.Support.Visible = true;
                // Open packets remain behind the seed targets only until their seeds are planted.
                if (item.GroupIndex == 3 && current[0].GroupIndex == 4)
                    item.Sprite.Visible = !_groups["plant_seeds"][item.Index].Done;
            }
        }
        _audio.PlayAutomaticTaskHint(current[0].Definition.HintAudioPath);
    }

    private void CompleteItem(YardItem item)
    {
        _busy = true;
        if (!_tasks.TryHandleInteraction(Report(item))) { _busy = false; ReturnItem(item); return; }
        item.Done = true;
        item.Target?.Hide();
        item.Marker?.Hide();
        if (_interactionSounds.TryGetValue(item.Definition.TargetType, out AudioStream? sound))
            _audio.PlayEffect(sound);
        else
            _audio.PlayDefaultInteractionEffect();
        Tween animation = NewAnimation();
        switch (item.GroupIndex)
        {
            case 0:
            case 1:
                animation.SetParallel();
                animation.TweenProperty(item.Sprite, "scale", item.HomeScale * 0.15f, ActionDuration);
                animation.TweenProperty(item.Sprite, "modulate:a", 0, ActionDuration);
                animation.Chain().TweenCallback(Callable.From(item.Sprite.Hide));
                break;
            case 2:
            case 4:
            case 6:
                animation.SetParallel();
                animation.TweenProperty(item.Sprite, "position", item.TargetPosition, ActionDuration);
                animation.TweenProperty(item.Sprite, "scale", item.TargetScale, ActionDuration);
                animation.TweenProperty(item.Sprite, "rotation", item.TargetRotation, ActionDuration);
                if (item.GroupIndex == 4) _groups["open_packets"][item.Index].Sprite.Hide();
                break;
            case 3:
                item.Sprite.Texture = item.OpenedTexture;
                item.Sprite.Scale = item.HomeScale * 0.9f;
                animation.TweenProperty(item.Sprite, "scale", item.HomeScale, ActionDuration)
                    .SetTrans(Tween.TransitionType.Back);
                break;
            case 5:
                _groups["plant_seeds"][item.Index].Sprite.Hide();
                ShowWatering(item);
                item.Sprite.Show();
                item.Sprite.Scale = item.HomeScale * 0.1f;
                // Keep the stem rooted in the flowerpot while it grows.
                Vector2 stem = new(0, item.Sprite.Texture.GetHeight() * 0.5f);
                Vector2 bottom = item.HomeTransform * stem;
                item.Sprite.Position += bottom - item.Sprite.Transform * stem;
                animation.SetParallel();
                animation.TweenProperty(item.Sprite, "scale", item.HomeScale, ActionDuration * 2);
                animation.TweenProperty(item.Sprite, "position", item.HomePosition, ActionDuration * 2);
                break;
            case 7:
                animation.TweenInterval(ActionDuration);
                break;
        }
        animation.Chain().TweenCallback(Callable.From(() => { item.Sprite.ZIndex = item.HomeZIndex; _busy = false; }));
    }

    private void ReturnItem(YardItem item)
    {
        _busy = true;
        Tween tween = NewAnimation().SetParallel();
        tween.TweenProperty(item.Sprite, "position", item.HomePosition, ActionDuration);
        tween.TweenProperty(item.Sprite, "scale", item.HomeScale, ActionDuration);
        tween.TweenProperty(item.Sprite, "rotation", item.HomeRotation, ActionDuration);
        tween.Chain().TweenCallback(Callable.From(() => { item.Sprite.ZIndex = item.HomeZIndex; _busy = false; }));
    }

    private void ShowWatering(YardItem item)
    {
        Vector2 stem = new(0, item.Sprite.Texture.GetHeight() * 0.5f);
        Node2D effect = new() { Position = _objects.ToLocal(item.Sprite.ToGlobal(stem)), ZIndex = 15 };
        _objects.AddChild(effect);
        _effects.Add(effect);
        Texture2D texture = LoadTexture("tool_watering_can");
        Sprite2D can = new() { Texture = texture, Position = new Vector2(58, -66), RotationDegrees = -18 };
        can.Scale = Vector2.One * 70 / Mathf.Max(texture.GetWidth(), texture.GetHeight());
        effect.AddChild(can);
        for (int index = 0; index < 5; index++)
        {
            Polygon2D drop = new() { Color = new Color(0.25f, 0.7f, 1), Position = new Vector2(index * 6, -40 - index * 4) };
            drop.Polygon = [new(-3, 0), new(0, -6), new(3, 0), new(2, 4), new(-2, 4)];
            effect.AddChild(drop);
            NewAnimation().TweenProperty(drop, "position:y", 0, ActionDuration).SetDelay(index * 0.04f);
        }
        Tween tween = NewAnimation();
        tween.TweenInterval(ActionDuration * 2);
        tween.TweenProperty(effect, "modulate:a", 0, 0.15);
        tween.TweenCallback(Callable.From(() => { _effects.Remove(effect); effect.QueueFree(); }));
    }

    private bool HitTest(YardItem item, Vector2 point)
    {
        if (item.GroupIndex == 5)
            return item.Marker != null && item.Marker.Visible
                && item.Marker.ToLocal(ToGlobal(point)).Length() <= 46;
        if (!item.Sprite.Visible) return false;
        Vector2 local = item.Sprite.ToLocal(ToGlobal(point));
        Vector2 pixel = local + item.Sprite.Texture.GetSize() * 0.5f;
        int x = (int)Math.Floor(pixel.X), y = (int)Math.Floor(pixel.Y);
        if (x >= 0 && y >= 0 && x < item.Pixels.GetWidth() && y < item.Pixels.GetHeight()
            && item.Pixels.GetPixel(x, y).A > 0.08f) return true;
        // Forgiving small padding around the drawn object, especially narrow leaves and tools.
        return item.Sprite.GetRect().Grow(10 / Mathf.Max(item.Sprite.Scale.X, 0.01f)).HasPoint(local);
    }

    private Godot.Collections.Dictionary Report(YardItem item) => new()
    {
        ["operation"] = item.Definition.Operation, ["object_id"] = item.Id,
        ["object_type"] = item.Definition.TargetType, ["object_color"] = "",
        ["object_tags"] = new string[] { item.Definition.TargetTag },
        ["timestamp"] = (long)Time.GetTicksMsec()
    };

    private void MoveDragged()
    {
        if (_dragged == null) return;
        Vector2 point = ToLocal(GetGlobalMousePosition()) + _grabOffset;
        Vector2 half = _dragged.Sprite.Texture.GetSize() * _dragged.Sprite.GlobalScale.Abs() * 0.5f;
        Vector2 min = BackgroundPoint(Vector2.Zero) + half;
        Vector2 max = BackgroundPoint(_background.Texture.GetSize()) - half;
        _dragged.Sprite.GlobalPosition = ToGlobal(new Vector2(Mathf.Clamp(point.X, min.X, max.X), Mathf.Clamp(point.Y, min.Y, max.Y)));
    }

    private void CancelDrag()
    {
        if (_dragged == null) return;
        YardItem item = _dragged;
        _dragged = null;
        ReturnItem(item);
    }

    private bool IsMatchingDrop(YardItem item)
    {
        YardItem? closest = null;
        float distance = SnapDistance;
        foreach (YardItem candidate in _groups[_activeGroup])
        {
            if (candidate.Done || candidate.Target == null || !candidate.Target.Visible) continue;
            float current = ToLocal(item.Sprite.GlobalPosition).DistanceTo(ToLocal(candidate.Target.GlobalPosition));
            if (current > distance) continue;
            closest = candidate;
            distance = current;
        }
        return closest == item;
    }

    private bool HasBlockingOverlay()
    {
        foreach (Node child in GetChildren())
            if (child is CanvasLayer { Layer: >= 50 }) return true;
        return false;
    }

    private void ActivateGroup(string id) { _pendingGroup = id; _groupCountdown = GroupDelay; }
    private void ResetDay(int dayId) { if (dayId == 10) RestoreItems(); }

    private void RestoreItems()
    {
        StopAnimations();
        _dragged = null;
        _busy = false;
        _pendingGroup = "";
        foreach (Node2D effect in _effects) if (IsInstanceValid(effect)) effect.QueueFree();
        _effects.Clear();
        _audio.StopTaskHint();
        RestoreTaskbar();
        foreach (List<YardItem> group in _groups.Values)
        {
            foreach (YardItem item in group)
            {
                item.Done = false;
                item.Sprite.Texture = item.HomeTexture;
                item.Sprite.Position = item.HomePosition;
                item.Sprite.Scale = item.HomeScale;
                item.Sprite.Rotation = item.HomeRotation;
                item.Sprite.Modulate = item.HomeColor;
                item.Sprite.ZIndex = item.HomeZIndex;
            }
        }
        ShowGroup(_tasks.GetActiveTaskId());
    }

    private void RestoreTaskbar()
    {
        if (!_restoreTaskbar) return;
        _taskbar.Call("_set_collapsed", false);
        _restoreTaskbar = false;
    }

    private Tween NewAnimation()
    {
        _animations.RemoveAll(tween => !IsInstanceValid(tween) || !tween.IsValid());
        Tween tween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _animations.Add(tween);
        return tween;
    }

    private void StopAnimations()
    {
        foreach (Tween tween in _animations) if (IsInstanceValid(tween)) tween.Kill();
        _animations.Clear();
    }

    private Vector2 BackgroundPoint(Vector2 pixels) =>
        ToLocal(_background.ToGlobal(pixels - _background.Texture.GetSize() * 0.5f));

    private static Texture2D LoadTexture(string name) =>
        GD.Load<Texture2D>(AssetRoot + name + ".tres") ?? throw new InvalidOperationException($"Missing texture: {name}");

    private sealed class YardItem(TaskDefinition definition, string id, int groupIndex, int index, Sprite2D sprite, Image pixels)
    {
        public TaskDefinition Definition { get; } = definition;
        public string Id { get; } = id;
        public int GroupIndex { get; } = groupIndex;
        public int Index { get; } = index;
        public Sprite2D Sprite { get; } = sprite;
        public Image Pixels { get; } = pixels;
        public Texture2D HomeTexture { get; } = sprite.Texture;
        public Vector2 HomePosition { get; } = sprite.Position;
        public Vector2 HomeScale { get; } = sprite.Scale;
        public Transform2D HomeTransform { get; } = sprite.Transform;
        public float HomeRotation { get; } = sprite.Rotation;
        public Color HomeColor { get; } = sprite.Modulate;
        public int HomeZIndex { get; } = sprite.ZIndex;
        public Texture2D? OpenedTexture { get; set; }
        public Vector2 TargetPosition { get; set; }
        public Vector2 TargetScale { get; set; }
        public float TargetRotation { get; set; }
        public Sprite2D? Target { get; set; }
        public Sprite2D? Support { get; set; }
        public Node2D? Marker { get; set; }
        public bool Done { get; set; }
    }
}

