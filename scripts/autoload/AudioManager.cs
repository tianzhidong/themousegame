using Godot;

namespace NewGameProject;

public partial class AudioManager : Node
{
    private const string BackgroundMusicPath = "res://assets/audio/music/forest_town_theme.ogg";
    private const string UiClickSoundPath = "res://assets/audio/ui/ui_click.ogg";
    private const string TaskFinishSoundPath = "res://assets/audio/ui/finish.mp3";
    private const string DefaultInteractionSoundPath = "res://assets/audio/voice/通用.ogg";
    private const string SoftInteractionSoundPath = "res://assets/audio/voice/generic_soft.mp3";
    private const string Day5InstallSoundPath = "res://assets/audio/voice/day5/part_install.mp3";
    private const string Day5RepairSoundPath = "res://assets/audio/voice/day5/repair_complete.mp3";
    private const string Day5MusicPath = "res://assets/audio/voice/day5/day5_bgm.mp3";
    private const double TaskFinishDelaySeconds = 1.0;
    private const string MusicBusName = "Music";

    private AudioStreamPlayer _backgroundMusicPlayer = null!;
    private AudioStreamPlayer _uiClickPlayer = null!;
    private AudioStream _taskFinishSound = null!;
    private AudioStream _defaultInteractionSound = null!;
    private AudioStream _softInteractionSound = null!;
    private AudioStream _day5InstallSound = null!;
    private AudioStream _day5RepairSound = null!;
    private AudioStreamPlayer _day5RepairPlayer = null!;
    private AudioStream _forestMusic = null!;
    private AudioStreamMP3 _day5Music = null!;
    private bool _playingDay5Music;
    private AudioStreamPlayer _taskHintPlayer = null!;
    private int _taskHintGeneration;

    public override void _Ready()
    {
        EnsureMusicBus();
        SetupUiClickPlayer();
        ProcessMode = ProcessModeEnum.Always;
        _taskHintPlayer = new AudioStreamPlayer { Name = "TaskHintPlayer", ProcessMode = ProcessModeEnum.Pausable };
        AddChild(_taskHintPlayer);
        _taskFinishSound = GD.Load<AudioStream>(TaskFinishSoundPath);
        _defaultInteractionSound = GD.Load<AudioStream>(DefaultInteractionSoundPath);
        _softInteractionSound = GD.Load<AudioStream>(SoftInteractionSoundPath);
        _day5InstallSound = GD.Load<AudioStream>(Day5InstallSoundPath);
        _day5RepairSound = GD.Load<AudioStream>(Day5RepairSoundPath);
        _day5Music = (AudioStreamMP3)GD.Load<AudioStreamMP3>(Day5MusicPath).Duplicate();
        _day5Music.Loop = true;
        if (_taskFinishSound == null)
        {
            GD.PushError($"Failed to load task finish sound: {TaskFinishSoundPath}");
        }
        StartBackgroundMusic();
    }

    public override void _Process(double delta)
    {
        if (_taskHintPlayer.Playing && TaskHintsBlocked()) StopTaskHint();
        UpdateDay5BackgroundMusic();
    }

    private void UpdateDay5BackgroundMusic()
    {
        Node scene = GetTree().CurrentScene;
        if (scene == null || _backgroundMusicPlayer == null) return;
        bool useDay5Music = scene.SceneFilePath is "res://scenes/levels/level_5.tscn"
            or "res://scenes/game/fixgame.tscn";
        if (useDay5Music == _playingDay5Music) return;

        // Both Day5 scenes share this autoload player, keeping the playback position on scene changes.
        bool wasPaused = _backgroundMusicPlayer.StreamPaused;
        _playingDay5Music = useDay5Music;
        _backgroundMusicPlayer.Stream = useDay5Music ? _day5Music : _forestMusic;
        _backgroundMusicPlayer.Play();
        _backgroundMusicPlayer.StreamPaused = wasPaused;
    }

    public bool TaskHintsBlocked()
    {
        Node scene = GetTree().CurrentScene;
        return GetTree().Paused || !GetWindow().HasFocus() || scene == null
            || scene.GetNodeOrNull<Node>("SettingsOverlay") != null
            || scene.GetNodeOrNull<Node>("GameExitOverlay") != null
            || scene.GetNodeOrNull<Node>("ResultOverlayLayer") != null;
    }

    public void PlayAutomaticTaskHint(string path)
    {
        PlayTaskHint(path, 1.0);
    }

    public void PlayTaskHint(string path, double delaySeconds = 0.0)
    {
        StopTaskHint();
        if (string.IsNullOrWhiteSpace(path)) return;
        int generation = _taskHintGeneration;
        // Scene changes assign CurrentScene after the new scene's _Ready callbacks.
        Callable.From(() => StartTaskHint(path, generation, delaySeconds)).CallDeferred();
    }

    private async void StartTaskHint(string path, int generation, double delaySeconds)
    {
        if (generation != _taskHintGeneration || TaskHintsBlocked()) return;
        if (delaySeconds > 0.0)
        {
            await ToSignal(GetTree().CreateTimer(delaySeconds, false), SceneTreeTimer.SignalName.Timeout);
            if (!IsInstanceValid(this) || !IsInsideTree()
                || generation != _taskHintGeneration || TaskHintsBlocked()) return;
        }
        AudioStream stream = GD.Load<AudioStream>(path);
        if (stream == null)
        {
            GD.PushWarning($"Failed to load task hint: {path}");
            return;
        }
        _taskHintPlayer.Stream = stream;
        _taskHintPlayer.Play();
    }

    public void StopTaskHint()
    {
        _taskHintGeneration++;
        if (_taskHintPlayer != null) _taskHintPlayer.Stop();
    }

    public void PlayUiClick()
    {
        if (_uiClickPlayer == null || _uiClickPlayer.Stream == null)
        {
            return;
        }

        _uiClickPlayer.Play();
    }

    public async void PlayTaskFinish()
    {
        if (_taskFinishSound == null)
        {
            return;
        }

        await ToSignal(GetTree().CreateTimer(TaskFinishDelaySeconds), SceneTreeTimer.SignalName.Timeout);
        if (!IsInstanceValid(this) || !IsInsideTree())
        {
            return;
        }

        PlayEffect(_taskFinishSound);
    }

    public void PlayEffect(AudioStream sound)
    {
        StartEffect(sound);
    }

    private AudioStreamPlayer StartEffect(AudioStream sound)
    {
        // Each trigger plays its full clip independently, at the source volume.
        AudioStreamPlayer player = new() { Stream = sound ?? _defaultInteractionSound, ProcessMode = ProcessModeEnum.Pausable };
        AddChild(player);
        player.Finished += player.QueueFree;
        player.Play();
        return player;
    }

    public void PlayDefaultInteractionEffect()
    {
        PlayEffect(_defaultInteractionSound);
    }

    public void PlaySoftInteractionEffect()
    {
        PlayEffect(_softInteractionSound);
    }

    public void PlayDay5PartInstall()
    {
        PlayEffect(_day5InstallSound);
    }

    public void PlayDay5RepairCompleted()
    {
        _day5RepairPlayer = StartEffect(_day5RepairSound);
    }

    public AudioStreamPlayer GetDay5RepairPlayer()
    {
        return IsInstanceValid(_day5RepairPlayer) && _day5RepairPlayer.Playing
            ? _day5RepairPlayer : null;
    }

    public void SetBackgroundMusicPaused(bool paused)
    {
        if (_backgroundMusicPlayer != null)
        {
            _backgroundMusicPlayer.StreamPaused = paused;
        }
    }

    private void StartBackgroundMusic()
    {
        var music = GD.Load<AudioStream>(BackgroundMusicPath);
        if (music == null)
        {
            GD.PushError($"Failed to load background music: {BackgroundMusicPath}");
            return;
        }

        _forestMusic = music;
        _backgroundMusicPlayer = new AudioStreamPlayer
        {
            Name = "BackgroundMusicPlayer",
            ProcessMode = ProcessModeEnum.Pausable,
            Bus = MusicBusName,
            Stream = music
        };

        AddChild(_backgroundMusicPlayer);
        _backgroundMusicPlayer.Play();
    }

    private void SetupUiClickPlayer()
    {
        var clickSound = GD.Load<AudioStream>(UiClickSoundPath);
        if (clickSound == null)
        {
            GD.PushError($"Failed to load UI click sound: {UiClickSoundPath}");
            return;
        }

        _uiClickPlayer = new AudioStreamPlayer
        {
            Name = "UiClickPlayer",
            ProcessMode = ProcessModeEnum.Pausable,
            Stream = clickSound
        };

        AddChild(_uiClickPlayer);
    }

    private static void EnsureMusicBus()
    {
        if (AudioServer.GetBusIndex(MusicBusName) >= 0)
        {
            return;
        }

        AudioServer.AddBus();
        AudioServer.SetBusName(AudioServer.BusCount - 1, MusicBusName);
    }
}
