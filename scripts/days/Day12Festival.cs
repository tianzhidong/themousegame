#nullable enable annotations

using Godot;
using NewGameProject.Tasks;
using NewGameProject.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewGameProject.Days;

// Forty editable objects share the same scrolling park; no activity overlay is opened.
public partial class Day12Festival : Node2D
{
    private const string MusicPath = "res://assets/audio/voice/day12/day12_bgm.mp3";
    private static readonly string[] InstrumentSoundPaths =
    [
        "res://assets/audio/voice/day12/drum.mp3",
        "res://assets/audio/voice/day12/xylophone.mp3",
        "res://assets/audio/voice/day12/piano.mp3",
        "res://assets/audio/voice/day12/trumpet.mp3",
        "res://assets/audio/voice/day12/guitar.mp3"
    ];
    private static readonly Dictionary<Day12ActivityKind, string> InteractionSoundPaths = new()
    {
        [Day12ActivityKind.Balloons] = "res://assets/audio/voice/day12/balloon_pop.mp3",
        [Day12ActivityKind.Eggs] = "res://assets/audio/voice/day12/egg_crack.mp3",
        [Day12ActivityKind.Gifts] = "res://assets/audio/voice/day9/box_open.mp3",
        [Day12ActivityKind.Food] = "res://assets/audio/voice/generic_soft.mp3",
        [Day12ActivityKind.Luggage] = "res://assets/audio/voice/day9/box_land.mp3",
        [Day12ActivityKind.Tower] = "res://assets/audio/voice/day12/window_open.mp3",
        [Day12ActivityKind.Aquarium] = "res://assets/audio/voice/day12/bubbles.mp3"
    };
    [Export(PropertyHint.Range, "50,400,10")] public float ScrollStep { get; set; } = 180;
    [Export(PropertyHint.Range, "0.05,0.5,0.01")] public float ScrollDuration { get; set; } = 0.16f;
    [Export(PropertyHint.Range, "0,60,5")] public float SnapTolerance { get; set; } = 30;
    [Export(PropertyHint.Range, "0,30,1")] public float SwayDistance { get; set; } = 12;
    [Export(PropertyHint.Range, "0,0.12,0.01")] public float SizeVariation { get; set; } = 0.04f;
    [Export(PropertyHint.Range, "0,10,0.5")] public float RotationVariationDegrees { get; set; } = 3;
    [Export(PropertyHint.Range, "0.1,1.5,0.05")] public float PlacedScaleFactor { get; set; } = 0.8f;

    private readonly List<Activity> _activities = [];
    private readonly List<Item> _items = [];
    private readonly Dictionary<ulong, Rect2I> _visiblePixelRects = [];
    private readonly RandomNumberGenerator _random = new();
    private Sprite2D _background = null!, _taskbarArt = null!;
    private TaskPanel _taskPanel = null!;
    private AudioManager _audio = null!;
    private TaskManager _tasks = null!;
    private readonly Dictionary<Day12ActivityKind, AudioStream> _interactionSounds = new();
    private AudioStreamPlayer _musicPlayer = null!;
    private AudioStream[] _notes = [];
    private Tween? _scrollTween;
    private Item? _dragged, _lastClick;
    private ulong _lastClickTime;
    private Vector2 _grabOffset, _viewportSize;
    private string _focusedTaskId = "";
    private double _focusHoldSeconds;
    private float _targetY, _minimumY, _maximumY, _time;

    public override void _EnterTree() => GetNode<Node>("/root/GameState").Call("set_selected_day", 12);

    public override void _Ready()
    {
        _random.Randomize();
        _background = GetNode<Sprite2D>("Day12Bg");
        _taskbarArt = GetNode<Sprite2D>("Taskbar/CanvasLayer/Taskbar");
        _taskPanel = GetNode<TaskPanel>("Taskbar/TaskPanel");
        _audio = GetNode<AudioManager>("/root/AudioManager");
        _tasks = GetNode<TaskManager>("/root/TaskManager");
        _taskPanel.AutoShowResultOnCompletion = true;
        _tasks.LoadDay(12);
        foreach (Node child in GetNode<Node2D>("Day12Bg/Attractions").GetChildren())
        {
            if (child is not Sprite2D art || !art.HasMeta("task_id")) continue;
            string taskId = art.GetMeta("task_id").AsString();
            TaskRuntimeState task = _tasks.Tasks.Single(task => task.Definition.Id == taskId);
            if (task.Definition.TargetIds.Count != 5)
                throw new InvalidOperationException($"Day12 {taskId} requires five unique objects.");
            Activity activity = new(art, Day12ActivityCatalog.Get(taskId), task);
            _activities.Add(activity);
            Node2D targets = art.GetNode<Node2D>("Targets");
            for (int index = 0; index < 5; index++)
            {
                Node2D root = targets.GetNode<Node2D>($"Target{index + 1}");
                Sprite2D source = root.GetNode<Sprite2D>("ObjectSprite");
                Sprite2D? reveal = root.GetNodeOrNull<Sprite2D>("RevealedSprite");
                float variation = _random.RandfRange(1 - SizeVariation, 1 + SizeVariation);
                float rotation = Mathf.DegToRad(_random.RandfRange(-RotationVariationDegrees, RotationVariationDegrees));
                // A closed window must keep its pixel alignment with the building.
                if (activity.Kind != Day12ActivityKind.Tower)
                {
                    source.Scale *= variation;
                    source.Rotation += rotation;
                }
                if (reveal != null) { reveal.Scale *= variation; reveal.Rotation += rotation; }
                Item item = new(activity, index, root, source, reveal, _random.RandfRange(0, Mathf.Tau));
                item.LeftDoor = root.GetNodeOrNull<Node2D>("DoorLeft");
                item.RightDoor = root.GetNodeOrNull<Node2D>("DoorRight");
                item.WindowInterior = root.GetNodeOrNull<Polygon2D>("WindowInterior");
                item.BubbleOrigin = source.GetNodeOrNull<Marker2D>("BubbleOrigin");
                if (activity.IsDrag)
                {
                    Node2D drop = art.GetNode<Node2D>($"DropTargets/DropTarget{index + 1}");
                    item.DropShape = drop.GetNode<CollisionShape2D>("DropArea");
                    item.Landing = drop.GetNode<Marker2D>("LandingPoint");
                    item.Guide = drop.GetNode<Sprite2D>("GuideIcon");
                }
                activity.Items.Add(item);
                _items.Add(item);
                Restore(item);
            }
        }
        if (_activities.Count != 8 || _items.Count != 40)
            throw new InvalidOperationException("Day12 park requires eight activities and forty authored objects.");
        ConfigureAudio();
        _tasks.OnDayReset += ResetActivities;
        _tasks.OnTaskCompleted += CelebrateActivity;
        RecalculateBounds();
        FocusVisibleActivity();
    }

    private void ConfigureAudio()
    {
        foreach ((Day12ActivityKind kind, string path) in InteractionSoundPaths)
        {
            if (!ResourceLoader.Exists(path)) continue;
            AudioStream? sound = GD.Load<AudioStream>(path);
            if (sound != null) _interactionSounds[kind] = sound;
        }
        _notes = InstrumentSoundPaths.Select((path, index) =>
            (ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null)
            ?? (AudioStream)Day12InstrumentAudio.Create(index)).ToArray();
        AudioStreamMP3? source = GD.Load<AudioStreamMP3>(MusicPath);
        if (source == null) return;
        AudioStreamMP3 music = (AudioStreamMP3)source.Duplicate();
        music.Loop = true;
        _musicPlayer = new AudioStreamPlayer { Name = "Day12MusicPlayer", Bus = "Music", Stream = music };
        AddChild(_musicPlayer);
        _audio.SetBackgroundMusicPaused(true);
        _musicPlayer.Play();
    }

    private void PlayItemSound(Item item)
    {
        if (item.Activity.Kind == Day12ActivityKind.Instruments)
            _audio.PlayEffect(_notes[item.Index]);
        else if (_interactionSounds.TryGetValue(item.Activity.Kind, out AudioStream? sound))
            _audio.PlayEffect(sound);
        else
            _audio.PlayDefaultInteractionEffect();
    }

    public override void _Process(double delta)
    {
        if (_viewportSize != GetViewportRect().Size) RecalculateBounds();
        if (Blocked())
        {
            CancelDrag();
            _lastClick = null;
            _audio.StopTaskHint();
            if (IsInstanceValid(_scrollTween) && _scrollTween!.IsValid()) _scrollTween.Pause();
            return;
        }
        if (IsInstanceValid(_scrollTween) && _scrollTween!.IsValid() && !_scrollTween.IsRunning())
            _scrollTween.Play();
        if (_dragged != null && !Input.IsMouseButtonPressed(MouseButton.Left)) CancelDrag();
        _time += (float)delta;
        _focusHoldSeconds = Math.Max(0, _focusHoldSeconds - delta);
        foreach (Item item in _items)
        {
            if (item.Activity.Kind == Day12ActivityKind.Balloons && !item.Completed)
                item.Root.Position = item.Home + new Vector2(Mathf.Sin(_time * 0.8f + item.Phase),
                    Mathf.Cos(_time * 0.6f + item.Phase) * 0.65f) * SwayDistance;
            if (item.Activity.Kind == Day12ActivityKind.Aquarium)
                item.Source.Position = item.SourcePosition + new Vector2(Mathf.Sin(_time * 0.65f + item.Phase) * SwayDistance, 0);
        }
        bool settled = Mathf.Abs(_background.Position.Y - _targetY) < 0.8f;
        if (_dragged == null && settled)
        {
            if (_focusHoldSeconds <= 0) FocusVisibleActivity();
        }
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (Blocked() || IsPointerOverUi() || input is not InputEventMouseButton mouse) return;
        if (mouse.Pressed && mouse.ButtonIndex is MouseButton.WheelDown or MouseButton.WheelUp)
        {
            ScrollMap(mouse);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (!mouse.Pressed || mouse.ButtonIndex != MouseButton.Left) return;
        Item? item = HitItem(GetGlobalMousePosition());
        if (item == null)
        {
            _lastClick = null;
            Activity? activity = _activities.FirstOrDefault(activity => HitSprite(activity.Art, GetGlobalMousePosition(), null));
            if (activity != null) { Focus(activity, true); GetViewport().SetInputAsHandled(); }
            return;
        }
        Focus(item.Activity);
        _focusHoldSeconds = 2;
        switch (item.Activity.Kind)
        {
            case Day12ActivityKind.Instruments:
                PlayItemSound(item);
                Bounce(item);
                Burst(item);
                if (!item.Completed) Complete(item);
                break;
            case Day12ActivityKind.Balloons:
                if (Complete(item)) { Burst(item); item.Source.Hide(); }
                break;
            case Day12ActivityKind.Aquarium:
                PlayItemSound(item);
                Bounce(item);
                Bubbles(item);
                if (!item.Completed) Complete(item);
                break;
            case Day12ActivityKind.Eggs:
            case Day12ActivityKind.Gifts:
            case Day12ActivityKind.Tower:
                ulong now = Time.GetTicksMsec();
                if (mouse.DoubleClick && _lastClick == item && now - _lastClickTime <= 700)
                {
                    if (Complete(item)) { Reveal(item, true); Bounce(item); Burst(item); }
                    _lastClick = null;
                }
                else { _lastClick = item; _lastClickTime = now; }
                break;
            case Day12ActivityKind.Food:
            case Day12ActivityKind.Luggage:
                StopScroll();
                KillAnimation(item);
                _dragged = item;
                _grabOffset = item.Root.GlobalPosition - GetGlobalMousePosition();
                item.Root.ZIndex = item.HomeZIndex + 50;
                item.Source.Scale = item.SourceScale * 1.04f;
                break;
        }
        GetViewport().SetInputAsHandled();
    }

    public override void _Input(InputEvent input)
    {
        if (_dragged == null) return;
        if (Blocked()) { CancelDrag(); return; }
        if (input is InputEventMouseMotion)
        {
            Rect2 bounds = UsableViewport();
            Vector2 pointer = GetGlobalMousePosition();
            pointer = new Vector2(Mathf.Clamp(pointer.X, bounds.Position.X, bounds.End.X),
                Mathf.Clamp(pointer.Y, bounds.Position.Y, bounds.End.Y));
            _dragged.Root.Position = ((Node2D)_dragged.Root.GetParent()).ToLocal(pointer + _grabOffset);
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton mouse)
        {
            if (mouse.ButtonIndex is MouseButton.WheelDown or MouseButton.WheelUp)
                GetViewport().SetInputAsHandled();
            else if (mouse.ButtonIndex == MouseButton.Left && !mouse.Pressed)
            {
                Item item = _dragged;
                _dragged = null;
                item.Root.ZIndex = item.HomeZIndex;
                bool accepted = !IsPointerOverUi()
                    && (OverDrop(item, GetGlobalMousePosition()) || OverDrop(item, SpriteCenter(item.Source)))
                    && Complete(item);
                if (accepted) MoveToTarget(item, true);
                else MoveHome(item);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void ScrollMap(InputEventMouseButton mouse)
    {
        if (_dragged != null) return;
        float movement = mouse.ButtonIndex == MouseButton.WheelDown ? -ScrollStep : ScrollStep;
        float next = Mathf.Clamp(_targetY + movement * Mathf.Max(mouse.Factor, 0.1f), _minimumY, _maximumY);
        if (Mathf.IsEqualApprox(next, _targetY)) return;
        _lastClick = null;
        _targetY = next;
        if (IsInstanceValid(_scrollTween)) _scrollTween!.Kill();
        _scrollTween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _scrollTween.TweenProperty(_background, "position:y", _targetY, ScrollDuration);
    }

    private bool Complete(Item item)
    {
        if (item.Completed) return false;
        var definition = item.Activity.Task.Definition;
        Godot.Collections.Dictionary report = new()
        {
            ["operation"] = definition.Operation,
            ["object_id"] = definition.TargetIds[item.Index],
            ["object_type"] = definition.TargetType,
            ["object_color"] = "",
            ["object_tags"] = new string[] { definition.TargetTag },
            ["timestamp"] = (long)Time.GetTicksMsec()
        };
        if (!_tasks.TryHandleInteraction(report)) return false;
        item.Completed = true;
        // Instruments and aquarium animals also sound on repeat clicks, at the input site.
        if (item.Activity.Kind is not (Day12ActivityKind.Instruments or Day12ActivityKind.Aquarium))
            PlayItemSound(item);
        return true;
    }

    private void Focus(Activity activity, bool repeatHint = false)
    {
        if (_focusedTaskId == activity.Definition.Id && !repeatHint) return;
        _focusedTaskId = activity.Definition.Id;
        _taskPanel.FocusTask(_focusedTaskId);
        if (activity.Task.Progress < 5) _audio.PlayAutomaticTaskHint(activity.Task.Definition.HintAudioPath);
    }

    private void FocusVisibleActivity()
    {
        if (_tasks.IsDayComplete()) return;
        Rect2 visible = UsableViewport();
        Activity? next = !IsPointerOverUi() ? HitItem(GetGlobalMousePosition())?.Activity : null;
        next ??= _activities.Where(activity => activity.Task.Progress < 5
                && activity.Items.Any(item => visible.HasPoint(SpriteCenter(item.Source))))
            .OrderBy(activity => activity.Items.Min(item => SpriteCenter(item.Source).DistanceSquaredTo(visible.GetCenter())))
            .FirstOrDefault();
        if (next != null) Focus(next);
    }

    private void CelebrateActivity(string taskId)
    {
        _audio.StopTaskHint();
    }

    private void Restore(Item item)
    {
        KillAnimation(item);
        if (IsInstanceValid(item.Opening)) item.Opening!.Kill();
        item.Opening = null;
        foreach (Node2D? door in new[] { item.LeftDoor, item.RightDoor })
            if (door != null) { door.Scale = Vector2.One; door.Hide(); }
        item.Root.Position = item.Home;
        item.Root.ZIndex = item.HomeZIndex;
        item.Source.Position = item.SourcePosition;
        item.Source.Scale = item.SourceScale;
        item.Source.Rotation = item.SourceRotation;
        item.Source.Show();
        item.CurrentSprite = item.Source;
        if (item.Reveal != null)
        {
            item.Reveal.Position = item.RevealPosition;
            item.Reveal.Scale = item.RevealScale;
            item.Reveal.Rotation = item.RevealRotation;
            item.Reveal.Hide();
        }
        item.Guide?.Show();
        item.Completed = item.Activity.Task.CountedObjectIds.Contains(item.Activity.Task.Definition.TargetIds[item.Index]);
        if (!item.Completed) return;
        if (item.Activity.Kind == Day12ActivityKind.Balloons) item.Source.Hide();
        else if (item.Activity.IsDrag) MoveToTarget(item, false);
        else if (item.Reveal != null) Reveal(item);
    }

    private void ResetActivities(int dayId)
    {
        if (dayId != 12) return;
        CancelDrag();
        _lastClick = null;
        foreach (Activity activity in _activities)
            activity.Task = _tasks.Tasks.Single(task => task.Definition.Id == activity.Definition.Id);
        foreach (Item item in _items) Restore(item);
        _focusedTaskId = "";
        FocusVisibleActivity();
    }

    private void Reveal(Item item, bool animate = false)
    {
        if (item.Reveal == null) return;
        if (item.Activity.Kind is Day12ActivityKind.Eggs or Day12ActivityKind.Gifts)
        {
            // Follow the current closed sprite, including editor offsets and random variation.
            Node2D parent = (Node2D)item.Reveal.GetParent();
            item.Reveal.Position += parent.ToLocal(SpriteBottomCenter(item.Source))
                - parent.ToLocal(SpriteBottomCenter(item.Reveal));
        }
        item.Source.Hide();
        item.Reveal.Show();
        item.CurrentSprite = item.Reveal;
        if (item.LeftDoor == null || item.RightDoor == null) return;
        if (IsInstanceValid(item.Opening)) item.Opening!.Kill();
        if (!animate) { item.LeftDoor.Hide(); item.RightDoor.Hide(); return; }
        item.LeftDoor.Scale = item.RightDoor.Scale = Vector2.One;
        item.LeftDoor.Show(); item.RightDoor.Show();
        item.Opening = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        item.Opening.TweenProperty(item.LeftDoor, "scale:x", 0.0f, 0.32);
        item.Opening.TweenProperty(item.RightDoor, "scale:x", 0.0f, 0.32);
        item.Opening.Chain().TweenCallback(Callable.From(() => { item.LeftDoor.Hide(); item.RightDoor.Hide(); }));
    }

    private void MoveToTarget(Item item, bool animate)
    {
        item.Guide!.Hide();
        item.Source.Scale = item.SourceScale * PlacedScaleFactor;
        Vector2 centerOffset = SpriteCenter(item.Source) - item.Root.GlobalPosition;
        Vector2 target = ((Node2D)item.Root.GetParent()).ToLocal(item.Landing!.GlobalPosition - centerOffset);
        KillAnimation(item);
        if (!animate) { item.Root.Position = target; return; }
        item.Animation = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        item.Animation.TweenProperty(item.Root, "position", target, 0.2);
        item.Animation.TweenCallback(Callable.From(() => Burst(item)));
    }

    private bool OverDrop(Item item, Vector2 point)
    {
        CollisionShape2D shape = item.DropShape!;
        if (shape.Disabled || shape.Shape is not RectangleShape2D rectangle) return false;
        float size = Mathf.Max(0.001f, Mathf.Min(Mathf.Abs(shape.GlobalScale.X), Mathf.Abs(shape.GlobalScale.Y)));
        return new Rect2(-rectangle.Size * 0.5f, rectangle.Size).Grow(SnapTolerance / size).HasPoint(shape.ToLocal(point));
    }

    private void MoveHome(Item item)
    {
        KillAnimation(item);
        item.Source.Scale = item.SourceScale;
        item.Animation = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        item.Animation.TweenProperty(item.Root, "position", item.Home, 0.2);
    }

    private void CancelDrag()
    {
        if (_dragged == null) return;
        Item item = _dragged;
        _dragged = null;
        KillAnimation(item);
        item.Root.Position = item.Home;
        item.Root.ZIndex = item.HomeZIndex;
        item.Source.Scale = item.SourceScale;
    }

    private void Bounce(Item item)
    {
        KillAnimation(item);
        Sprite2D sprite = item.CurrentSprite;
        Vector2 home = item.CurrentSprite == item.Source ? item.SourceScale : item.RevealScale;
        Vector2 homePosition = sprite.Position;
        bool keepBottom = item.Activity.Kind is Day12ActivityKind.Eggs or Day12ActivityKind.Gifts;
        Vector2 bottom = keepBottom ? SpriteBottomCenter(sprite) : Vector2.Zero;
        sprite.Scale = home * 0.9f;
        if (keepBottom)
        {
            Node2D parent = (Node2D)sprite.GetParent();
            sprite.Position += parent.ToLocal(bottom) - parent.ToLocal(SpriteBottomCenter(sprite));
        }
        item.Animation = CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        item.Animation.TweenProperty(sprite, "scale", home, 0.35);
        if (keepBottom) item.Animation.Parallel().TweenProperty(sprite, "position", homePosition, 0.35);
    }

    private void Burst(Item item)
    {
        // Follow the sprite's live center, including editor offsets and map scrolling.
        Node2D parent = item.Activity.Art;
        Vector2 inverseScale = new(1 / Mathf.Max(0.001f, Mathf.Abs(parent.GlobalScale.X)),
            1 / Mathf.Max(0.001f, Mathf.Abs(parent.GlobalScale.Y)));
        parent.AddChild(new Day12CelebrationBits
        {
            Position = parent.ToLocal(SpriteCenter(item.CurrentSprite)), Scale = inverseScale, ZIndex = 20
        });
    }

    private void Bubbles(Item item)
    {
        Node2D parent = item.Activity.Art;
        Vector2 position = item.BubbleOrigin?.GlobalPosition ?? SpriteCenter(item.Source);
        parent.AddChild(new Day12AquariumBubbles
        {
            Position = parent.ToLocal(position), ZIndex = 20,
            Scale = new Vector2(1 / Mathf.Abs(parent.GlobalScale.X), 1 / Mathf.Abs(parent.GlobalScale.Y))
        });
    }

    private Item? HitItem(Vector2 point) => _items.AsEnumerable().Reverse().FirstOrDefault(item =>
        (!item.Completed || item.Activity.Kind is Day12ActivityKind.Instruments or Day12ActivityKind.Aquarium)
        && (item.Activity.Kind == Day12ActivityKind.Tower && item.WindowInterior != null
            ? item.Source.IsVisibleInTree() && Geometry2D.IsPointInPolygon(
                item.WindowInterior.ToLocal(point), item.WindowInterior.Polygon)
            : HitSprite(item.Source, point, item.Pixels)));

    private static bool HitSprite(Sprite2D sprite, Vector2 point, Image? pixels)
    {
        if (!sprite.IsVisibleInTree()) return false;
        Vector2 pixel = sprite.ToLocal(point) - sprite.GetRect().Position;
        int x = Mathf.FloorToInt(pixel.X), y = Mathf.FloorToInt(pixel.Y);
        if (pixels == null) return sprite.GetRect().HasPoint(sprite.ToLocal(point));
        return x >= 0 && y >= 0 && x < pixels.GetWidth() && y < pixels.GetHeight()
            && pixels.GetPixel(x, y).A > 0.12f;
    }

    private Vector2 SpriteCenter(Sprite2D sprite)
    {
        Rect2I used = VisiblePixelRect(sprite);
        // Transparent padding must not shift the landing point or celebration.
        Vector2 visibleCenter = new(used.Position.X + used.Size.X * 0.5f, used.Position.Y + used.Size.Y * 0.5f);
        return sprite.ToGlobal(sprite.GetRect().Position + visibleCenter);
    }

    private Vector2 SpriteBottomCenter(Sprite2D sprite)
    {
        Rect2I used = VisiblePixelRect(sprite);
        Vector2 bottomCenter = new(used.Position.X + used.Size.X * 0.5f, used.End.Y);
        return sprite.ToGlobal(sprite.GetRect().Position + bottomCenter);
    }

    private Rect2I VisiblePixelRect(Sprite2D sprite)
    {
        ulong id = sprite.Texture.GetInstanceId();
        if (!_visiblePixelRects.TryGetValue(id, out Rect2I used))
        {
            using Image pixels = sprite.Texture.GetImage();
            used = pixels.GetUsedRect();
            _visiblePixelRects[id] = used;
        }
        return used;
    }

    private static Rect2 SpriteBounds(Sprite2D sprite)
    {
        Rect2 rect = sprite.GetRect();
        Vector2[] corners = [rect.Position, rect.Position + new Vector2(rect.Size.X, 0),
            rect.End, rect.Position + new Vector2(0, rect.Size.Y)];
        Rect2 bounds = new(sprite.ToGlobal(corners[0]), Vector2.Zero);
        foreach (Vector2 corner in corners) bounds = bounds.Expand(sprite.ToGlobal(corner));
        return bounds;
    }

    private Rect2 UsableViewport()
    {
        Vector2 size = GetViewportRect().Size;
        float right = size.X - 20;
        if (_taskbarArt.IsVisibleInTree()) right = Mathf.Min(right, SpriteBounds(_taskbarArt).Position.X - 16);
        return new Rect2(20, 120, Mathf.Max(100, right - 20), Mathf.Max(100, size.Y - 140));
    }

    private bool IsPointerOverUi() => GetViewport().GuiGetHoveredControl() != null
        || _taskbarArt.IsVisibleInTree() && SpriteBounds(_taskbarArt).HasPoint(GetGlobalMousePosition());

    private bool Blocked() => !GetWindow().HasFocus()
        || GetNodeOrNull<Node>("SettingsOverlay") != null
        || GetNodeOrNull<Node>("GameExitOverlay") != null
        || GetNodeOrNull<Node>("ResultOverlayLayer") != null;

    private void RecalculateBounds()
    {
        StopScroll();
        _viewportSize = GetViewportRect().Size;
        float halfHeight = _background.Texture.GetHeight() * Mathf.Abs(_background.Scale.Y) * 0.5f;
        _maximumY = Mathf.Max(halfHeight, _viewportSize.Y * 0.5f);
        _minimumY = halfHeight * 2 > _viewportSize.Y ? _viewportSize.Y - halfHeight : _maximumY;
        _targetY = Mathf.Clamp(_background.Position.Y, _minimumY, _maximumY);
        _background.Position = new Vector2(_background.Position.X, _targetY);
    }

    private void StopScroll()
    {
        if (IsInstanceValid(_scrollTween)) _scrollTween!.Kill();
        _scrollTween = null;
        _targetY = _background.Position.Y;
    }

    private static void KillAnimation(Item item)
    {
        if (IsInstanceValid(item.Animation)) item.Animation!.Kill();
        item.Animation = null;
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
            _tasks.OnDayReset -= ResetActivities;
            _tasks.OnTaskCompleted -= CelebrateActivity;
        }
        if (IsInstanceValid(_audio)) _audio.StopTaskHint();
        if (IsInstanceValid(_scrollTween)) _scrollTween!.Kill();
        foreach (Item item in _items)
        {
            KillAnimation(item);
            if (IsInstanceValid(item.Opening)) item.Opening!.Kill();
            item.Pixels.Dispose();
        }
    }

    private sealed class Activity(Sprite2D art, Day12ActivityDefinition definition, TaskRuntimeState task)
    {
        public Sprite2D Art { get; } = art;
        public Day12ActivityDefinition Definition { get; } = definition;
        public TaskRuntimeState Task { get; set; } = task;
        public Day12ActivityKind Kind => Definition.Kind;
        public bool IsDrag => Kind is Day12ActivityKind.Food or Day12ActivityKind.Luggage;
        public List<Item> Items { get; } = [];
    }

    private sealed class Item(Activity activity, int index, Node2D root, Sprite2D source, Sprite2D? reveal, float phase)
    {
        public Activity Activity { get; } = activity;
        public int Index { get; } = index;
        public Node2D Root { get; } = root;
        public Sprite2D Source { get; } = source;
        public Sprite2D? Reveal { get; } = reveal;
        public Sprite2D CurrentSprite { get; set; } = source;
        public Vector2 Home { get; } = root.Position;
        public int HomeZIndex { get; } = root.ZIndex;
        public Vector2 SourcePosition { get; } = source.Position;
        public Vector2 SourceScale { get; } = source.Scale;
        public float SourceRotation { get; } = source.Rotation;
        public Vector2 RevealPosition { get; } = reveal?.Position ?? Vector2.Zero;
        public Vector2 RevealScale { get; } = reveal?.Scale ?? Vector2.One;
        public float RevealRotation { get; } = reveal?.Rotation ?? 0;
        public float Phase { get; } = phase;
        public Image Pixels { get; } = source.Texture.GetImage();
        public CollisionShape2D? DropShape { get; set; }
        public Marker2D? Landing { get; set; }
        public Sprite2D? Guide { get; set; }
        public Tween? Animation { get; set; }
        public Tween? Opening { get; set; }
        public Node2D? LeftDoor { get; set; }
        public Node2D? RightDoor { get; set; }
        public Polygon2D? WindowInterior { get; set; }
        public Marker2D? BubbleOrigin { get; set; }
        public bool Completed { get; set; }
    }
}

