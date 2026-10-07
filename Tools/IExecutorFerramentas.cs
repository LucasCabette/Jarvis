using Jarvis.AI;

namespace Jarvis.Tools;

public interface IExecutorFerramentas
{
    IReadOnlyList<DefinicaoFerramenta> Definicoes { get; }
    Task<string> ExecutarAsync(ChamadaFerramenta chamada);
}
