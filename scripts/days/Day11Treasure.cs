#nullable enable annotations

using Godot;
using NewGameProject.Tasks;
using NewGameProject.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewGameProject.Days;

public partial class Day11Treasure : Node2D
{
    private const string AssetRoot = "res://assets/images/objects/day11/";
    private const string MusicPath = "res://assets/audio/voice/day11/day11_bgm.mp3";
    private const string ChestSoundPath = "res://assets/audio/voice/day9/box_open.mp3";
    private const string DigSoundPath = "res://assets/audio/voice/day12/dig_soil.mp3";
    private const string DigHintPath = "res://assets/audio/voice/task_hints/day11/dig_treasure_map_hint.wav";
    private const string ChestHintPath = "res://assets/audio/voice/task_hints/day11/dug_chest_hint.wav";
    private const string ScrollReadHintPath = "res://assets/audio/voice/task_hints/day11/read_scroll_hint.wav";
    private static readonly Dictionary<string, string> InteractionSoundPaths = new()
    {
        ["open_clues"] = "res://assets/audio/voice/day6/paper_rustle.mp3",
        ["move_rocks"] = "res://assets/audio/voice/day11/rock_move.mp3",
        ["store_gems"] = "res://assets/audio/voice/day11/gem_sparkle.mp3",
        ["move_leaf_piles"] = "res://assets/audio/voice/day4/leaf_rustle.mp3",
        ["collect_coins"] = "res://assets/audio/voice/day11/coin_collect.mp3",
        ["open_bottles"] = "res://assets/audio/voice/day11/bottle_open.mp3",
        ["collect_map_fragments"] = "res://assets/audio/voice/day6/paper_rustle.mp3",
        ["assemble_map"] = "res://assets/audio/voice/generic_soft.mp3"
    };
    private static readonly string[] GroupIds =
        ["open_clues", "move_rocks", "store_gems", "move_leaf_piles", "collect_coins", "open_bottles", "collect_map_fragments", "assemble_map"];
    private static readonly string[] Colors = ["red", "blue", "yellow", "green", "purple"];
    private static readonly string[][] AssetNames =
    [
        ["scroll_red_closed", "scroll_blue_closed", "scroll_yellow_closed", "scroll_green_closed", "scroll_purple_closed"],
        ["rock_moss_round", "rock_white_stripe", "rock_twin_peak", "rock_flat_oval", "rock_pink_crystal"],
        ["gem_red", "gem_blue", "gem_yellow", "gem_green", "gem_purple"],
        ["leaf_pile_orange_maple", "leaf_pile_yellow_oak", "leaf_pile_red_maple", "leaf_pile_amber_mixed", "leaf_pile_green_gold"],
        ["coin_leaf", "coin_flower", "coin_sun", "coin_wave", "coin_star"],
        ["bottle_red_closed", "bottle_blue_closed", "bottle_yellow_closed", "bottle_green_closed", "bottle_purple_closed"],
        ["map_fragment_01", "map_fragment_02", "map_fragment_03", "map_fragment_04", "map_fragment_05"],
        ["map_fragment_01", "map_fragment_02", "map_fragment_03", "map_fragment_04", "map_fragment_05"]
    ];
    private static readonly string[] SceneGroups = ["Scrolls", "Rocks", "Gems", "LeafPiles", "Coins", "Bottles", "MapFragments"];

    [Export(PropertyHint.Range, "50,400,10")] public float ScrollStep { get; set; } = 220;
    [Export(PropertyHint.Range, "0.05,0.5,0.01")] public float ScrollDuration { get; set; } = 0.16f;
    [Export(PropertyHint.Range, "30,140,5")] public float SnapDistance { get; set; } = 85;
    [Export(PropertyHint.Range, "40,180,5")] public float LeafDragDistance { get; set; } = 90;
    [Export(PropertyHint.Range, "40,180,5")] public float RockDragDistance { get; set; } = 90;
    [Export(PropertyHint.Range, "180,400,10")] public float CoverMaxDragDistance { get; set; } = 220;
    [Export(PropertyHint.Range, "40,180,5")] public float DigSnapDistance { get; set; } = 100;
    [Export(PropertyHint.Range, "220,800,10")] public float ShovelMaxDragDistance { get; set; } = 420;
    [Export(PropertyHint.Range, "0.1,0.8,0.05")] public float ActionDuration { get; set; } = 0.35f;
    [Export(PropertyHint.Range, "0,0.12,0.01")] public float SizeVariation { get; set; } = 0.05f;
    [Export(PropertyHint.Range, "0,8,0.5")] public float TiltVariationDegrees { get; set; } = 3;
    [Export] public Vector2 BottleDriftRange { get; set; } = new(8, 4);
    [Export(PropertyHint.Range, "2,10,0.1")] public float BottleDriftPeriod { get; set; } = 4;
    [Export(PropertyHint.Range, "0,5,0.5")] public float BottleRockDegrees { get; set; } = 2;

    private readonly Dictionary<string, List<TreasureItem>> _groups = new();
    private readonly Dictionary<string, AudioStream> _interactionSounds = new();
    private AudioStreamPlayer _musicPlayer = null!;
    private AudioStream? _chestSound;
    private AudioStream? _digSound;
    private readonly Dictionary<Texture2D, Image> _pixels = new();
    private readonly List<Tween> _animations = [];
    private readonly RandomNumberGenerator _random = new();
    private TaskManager _tasks = null!;
    private AudioManager _audio = null!;
    private TaskPanel _taskPanel = null!;
    private Sprite2D _background = null!;
    private Sprite2D _chest = null!;
    private Texture2D _chestClosed = null!;
    private Texture2D _chestOpen = null!;
    private Vector2 _chestScale;
    private Sprite2D _shovel = null!;
    private Marker2D _shovelTip = null!;
    private Marker2D _digTarget = null!;
    private Sprite2D _digPit = null!;
    private Node2D _shovelHomeParent = null!;
    private Transform2D _shovelHomeTransform;
    private int _shovelHomeZ;
    private Vector2 _pitHomeScale;
    private Color _pitHomeColor;
    private bool _shovelDragging;
    private bool _treasureDug;
    private Tween? _digTween;
    private Node2D _gemTray = null!;
    private Node2D _collectedGems = null!;
    private Node2D _dragRoot = null!;
    private Node2D _puzzle = null!;
    private ColorRect _puzzleBackdrop = null!;
    private Sprite2D _puzzleBoard = null!;
    private Node2D _puzzleMiniAnchor = null!;
    private Vector2 _puzzleHomePosition;
    private Vector2 _puzzleHomeScale;
    private bool _puzzleExpanded;
    private bool _puzzleTransitioning;
    private bool _autoOpenPuzzlePending;
    private Tween? _puzzleTween;
    private TreasureItem? _flyingFragment;
    private Control _clueViewer = null!;
    private TextureRect _cluePicture = null!;
    private ColorRect _clueBackdrop = null!;
    private Vector2 _clueHomePosition;
    private Vector2 _clueHomeScale;
    private Color _clueHomeColor;
    private Color _clueBackdropColor;
    private Vector2 _clueSourcePosition;
    private Vector2 _clueSourceScale;
    private float _clueSourceRotation;
    private TreasureItem? _viewedClue;
    private Tween? _clueTween;
    private bool _clueClosing;
    private TreasureItem? _dragged;
    private Vector2 _grabOffset;
    private Tween? _scrollTween;
    private float _targetY;
    private float _fixedX;
    private string _focusedGroup = "";
    private bool _busy;
    private bool _rewardReady;
    private bool _rewardOpened;
    private double _puzzleCloseCountdown = -1;
    private float _bottleDriftTime;

    public override void _EnterTree() => GetNode<Node>("/root/GameState").Call("set_selected_day", 11);

    public override void _Ready()
    {
        _random.Randomize();
        _tasks = GetNode<TaskManager>("/root/TaskManager");
        _audio = GetNode<AudioManager>("/root/AudioManager");
        _taskPanel = GetNode<TaskPanel>("Taskbar/TaskPanel");
        _taskPanel.AutoShowResultOnCompletion = false;
        _background = GetNode<Sprite2D>("Day11Bg");
        // Keep the editor-authored position/size while attaching the friend to the scrolling map.
        Sprite2D friend = GetNode<Sprite2D>("friend");
        friend.Reparent(_background, true);
        friend.ZAsRelative = false;
        _chest = GetNode<Sprite2D>("Day11Bg/TreasureObjects/FinalChest");
        _chestClosed = _chest.Texture;
        _chestOpen = LoadTexture("treasure_chest_open");
        _chestScale = _chest.Scale;
        _shovel = GetNode<Sprite2D>("Day11Bg/TreasureObjects/Shovel");
        _shovelTip = _shovel.GetNode<Marker2D>("BladeTip");
        _shovelHomeParent = _shovel.GetParent<Node2D>();
        _shovelHomeTransform = _shovel.Transform;
        _shovelHomeZ = _shovel.ZIndex;
        _digTarget = GetNode<Marker2D>("Day11Bg/TreasureObjects/DigSite/DigTarget");
        _digPit = GetNode<Sprite2D>("Day11Bg/TreasureObjects/DigSite/Pit");
        _pitHomeScale = _digPit.Scale;
        _pitHomeColor = _digPit.Modulate;
        _gemTray = GetNode<Node2D>("AdventureHud/GemTray");
        _collectedGems = GetNode<Node2D>("AdventureHud/GemTray/CollectedGems");
        // A world sprite's ZIndex cannot place it above the HUD's CanvasLayer.
        CanvasLayer dragLayer = new() { Name = "TreasureDragLayer", Layer = 10 };
        AddChild(dragLayer);
        _dragRoot = new Node2D { Name = "DraggedItems" };
        dragLayer.AddChild(_dragRoot);
        _puzzle = GetNode<Node2D>("AdventureHud/MapPuzzle");
        _puzzleBackdrop = GetNode<ColorRect>("AdventureHud/MapPuzzle/Backdrop");
        _puzzleBoard = GetNode<Sprite2D>("AdventureHud/MapPuzzle/Board");
        _puzzleMiniAnchor = GetNode<Node2D>("AdventureHud/PuzzleMiniAnchor");
        // This scene-authored preview makes the collapsed panel easy to place in the editor.
        _puzzleMiniAnchor.GetNode<Node2D>("EditorPreview").Hide();
        _puzzleHomePosition = _puzzle.Position;
        _puzzleHomeScale = _puzzle.Scale;
        _clueViewer = GetNode<Control>("ClueLayer/ClueViewer");
        _cluePicture = GetNode<TextureRect>("ClueLayer/ClueViewer/CluePicture");
        _clueBackdrop = GetNode<ColorRect>("ClueLayer/ClueViewer/Backdrop");
        _clueHomePosition = _cluePicture.Position;
        _clueHomeScale = _cluePicture.Scale;
        _clueHomeColor = _clueViewer.Modulate;
        _clueBackdropColor = _clueBackdrop.Modulate;
        if (!_tasks.LoadDay(11)) return;
        try { ReadItems(); }
        catch (InvalidOperationException error)
        {
            GD.PushError($"Day11: {error.Message}");
            SetProcess(false);
            SetProcessInput(false);
            SetProcessUnhandledInput(false);
            return;
        }
        ConfigureAudio();
        _tasks.OnAllTasksCompleted += AllTasksCompleted;
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
            _tasks.OnAllTasksCompleted -= AllTasksCompleted;
            _tasks.OnDayReset -= ResetDay;
        }
        StopAnimations();
        if (IsInstanceValid(_audio)) _audio.StopTaskHint();
        foreach (Image image in _pixels.Values) image.Dispose();
        _pixels.Clear();
    }

    private void ConfigureAudio()
    {
        foreach ((string groupId, string path) in InteractionSoundPaths)
        {
            if (!ResourceLoader.Exists(path)) continue;
            AudioStream? sound = GD.Load<AudioStream>(path);
            if (sound != null) _interactionSounds[groupId] = sound;
        }
        if (ResourceLoader.Exists(ChestSoundPath)) _chestSound = GD.Load<AudioStream>(ChestSoundPath);
        if (ResourceLoader.Exists(DigSoundPath)) _digSound = GD.Load<AudioStream>(DigSoundPath);
        AudioStreamMP3? source = GD.Load<AudioStreamMP3>(MusicPath);
        if (source == null) return;
        AudioStreamMP3 music = (AudioStreamMP3)source.Duplicate();
        music.Loop = true;
        _musicPlayer = new AudioStreamPlayer { Name = "Day11MusicPlayer", Bus = "Music", Stream = music };
        AddChild(_musicPlayer);
        _audio.SetBackgroundMusicPaused(true);
        _musicPlayer.Play();
    }

    public override void _Process(double delta)
    {
        if (HasBlockingOverlay() || !GetWindow().HasFocus())
        {
            CancelDrag();
            CancelShovelDrag();
            if (IsInstanceValid(_scrollTween)) _scrollTween!.Pause();
            if (IsInstanceValid(_clueTween)) _clueTween!.Pause();
            if (IsInstanceValid(_puzzleTween)) _puzzleTween!.Pause();
            if (IsInstanceValid(_digTween)) _digTween!.Pause();
            _audio.StopTaskHint();
            return;
        }
        if (IsInstanceValid(_scrollTween) && _scrollTween!.IsValid() && !_scrollTween.IsRunning()) _scrollTween.Play();
        if (IsInstanceValid(_clueTween) && _clueTween!.IsValid() && !_clueTween.IsRunning()) _clueTween.Play();
        if (IsInstanceValid(_puzzleTween) && _puzzleTween!.IsValid() && !_puzzleTween.IsRunning()) _puzzleTween.Play();
        if (IsInstanceValid(_digTween) && _digTween!.IsValid() && !_digTween.IsRunning()) _digTween.Play();
        if (_dragged != null && !Input.IsMouseButtonPressed(MouseButton.Left)) CancelDrag();
        if (_shovelDragging && !Input.IsMouseButtonPressed(MouseButton.Left)) CancelShovelDrag();
        if (!_clueViewer.Visible) UpdateBottleDrift(delta);
        if (_busy || _clueViewer.Visible || _puzzleTransitioning) return;
        if (_autoOpenPuzzlePending) OpenPuzzle();
        if (_puzzleExpanded && _puzzleCloseCountdown >= 0)
        {
            _puzzleCloseCountdown -= delta;
            if (_puzzleCloseCountdown <= 0) ClosePuzzle();
        }
    }

    public override void _Input(InputEvent input)
    {
        if (_clueViewer.Visible)
        {
            if (!_clueClosing && !HasBlockingOverlay() && GetWindow().HasFocus()
                && input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }
                && !HitCluePicture()) CloseClue();
            // Consume the dismissal click and wheel events so they cannot affect the map underneath.
            if (input is InputEventMouseButton or InputEventMouseMotion) GetViewport().SetInputAsHandled();
            return;
        }
        if (_shovelDragging)
        {
            if (HasBlockingOverlay() || !GetWindow().HasFocus()) { CancelShovelDrag(); return; }
            if (input is InputEventMouseMotion)
            {
                MoveShovel();
                GetViewport().SetInputAsHandled();
            }
            else if (input is InputEventMouseButton shovelButton)
            {
                if (shovelButton.ButtonIndex == MouseButton.Left && !shovelButton.Pressed)
                {
                    MoveShovel();
                    _shovelDragging = false;
                    if (_shovelTip.GlobalPosition.DistanceTo(_digTarget.GlobalPosition) <= DigSnapDistance)
                        DigTreasure();
                    else ReturnShovel();
                }
                // Dragging the tool must not scroll or activate the map underneath.
                GetViewport().SetInputAsHandled();
            }
            return;
        }
        if (_dragged == null) return;
        if (HasBlockingOverlay() || !GetWindow().HasFocus()) { CancelDrag(); return; }
        if (input is InputEventMouseMotion)
        {
            MoveDragged();
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton button)
        {
            if (button.ButtonIndex is MouseButton.WheelDown or MouseButton.WheelUp)
            {
                // Keep the source object still while dragging to a fixed collection tray.
                GetViewport().SetInputAsHandled();
            }
            else if (button.ButtonIndex == MouseButton.Left && !button.Pressed)
            {
                MoveDragged();
                TreasureItem item = _dragged;
                _dragged = null;
                if (ValidDrop(item)) CompleteItem(item);
                else ReturnItem(item);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (HasBlockingOverlay() || !GetWindow().HasFocus() || _clueViewer.Visible || _puzzleTransitioning) return;
        if (input is not InputEventMouseButton { Pressed: true } button) return;
        if (button.ButtonIndex is MouseButton.WheelDown or MouseButton.WheelUp)
        {
            if (!_busy && !_puzzleExpanded) ScrollMap(button);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (button.ButtonIndex != MouseButton.Left || _busy) return;
        Vector2 mouse = GetGlobalMousePosition();
        if (!_puzzleExpanded && _puzzleBackdrop.GetRect().HasPoint(_puzzle.ToLocal(mouse)))
        {
            OpenPuzzle();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_puzzleExpanded && _dragged == null && !HitSprite(_puzzleBoard, mouse)
            && !_groups[GroupIds[7]].Any(item => HitSprite(item.Sprite, mouse)))
        {
            ClosePuzzle();
            // Consume this click so it cannot activate a map object after collapsing.
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_rewardReady)
        {
            if (!_rewardOpened && !_puzzleExpanded && !_treasureDug && HitSprite(_shovel, mouse))
            {
                StopScroll();
                _shovelDragging = true;
                _grabOffset = _shovel.GlobalPosition - mouse;
                _shovel.Reparent(_dragRoot, true);
                _shovel.ZIndex = 30;
                _audio.PlayUiClick();
                GetViewport().SetInputAsHandled();
            }
            else if (!_rewardOpened && !_puzzleExpanded && _treasureDug && HitSprite(_chest, mouse))
            {
                if (button.DoubleClick) OpenFinalChest();
                GetViewport().SetInputAsHandled();
            }
            return;
        }
        // Previously opened scrolls remain readable throughout the search.
        foreach (TreasureItem clue in _groups[GroupIds[0]])
        {
            if (_puzzleExpanded || !clue.Done || !HitSprite(clue.Sprite, mouse)) continue;
            ViewClue(clue);
            GetViewport().SetInputAsHandled();
            return;
        }
        // Each visible object can be used as soon as its own prerequisite is met.
        foreach (TreasureItem item in _groups.Values.SelectMany(items => items)
            .Where(CanUseItem).OrderByDescending(item => item.Sprite.ZIndex))
        {
            if (!HitSprite(item.Sprite, mouse) || !_tasks.CanInteract(Report(item))) continue;
            FocusGroup(item.Definition.Id);
            if (item.Definition.Operation == "double_click" && !button.DoubleClick)
            {
                GetViewport().SetInputAsHandled();
                return;
            }
            if (item.Definition.Operation == "drag")
            {
                StopScroll();
                _dragged = item;
                _grabOffset = item.Sprite.GlobalPosition - mouse;
                if (item.GroupIndex == 2) item.Sprite.Reparent(_dragRoot, true);
                item.Sprite.ZIndex = 30;
                _audio.PlayUiClick();
            }
            else CompleteItem(item);
            GetViewport().SetInputAsHandled();
            return;
        }
    }

    private void ReadItems()
    {
        if (_tasks.Tasks.Count != 8) throw new InvalidOperationException("Expected eight task groups.");
        for (int groupIndex = 0; groupIndex < GroupIds.Length; groupIndex++)
        {
            TaskDefinition definition = _tasks.Tasks.Single(t => t.Definition.Id == GroupIds[groupIndex]).Definition;
            if (definition.TargetCount != 5 || definition.TargetIds.Count != 5)
                throw new InvalidOperationException($"{definition.Id} must define five targets.");
            List<TreasureItem> items = [];
            _groups.Add(definition.Id, items);
            for (int index = 0; index < 5; index++)
            {
                string path = groupIndex == 7
                    ? $"AdventureHud/MapPuzzle/Pieces/{AssetNames[groupIndex][index]}"
                    : $"Day11Bg/TreasureObjects/{SceneGroups[groupIndex]}/{AssetNames[groupIndex][index]}";
                Sprite2D sprite = GetNodeOrNull<Sprite2D>(path)
                    ?? throw new InvalidOperationException($"Missing scene node: {path}");
                // Keep puzzle geometry exact; vary the natural field objects slightly.
                if (groupIndex < 6)
                {
                    sprite.Scale *= _random.RandfRange(1-SizeVariation,1+SizeVariation);
                    sprite.RotationDegrees += _random.RandfRange(-TiltVariationDegrees,TiltVariationDegrees);
                    float brightness = _random.RandfRange(0.96f,1f);
                    sprite.Modulate = new Color(brightness,brightness,brightness);
                }
                TreasureItem item = new(definition, definition.TargetIds[index],groupIndex,index,sprite);
                items.Add(item);
                if (groupIndex == 5)
                {
                    item.DriftPhase = _random.RandfRange(0, Mathf.Tau);
                    item.DriftSpeed = _random.RandfRange(0.85f, 1.15f);
                }
                if (groupIndex is 0 or 5)
                    item.OpenTexture = LoadTexture(AssetNames[groupIndex][index].Replace("_closed","_open"));
                if (groupIndex is 2 or 7)
                {
                    string targetPath = groupIndex == 2
                        ? $"AdventureHud/GemTray/Slots/gem_{Colors[index]}"
                        : $"AdventureHud/MapPuzzle/Targets/map_fragment_{index+1:00}";
                    item.Target = GetNode<Sprite2D>(targetPath);
                }
            }
        }
        // Match the authored layout: the coin order need not match the leaf-pile order.
        List<TreasureItem> remainingLeaves = new(_groups[GroupIds[3]]);
        foreach (TreasureItem coin in _groups[GroupIds[4]])
        {
            TreasureItem cover = remainingLeaves.MinBy(leaf =>
                leaf.Sprite.GlobalPosition.DistanceSquaredTo(coin.Sprite.GlobalPosition))!;
            coin.Cover = cover;
            remainingLeaves.Remove(cover);
        }
    }

    private bool CanUseItem(TreasureItem item)
    {
        if (item.Done || (_puzzleExpanded && item.GroupIndex != 7)) return false;
        return item.GroupIndex switch
        {
            2 => _groups[GroupIds[1]][item.Index].Done,
            4 => item.Cover!.Done,
            6 => _groups[GroupIds[5]][item.Index].Done,
            7 => _puzzleExpanded && _groups[GroupIds[6]].All(fragment => fragment.Done),
            _ => true
        };
    }

    private void FocusGroup(string id)
    {
        bool changed = _focusedGroup != id;
        _focusedGroup = id;
        _taskPanel.FocusTask(id);
        if (changed && _tasks.GetTaskState(id) == "Active")
            _audio.PlayAutomaticTaskHint(_groups[id][0].Definition.HintAudioPath);
    }

    private void RefreshItems()
    {
        _gemTray.Visible = !_puzzleExpanded;
        foreach (List<TreasureItem> items in _groups.Values)
            foreach (TreasureItem item in items)
            {
                item.Sprite.Visible = item.GroupIndex switch
                {
                    0 => true,
                    1 => true,
                    2 => true,
                    3 => true,
                    4 => !item.Done,
                    5 => true,
                    6 => _groups[GroupIds[5]][item.Index].Done && !item.Done,
                    7 => _groups[GroupIds[6]][item.Index].Done && _flyingFragment?.Index != item.Index,
                    _ => false
                };
                if (item.Target != null) item.Target.Visible = !item.Done;
            }
        _viewedClue?.Sprite.Hide();
    }

    private void OpenPuzzle()
    {
        if (_busy || _dragged != null || _shovelDragging || _clueViewer.Visible || _puzzleExpanded || _puzzleTransitioning
            || HasBlockingOverlay() || !GetWindow().HasFocus()
            || !_groups[GroupIds[6]].All(fragment => fragment.Done)) return;
        StopScroll();
        _autoOpenPuzzlePending = false;
        _puzzleExpanded = true;
        AnimatePuzzle(_puzzleHomePosition, _puzzleHomeScale);
        RefreshItems();
        if (!_rewardReady) FocusGroup(GroupIds[7]);
    }

    private void ClosePuzzle()
    {
        if (_busy || _dragged != null || _shovelDragging || _puzzleTransitioning || !_puzzleExpanded) return;
        _puzzleExpanded = false;
        AnimatePuzzle(_puzzleMiniAnchor.Position, _puzzleMiniAnchor.Scale);
        _puzzleCloseCountdown = -1;
        RefreshItems();
        if (_rewardReady) return;
        string next = _groups.Values.SelectMany(items => items)
            .FirstOrDefault(item => CanUseItem(item))?.Definition.Id ?? GroupIds[7];
        FocusGroup(next);
    }

    private void AnimatePuzzle(Vector2 position, Vector2 scale)
    {
        if (IsInstanceValid(_puzzleTween)) _puzzleTween!.Kill();
        _puzzleTransitioning = true;
        _puzzleTween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _puzzleTween.TweenProperty(_puzzle, "position", position, 0.3);
        _puzzleTween.TweenProperty(_puzzle, "scale", scale, 0.3);
        _puzzleTween.Chain().TweenCallback(Callable.From(() => _puzzleTransitioning = false));
    }

    private void CompleteItem(TreasureItem item)
    {
        _busy = true;
        StopScroll();
        if (!_tasks.TryHandleInteraction(Report(item))) { ReturnItem(item); return; }
        item.Done = true;
        item.Target?.Hide();
        if (_interactionSounds.TryGetValue(item.Definition.Id, out AudioStream? sound))
            _audio.PlayEffect(sound);
        else
            _audio.PlayDefaultInteractionEffect();
        Tween tween = NewAnimation();
        switch (item.GroupIndex)
        {
            case 0:
                item.Sprite.Texture = item.OpenTexture;
                tween.TweenInterval(ActionDuration);
                ViewClue(item);
                break;
            case 1:
                tween.TweenInterval(ActionDuration);
                break;
            case 2:
                tween.SetParallel();
                tween.TweenProperty(item.Sprite,"global_position",item.Target!.GlobalPosition,ActionDuration);
                tween.TweenProperty(item.Sprite,"global_scale",item.Target.GlobalScale,ActionDuration);
                tween.TweenProperty(item.Sprite,"global_rotation",0,ActionDuration);
                break;
            case 3:
                tween.TweenInterval(ActionDuration);
                break;
            case 4:
                tween.SetParallel();
                tween.TweenProperty(item.Sprite,"scale",item.HomeScale*0.2f,ActionDuration);
                tween.TweenProperty(item.Sprite,"modulate:a",0,ActionDuration);
                tween.Chain().TweenCallback(Callable.From(item.Sprite.Hide));
                break;
            case 6:
                _flyingFragment = item;
                TreasureItem puzzlePiece = _groups[GroupIds[7]][item.Index];
                puzzlePiece.Sprite.Hide();
                item.Sprite.Reparent(_dragRoot, true);
                item.Sprite.ZIndex = 30;
                tween.SetParallel();
                tween.TweenProperty(item.Sprite, "global_position", puzzlePiece.Sprite.GlobalPosition, 0.5);
                tween.TweenProperty(item.Sprite, "global_scale", puzzlePiece.Sprite.GlobalScale, 0.5);
                tween.TweenProperty(item.Sprite, "global_rotation", puzzlePiece.Sprite.GlobalRotation, 0.5);
                break;
            case 5:
                item.Sprite.Texture = item.OpenTexture;
                TreasureItem fragment = _groups[GroupIds[6]][item.Index];
                fragment.Sprite.Show();
                tween.TweenInterval(ActionDuration);
                break;
            case 7:
                tween.SetParallel();
                tween.TweenProperty(item.Sprite,"global_position",item.Target!.GlobalPosition,ActionDuration);
                tween.TweenProperty(item.Sprite,"global_scale",item.Target.GlobalScale,ActionDuration);
                tween.TweenProperty(item.Sprite,"global_rotation",0,ActionDuration);
                break;
        }
        tween.Chain().TweenCallback(Callable.From(() =>
        {
            if (item.GroupIndex == 2) item.Sprite.Reparent(_collectedGems, true);
            if (item.GroupIndex == 6)
            {
                item.Sprite.Hide();
                _flyingFragment = null;
            }
            item.Sprite.ZIndex = item.HomeZ;
            _busy = false;
            RefreshItems();
            if (_rewardReady) return;
            // Guide the next action at this same location, without waiting for a whole group.
            int nextGroup = item.GroupIndex switch { 1 => 2, 3 => 4, 5 => 6, _ => -1 };
            TreasureItem? revealed = nextGroup < 0 ? null : item.GroupIndex == 3
                ? _groups[GroupIds[4]].Single(coin => coin.Cover == item)
                : _groups[GroupIds[nextGroup]][item.Index];
            if (revealed != null && !revealed.Done)
                FocusGroup(GroupIds[nextGroup]);
            if (item.GroupIndex == 6 && _groups[GroupIds[6]].All(fragment => fragment.Done))
            {
                _autoOpenPuzzlePending = true;
                OpenPuzzle();
            }
            if (item.GroupIndex == 7 && _groups[GroupIds[7]].All(piece => piece.Done))
            {
                _puzzleCloseCountdown = 1.5;
            }
        }));
    }

    private void ReturnItem(TreasureItem item)
    {
        _busy = true;
        Transform2D home = item.HomeParent.GlobalTransform * item.HomeTransform;
        Tween tween = NewAnimation().SetParallel();
        tween.TweenProperty(item.Sprite,"global_position",home.Origin,ActionDuration);
        tween.TweenProperty(item.Sprite,"global_scale",home.Scale,ActionDuration);
        tween.TweenProperty(item.Sprite,"global_rotation",home.Rotation,ActionDuration);
        tween.Chain().TweenCallback(Callable.From(() =>
        {
            if (item.Sprite.GetParent() != item.HomeParent) item.Sprite.Reparent(item.HomeParent, false);
            item.Sprite.Transform = item.HomeTransform;
            item.Sprite.ZIndex = item.HomeZ;
            _busy = false;
        }));
    }

    private bool ValidDrop(TreasureItem item)
    {
        if (item.GroupIndex is 1 or 3)
            return item.Sprite.GlobalPosition.DistanceTo(item.HomeParent.ToGlobal(item.HomePosition))
                >= (item.GroupIndex == 1 ? RockDragDistance : LeafDragDistance);
        TreasureItem? nearest = null;
        float distance = SnapDistance;
        foreach (TreasureItem candidate in _groups[item.Definition.Id])
        {
            if (candidate.Done || candidate.Target == null || !candidate.Target.IsVisibleInTree()) continue;
            float current = item.Sprite.GlobalPosition.DistanceTo(candidate.Target.GlobalPosition);
            if (current >= distance) continue;
            nearest = candidate;
            distance = current;
        }
        return nearest == item;
    }

    private void MoveDragged()
    {
        if (_dragged == null) return;
        Vector2 mouse = GetGlobalMousePosition()+_grabOffset;
        if (_dragged.GroupIndex is 1 or 3)
        {
            Vector2 home = _dragged.HomeParent.ToGlobal(_dragged.HomePosition);
            float minimum = _dragged.GroupIndex == 1 ? RockDragDistance : LeafDragDistance;
            mouse = home + (mouse - home).LimitLength(Mathf.Max(CoverMaxDragDistance, minimum));
        }
        Vector2 size = GetViewportRect().Size;
        Vector2 half = _dragged.Sprite.Texture.GetSize()*_dragged.Sprite.GlobalScale.Abs()*0.5f;
        _dragged.Sprite.GlobalPosition = new Vector2(
            Mathf.Clamp(mouse.X,half.X,size.X-half.X),Mathf.Clamp(mouse.Y,half.Y,size.Y-half.Y));
    }

    private void CancelDrag()
    {
        if (_dragged == null) return;
        TreasureItem item = _dragged;
        _dragged = null;
        ReturnItem(item);
    }

    private bool HitSprite(Sprite2D sprite, Vector2 mouse)
    {
        if (!sprite.IsVisibleInTree() || sprite.Texture == null) return false;
        Vector2 point = sprite.ToLocal(mouse);
        if (!sprite.GetRect().HasPoint(point)) return false;
        return HasTexturePixel(sprite.Texture, point - sprite.GetRect().Position);
    }

    private bool HitCluePicture()
    {
        Texture2D texture = _cluePicture.Texture;
        Vector2 textureSize = texture.GetSize();
        float fit = Mathf.Min(_cluePicture.Size.X / textureSize.X, _cluePicture.Size.Y / textureSize.Y);
        Vector2 displayedSize = textureSize * fit;
        Rect2 displayedRect = new((_cluePicture.Size - displayedSize) * 0.5f, displayedSize);
        Vector2 point = _cluePicture.GetLocalMousePosition();
        return displayedRect.HasPoint(point) && HasTexturePixel(texture, (point - displayedRect.Position) / fit);
    }

    private bool HasTexturePixel(Texture2D texture, Vector2 uv)
    {
        if (!_pixels.TryGetValue(texture,out Image? pixels))
        {
            pixels = texture.GetImage();
            if (pixels == null) return false;
            _pixels.Add(texture,pixels);
        }
        // AtlasTexture.GetImage() returns the cropped region without the display margin.
        // Convert the displayed pixel into region coordinates before sampling its alpha.
        if (texture is AtlasTexture atlas) uv -= atlas.Margin.Position;
        int x = (int)Math.Floor(uv.X), y = (int)Math.Floor(uv.Y);
        return x>=0 && y>=0 && x<pixels.GetWidth() && y<pixels.GetHeight() && pixels.GetPixel(x,y).A>0.12f;
    }

    private void ViewClue(TreasureItem item)
    {
        StopScroll();
        if (!_rewardReady) FocusGroup(item.Definition.Id);
        _audio.StopTaskHint();
        if (IsInstanceValid(_clueTween)) _clueTween!.Kill();
        _clueClosing = false;
        _cluePicture.Texture = item.OpenTexture;
        _cluePicture.PivotOffset = _cluePicture.Size * 0.5f;
        Transform2D viewportToViewer = _clueViewer.GetGlobalTransformWithCanvas().AffineInverse();
        Transform2D sourceToViewer = viewportToViewer * item.Sprite.GetGlobalTransformWithCanvas();
        Vector2 textureSize = _cluePicture.Texture.GetSize();
        float fit = Mathf.Min(_cluePicture.Size.X / textureSize.X, _cluePicture.Size.Y / textureSize.Y);
        // Match the map scroll's current position and size, including the background's scrolling transform.
        _clueSourcePosition = sourceToViewer * item.Sprite.GetRect().GetCenter() - _cluePicture.PivotOffset;
        _clueSourceScale = sourceToViewer.Scale / fit;
        _clueSourceRotation = sourceToViewer.Rotation;
        Vector2 centerPosition = viewportToViewer * GetViewportRect().GetCenter() - _cluePicture.PivotOffset;
        _cluePicture.Position = _clueSourcePosition;
        _cluePicture.Scale = _clueSourceScale;
        _cluePicture.Rotation = _clueSourceRotation;
        _clueViewer.Modulate = _clueHomeColor;
        _clueBackdrop.Modulate = new Color(_clueBackdropColor, 0);
        _viewedClue = item;
        item.Sprite.Hide();
        _clueViewer.Show();
        _clueTween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _clueTween.TweenProperty(_cluePicture, "position", centerPosition, 0.32);
        _clueTween.TweenProperty(_cluePicture, "scale", _clueHomeScale, 0.32);
        _clueTween.TweenProperty(_cluePicture, "rotation", 0, 0.32);
        _clueTween.TweenProperty(_clueBackdrop, "modulate:a", _clueBackdropColor.A, 0.32);
        // Explain the clue once its opening animation has finished, including reopened scrolls.
        _audio.PlayTaskHint(ScrollReadHintPath, 0.35);
    }

    private void CloseClue()
    {
        if (_clueClosing) return;
        if (IsInstanceValid(_clueTween)) _clueTween!.Kill();
        _clueClosing = true;
        _clueTween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        _clueTween.TweenProperty(_cluePicture, "position", _clueSourcePosition, 0.26);
        _clueTween.TweenProperty(_cluePicture, "scale", _clueSourceScale, 0.26);
        _clueTween.TweenProperty(_cluePicture, "rotation", _clueSourceRotation, 0.26);
        _clueTween.TweenProperty(_clueBackdrop, "modulate:a", 0, 0.26);
        _clueTween.Chain().TweenCallback(Callable.From(() =>
        {
            _clueViewer.Hide();
            _viewedClue?.Sprite.Show();
            _viewedClue = null;
            _cluePicture.Position = _clueHomePosition;
            _cluePicture.Scale = _clueHomeScale;
            _cluePicture.Rotation = 0;
            _clueViewer.Modulate = _clueHomeColor;
            _clueBackdrop.Modulate = _clueBackdropColor;
            _clueClosing = false;
            if (_rewardReady)
                ShowTreasurePrompt();
        }));
    }

    private void UpdateBottleDrift(double delta)
    {
        _bottleDriftTime += (float)delta;
        float speed = Mathf.Tau / Mathf.Max(BottleDriftPeriod, 0.1f);
        foreach (TreasureItem bottle in _groups[GroupIds[5]])
        {
            // Once opened, leave the bottle still so its revealed fragment is easy to collect.
            if (bottle.Done) continue;
            float phase = _bottleDriftTime * speed * bottle.DriftSpeed + bottle.DriftPhase;
            bottle.Sprite.Position = bottle.HomePosition + new Vector2(
                Mathf.Sin(phase) * BottleDriftRange.X,
                Mathf.Sin(phase * 1.3f) * BottleDriftRange.Y);
            bottle.Sprite.Rotation = bottle.HomeRotation + Mathf.Sin(phase * 0.8f)
                * Mathf.DegToRad(BottleRockDegrees);
        }
    }

    private void ScrollMap(InputEventMouseButton button)
    {
        float halfHeight = _background.Texture.GetHeight()*Mathf.Abs(_background.Scale.Y)*0.5f;
        float minimum = Math.Min(halfHeight,GetViewportRect().Size.Y-halfHeight);
        float step = ScrollStep*Mathf.Max(button.Factor,0.1f);
        float next = Mathf.Clamp(_targetY+(button.ButtonIndex==MouseButton.WheelDown ? -step : step),minimum,halfHeight);
        if (Mathf.IsEqualApprox(_targetY,next)) return;
        _targetY = next;
        if (IsInstanceValid(_scrollTween)) _scrollTween!.Kill();
        _scrollTween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _scrollTween.TweenProperty(_background,"position:y",_targetY,ScrollDuration);
    }

    private void StopScroll()
    {
        if (IsInstanceValid(_scrollTween)) _scrollTween!.Kill();
        _scrollTween = null;
        _targetY = _background.Position.Y;
    }

    private void AllTasksCompleted(int dayId, double elapsed)
    {
        if (dayId != 11) return;
        _rewardReady = true;
        if (_puzzleExpanded) _puzzleCloseCountdown = 1.5;
        ShowTreasurePrompt();
    }

    private void ShowTreasurePrompt()
    {
        if (_treasureDug)
            _taskPanel.SetCompletionPrompt("打开宝箱", _chestClosed, ChestHintPath);
        else
            _taskPanel.SetCompletionPrompt("挖出宝箱", _shovel.Texture, DigHintPath);
    }

    private void MoveShovel()
    {
        Vector2 home = (_shovelHomeParent.GlobalTransform * _shovelHomeTransform).Origin;
        Vector2 desired = GetGlobalMousePosition() + _grabOffset;
        desired = home + (desired - home).LimitLength(ShovelMaxDragDistance);
        Vector2 half = _shovel.Texture.GetSize() * _shovel.GlobalScale.Abs() * 0.5f;
        Vector2 size = GetViewportRect().Size;
        _shovel.GlobalPosition = new Vector2(
            Mathf.Clamp(desired.X, half.X, size.X - half.X),
            Mathf.Clamp(desired.Y, half.Y, size.Y - half.Y));
    }

    private void CancelShovelDrag()
    {
        if (!_shovelDragging) return;
        _shovelDragging = false;
        ReturnShovel();
    }

    private void RestoreShovel()
    {
        if (_shovel.GetParent() != _shovelHomeParent) _shovel.Reparent(_shovelHomeParent, false);
        _shovel.Transform = _shovelHomeTransform;
        _shovel.ZIndex = _shovelHomeZ;
    }

    private void ReturnShovel()
    {
        _busy = true;
        Transform2D home = _shovelHomeParent.GlobalTransform * _shovelHomeTransform;
        Tween tween = NewAnimation().SetParallel();
        _digTween = tween;
        tween.TweenProperty(_shovel, "global_position", home.Origin, ActionDuration);
        tween.TweenProperty(_shovel, "global_scale", home.Scale, ActionDuration);
        tween.TweenProperty(_shovel, "global_rotation", home.Rotation, ActionDuration);
        tween.Chain().TweenCallback(Callable.From(() =>
        {
            RestoreShovel();
            _busy = false;
        }));
    }

    private void DigTreasure()
    {
        _busy = true;
        _audio.StopTaskHint();
        Vector2 basePosition = _shovel.GlobalPosition + _digTarget.GlobalPosition - _shovelTip.GlobalPosition;
        float baseRotation = _shovel.GlobalRotation;
        Tween tween = NewAnimation();
        _digTween = tween;
        tween.TweenProperty(_shovel, "global_position", basePosition, 0.2);
        for (int stroke = 0; stroke < 3; stroke++)
        {
            tween.TweenProperty(_shovel, "global_position", basePosition + new Vector2(0, -24), 0.15);
            tween.Parallel().TweenProperty(_shovel, "global_rotation", baseRotation - 0.15f, 0.15);
            tween.TweenCallback(Callable.From(() =>
            {
                if (_digSound != null) _audio.PlayEffect(_digSound);
                else _audio.PlayDefaultInteractionEffect();
            }));
            tween.TweenProperty(_shovel, "global_position", basePosition + new Vector2(0, 10), 0.18);
            tween.Parallel().TweenProperty(_shovel, "global_rotation", baseRotation + 0.08f, 0.18);
            // Finish this full clip before starting the next stroke or revealing the chest.
            double remainingSound = (_digSound?.GetLength() ?? 0) - 0.18;
            if (remainingSound > 0) tween.TweenInterval(remainingSound);
        }
        tween.TweenCallback(Callable.From(() =>
        {
            _treasureDug = true;
            _digPit.Scale = _pitHomeScale * 0.55f;
            _digPit.Modulate = new Color(_pitHomeColor, 0);
            _digPit.Show();
        }));
        tween.TweenProperty(_digPit, "scale", _pitHomeScale, 0.25);
        tween.Parallel().TweenProperty(_digPit, "modulate:a", _pitHomeColor.A, 0.25);
        tween.TweenCallback(Callable.From(() => _chest.Show()));
        tween.TweenCallback(Callable.From(() =>
        {
            ShowTreasurePrompt();
            ReturnShovel();
        }));
    }

    private void OpenFinalChest()
    {
        if (!_rewardReady || !_treasureDug || _rewardOpened) return;
        _rewardOpened = true;
        _chest.Texture = _chestOpen;
        if (_chestSound != null)
            _audio.PlayEffect(_chestSound);
        else
            _audio.PlayDefaultInteractionEffect();
        _taskPanel.SetCompletionPrompt("寻宝成功",_chestOpen);
        _taskPanel.ShowCompletionResult();
    }

    private bool HasBlockingOverlay() => GetChildren().OfType<CanvasLayer>().Any(layer => layer.Layer>=50);

    private void ResetDay(int dayId) { if (dayId==11) RestoreItems(); }

    private void RestoreItems()
    {
        StopAnimations();
        StopScroll();
        _dragged = null;
        _busy = false;
        _focusedGroup = "";
        _puzzleCloseCountdown = -1;
        _rewardReady = false;
        _rewardOpened = false;
        _treasureDug = false;
        _shovelDragging = false;
        RestoreShovel();
        _digPit.Hide();
        _digPit.Scale = _pitHomeScale;
        _digPit.Modulate = _pitHomeColor;
        _chest.Hide();
        _bottleDriftTime = 0;
        _audio.StopTaskHint();
        _clueViewer.Hide();
        _viewedClue = null;
        _cluePicture.Position = _clueHomePosition;
        _cluePicture.Scale = _clueHomeScale;
        _cluePicture.Rotation = 0;
        _clueViewer.Modulate = _clueHomeColor;
        _clueBackdrop.Modulate = _clueBackdropColor;
        _clueClosing = false;
        _puzzleExpanded = false;
        _puzzleTransitioning = false;
        _autoOpenPuzzlePending = false;
        _flyingFragment = null;
        _puzzle.Show();
        _puzzle.Position = _puzzleMiniAnchor.Position;
        _puzzle.Scale = _puzzleMiniAnchor.Scale;
        _fixedX = _background.Position.X;
        _targetY = _background.Texture.GetHeight()*Mathf.Abs(_background.Scale.Y)*0.5f;
        _background.Position = new Vector2(_fixedX,_targetY);
        _chest.Texture = _chestClosed;
        _chest.Scale = _chestScale;
        foreach (List<TreasureItem> items in _groups.Values)
            foreach (TreasureItem item in items)
            {
                if (item.Sprite.GetParent()!=item.HomeParent) item.Sprite.Reparent(item.HomeParent,false);
                item.Done = false;
                item.Sprite.Texture = item.HomeTexture;
                item.Sprite.Position = item.HomePosition;
                item.Sprite.Scale = item.HomeScale;
                item.Sprite.Rotation = item.HomeRotation;
                item.Sprite.Modulate = item.HomeColor;
                item.Sprite.ZIndex = item.HomeZ;
            }
        RefreshItems();
        FocusGroup(GroupIds[0]);
        UpdateBottleDrift(0);
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
        if (IsInstanceValid(_clueTween)) _clueTween!.Kill();
        _clueTween = null;
        if (IsInstanceValid(_puzzleTween)) _puzzleTween!.Kill();
        _puzzleTween = null;
        foreach (Tween tween in _animations) if (IsInstanceValid(tween)) tween.Kill();
        _animations.Clear();
        _digTween = null;
    }

    private static Texture2D LoadTexture(string name) =>
        GD.Load<Texture2D>(AssetRoot+name+".tres") ?? throw new InvalidOperationException($"Missing texture: {name}");

    private static Godot.Collections.Dictionary Report(TreasureItem item) => new()
    {
        ["operation"] = item.Definition.Operation, ["object_id"] = item.Id,
        ["object_type"] = item.Definition.TargetType, ["object_color"] = "",
        ["object_tags"] = new string[] {item.Definition.TargetTag}, ["timestamp"] = (long)Time.GetTicksMsec()
    };

    private sealed class TreasureItem(TaskDefinition definition,string id,int groupIndex,int index,Sprite2D sprite)
    {
        public TaskDefinition Definition { get; } = definition;
        public string Id { get; } = id;
        public int GroupIndex { get; } = groupIndex;
        public int Index { get; } = index;
        public Sprite2D Sprite { get; } = sprite;
        public Node2D HomeParent { get; } = sprite.GetParent<Node2D>();
        public Texture2D HomeTexture { get; } = sprite.Texture;
        public Vector2 HomePosition { get; } = sprite.Position;
        public Vector2 HomeScale { get; } = sprite.Scale;
        public Transform2D HomeTransform { get; } = sprite.Transform;
        public float HomeRotation { get; } = sprite.Rotation;
        public Color HomeColor { get; } = sprite.Modulate;
        public int HomeZ { get; } = sprite.ZIndex;
        public Texture2D? OpenTexture { get; set; }
        public Sprite2D? Target { get; set; }
        public TreasureItem? Cover { get; set; }
        public bool Done { get; set; }
        public float DriftPhase { get; set; }
        public float DriftSpeed { get; set; } = 1;
    }
}
