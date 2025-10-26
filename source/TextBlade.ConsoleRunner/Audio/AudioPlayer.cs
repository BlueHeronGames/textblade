using NAudio.Vorbis;
using NAudio.Wave;
using TextBlade.Core.Audio;

namespace TestBlade.ConsoleRunner.Audio;

/// <summary>
/// A fire-and-forget audio player for audio.
/// Can loop OGG files.
/// </summary>
public class AudioPlayer : ISoundPlayer, IDisposable
{
    private WaveStream? _reader;
    private WaveOutEvent? _waveOut;
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

        if (_reader != null)
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _reader?.Dispose();
        }

        var fileExtension = Path.GetExtension(fileName).Replace(".", "").ToLower();
        switch (fileExtension)
        {
            case "ogg":
                _reader = new VorbisWaveReader(fileName);
                break;
            case "wav":
                _reader = new WaveFileReader(fileName);
                break;
            default:
                throw new ArgumentException($"Not sure how to play {fileExtension} files");
        }

        _waveOut = new WaveOutEvent();
        _waveOut.Init(_reader);
        _waveOut.PlaybackStopped += (sender, stoppedArgs) => 
        {
            if (stoppedArgs.Exception != null)
            {
                throw stoppedArgs.Exception;
            }

            OnPlaybackComplete?.Invoke();

            if (this.LoopPlayback)
            {
                _reader.Position = 0;
                _waveOut.Play();
            }
        };
        
        _waveOut.Play();
    }

    /// <summary>
    /// Volume to play back at; 0 is silent and 1.0 is maximum volume.
    /// </summary>
    public float Volume
    {
        get { return _waveOut?.Volume ?? 0; }
        set { if (_waveOut != null) _waveOut.Volume = value; }
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
        
        _waveOut?.Stop();
    }

    public bool IsPlaying
    {
        get
        {
            if (_waveOut != null)
            {
                return _waveOut.PlaybackState == PlaybackState.Playing;
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
        _waveOut?.Dispose();
        _reader?.Dispose();
    }
}
