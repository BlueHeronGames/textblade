using LibVLCSharp.Shared;
using TextBlade.Core.Audio;

namespace TestBlade.ConsoleRunner.Audio;

/// <summary>
/// A fire-and-forget audio player for audio.
/// Can loop OGG files.
/// </summary>
public class AudioPlayer : ISoundPlayer, IDisposable
{
    private LibVLC _libVlc;

    private MediaPlayer? _player;
    private Media? _media;

    /// <summary>
    /// An event that fires when playback automatically completes.
    /// Does not fire if you call Stop or Pause.
    /// If LoopPlayback is true, this fires on every loop.
    /// </summary>
    public event Action? OnPlaybackComplete;

    /// <summary>
    /// Set to true to loop playback.
    /// </summary>
    public bool LoopPlayback { get; set; }
    
    public AudioPlayer(bool loopPlayback = false)
    {
        LoopPlayback = loopPlayback;
        _libVlc = new();
    }

    /// <summary>
    /// Create a new audio player, and preload the audio file specified.
    /// </summary>
    public void Play(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        _player = new MediaPlayer(_libVlc) { EnableHardwareDecoding = true };
        _media = new Media(_libVlc, fileName, FromType.FromPath);
        _player.EndReached += (sender, args) => 
        {
            OnPlaybackComplete?.Invoke();
            if (this.LoopPlayback)
            {
                ThreadPool.QueueUserWorkItem((x) => 
                {
                    _player.Stop();
                    _player.Play();
                });
            }
        };

        _player.Play(_media);
    }

    /// <summary>
    /// Volume to play back at; 0 is silent and 100 is maximum volume.
    /// </summary>
    public int Volume
    {
        get { return _player.Volume; }
        set { _player.Volume = value; }
    }
    
    /// <summary>
    /// Stops audio playback.
    /// </summary>
    public void Stop() => _player?.Stop();

    public bool IsPlaying
    {
        get
        {
            if (_player != null)
            {
                return _player.IsPlaying;
            }
            
            return false;
        }
    }

    public void Dispose()
    {
        _player?.Dispose();
        _media?.Dispose();
    }
}
