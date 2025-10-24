using LibVLCSharp.Shared;
using TextBlade.ConsoleRunner.Audio;
using TextBlade.Core.Audio;

namespace TestBlade.ConsoleRunner.Audio;

/// <summary>
/// A fire-and-forget audio player for audio.
/// Can loop OGG files.
/// </summary>
public class AudioPlayer : ISoundPlayer, IDisposable
{
    // Should only have one LibVLC instance in your app
    private static readonly LibVLC s_libVlc = new();

    private MediaPlayer? _player;
    private Media? _media;
    private bool _isDisposed = false;

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
    }

    /// <summary>
    /// Create a new audio player, and preload the audio file specified.
    /// </summary>
    public void Play(string fileName, string channel = "stereo")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (_player != null)
        {
            _media.Dispose();
            // _player.Dispose() never comes back. I don't know why.
            // Will this thread hang around forever? Do I care? I know not.
            ThreadPool.QueueUserWorkItem((x) => 
            {
                _player.Dispose();
                 _player = null;
            });
        }

        _player = new MediaPlayer(s_libVlc) { EnableHardwareDecoding = true };
        _media = new Media(s_libVlc, fileName, FromType.FromPath);
        _player.EndReached += (sender, args) => 
        {
            OnPlaybackComplete?.Invoke();
            if (this.LoopPlayback)
            {
                // Must be in a separate thread or it might crash
                ThreadPool.QueueUserWorkItem((x) => 
                {
                    _player.Stop();
                    _player.Play();
                });
            }
        };

        _player.SetChannel(channel.ToAudioOutputChannel());
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
    public void Stop()
    {
        if (_isDisposed)
        {
            return;
        }
        
        if (_player != null && _player.IsPlaying)
        {
            _player?.Stop();
        }
    }

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
        if (_isDisposed)
        {
            // Double dispose causes a crash
            return;
        }

        _isDisposed = true;
        _player?.Dispose();
        _media?.Dispose();
    }
}
