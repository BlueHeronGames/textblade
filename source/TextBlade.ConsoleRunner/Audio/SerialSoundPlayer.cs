using TestBlade.ConsoleRunner.Audio;
using TextBlade.Core.Audio;

namespace TextBlade.ConsoleRunner.Audio;

public class SerialSoundPlayer : ISerialSoundPlayer, IDisposable
{
    private readonly AudioPlayer _audioPlayer = new();
    private int _currentAudioId = 0;
    private List<string> _audiosToPlay = new();

    public SerialSoundPlayer()
    {
        _audioPlayer.OnPlaybackComplete += PlayNext;
    }

    public void Play()
    {
        if (!_audiosToPlay.Any())
        {
            throw new InvalidOperationException("No audios are queued.");
        }

        _currentAudioId = -1;
        PlayNext();
    }

    public void Stop()
    {
        _audioPlayer.Stop();
        _currentAudioId = -1;
        _audiosToPlay.Clear();
    }

    public void Queue(string audioFile)
    {
        _audiosToPlay.Add(audioFile);
    }

    private void PlayNext()
    {
        _currentAudioId++;
        if (_currentAudioId >= _audiosToPlay.Count)
        {
            // We're done!
            Stop();
            return;
        }

        _audioPlayer.Stop();
        _audioPlayer.Load(_audiosToPlay[_currentAudioId]);
        _audioPlayer.Play();
    }

    public void Dispose()
    {
        _audioPlayer.Dispose();
    }
}
