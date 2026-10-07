using System.Speech.Synthesis;

namespace Jarvis.Audio;

public sealed class VozService : IDisposable
{
    private readonly SpeechSynthesizer _sintetizador = new();

    public VozService()
    {
        var vozes = _sintetizador.GetInstalledVoices()
            .Where(v => v.Enabled)
            .Select(v => v.VoiceInfo)
            .ToList();

        var voz = vozes.FirstOrDefault(
            v => v.Name == "Microsoft Daniel"
        ) ?? vozes.FirstOrDefault(
            v => v.Culture.Name == "pt-BR"
        );

        if (voz is null)
        {
            _sintetizador.Dispose();

            throw new InvalidOperationException(
                "Nenhuma voz em português brasileiro está disponível."
            );
        }

        _sintetizador.SelectVoice(voz.Name);
        _sintetizador.SetOutputToDefaultAudioDevice();

        _sintetizador.Rate = 0;
        _sintetizador.Volume = 100;
    }

    public void Falar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return;

        Parar();

        _sintetizador.SpeakAsync(texto);
    }

    public void Parar()
    {
        _sintetizador.SpeakAsyncCancelAll();
    }

    public void Dispose()
    {
        Parar();
        _sintetizador.Dispose();
    }
}