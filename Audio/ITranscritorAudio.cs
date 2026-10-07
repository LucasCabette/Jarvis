namespace Jarvis.Audio;

public interface ITranscritorAudio
{
    Task<string> TranscreverAudioAsync(byte[] audio);
}
