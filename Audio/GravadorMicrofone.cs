using NAudio.Wave;

namespace Jarvis.Audio;

public sealed class GravadorMicrofone : IDisposable
{
    private WaveInEvent? _entrada;
    private TaskCompletionSource<byte[]>? _conclusao;
    private bool _descartado;

    public void Iniciar()
    {
        ObjectDisposedException.ThrowIf(_descartado, this);

        if (_entrada is not null)
            throw new InvalidOperationException(
                "Já existe uma gravação em andamento."
            );

        if (WaveInEvent.DeviceCount == 0)
            throw new InvalidOperationException(
                "Nenhum dispositivo de gravação foi encontrado."
            );

        var memoria = new MemoryStream();

        var entrada = new WaveInEvent
        {
            DeviceNumber = 0,
            WaveFormat = new WaveFormat(16000, 16, 1)
        };

        var escritor = new WaveFileWriter(memoria, entrada.WaveFormat);

        var conclusao = new TaskCompletionSource<byte[]>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        Exception? erroCaptura = null;

        entrada.DataAvailable += (_, e) =>
        {
            try
            {
                escritor.Write(e.Buffer, 0, e.BytesRecorded);
            }
            catch (Exception ex)
            {
                erroCaptura = ex;
                entrada.StopRecording();
            }
        };

        entrada.RecordingStopped += (_, e) =>
        {
            try
            {
                // Finaliza o cabeçalho WAV antes de obter os bytes.
                escritor.Dispose();

                var erro = e.Exception ?? erroCaptura;

                if (erro is not null)
                    conclusao.TrySetException(erro);
                else
                    conclusao.TrySetResult(memoria.ToArray());
            }
            catch (Exception ex)
            {
                conclusao.TrySetException(ex);
            }
            finally
            {
                entrada.Dispose();
                memoria.Dispose();
            }
        };

        _entrada = entrada;
        _conclusao = conclusao;

        try
        {
            entrada.StartRecording();
        }
        catch
        {
            _entrada = null;
            _conclusao = null;

            escritor.Dispose();
            memoria.Dispose();
            entrada.Dispose();

            throw;
        }
    }

    public async Task<byte[]> PararAsync()
    {
        var entrada = _entrada;
        var conclusao = _conclusao;

        if (entrada is null || conclusao is null)
            throw new InvalidOperationException(
                "Não existe gravação em andamento."
            );

        try
        {
            entrada.StopRecording();
            return await conclusao.Task;
        }
        finally
        {
            _entrada = null;
            _conclusao = null;
        }
    }

    public void Dispose()
    {
        if (_descartado)
            return;

        _descartado = true;

        // O evento RecordingStopped libera os recursos.
        _entrada?.StopRecording();
    }
}