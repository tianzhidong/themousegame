using Godot;

namespace NewGameProject;

public partial class AudioManager : Node
{
    private const string BackgroundMusicPath = "res://assets/audio/music/forest_town_theme.ogg";
    private const string MusicBusName = "Music";

    private AudioStreamPlayer _backgroundMusicPlayer = null!;

    public override void _Ready()
    {
        EnsureMusicBus();

        var music = GD.Load<AudioStream>(BackgroundMusicPath);
        if (music == null)
        {
            GD.PushError($"Failed to load background music: {BackgroundMusicPath}");
            return;
        }

        _backgroundMusicPlayer = new AudioStreamPlayer
        {
            Name = "BackgroundMusicPlayer",
            Bus = MusicBusName,
            Stream = music
        };

        AddChild(_backgroundMusicPlayer);
        _backgroundMusicPlayer.Play();
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
