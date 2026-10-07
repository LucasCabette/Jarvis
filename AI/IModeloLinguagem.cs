using System.Text.Json;

namespace Jarvis.AI;

// Contratos do Jarvis: não dependem dos tipos do SDK do Google.
public interface IModeloLinguagem
{
    Task<MensagemModelo> GerarRespostaAsync(
        IReadOnlyList<MensagemModelo> conversa,
        string instrucoes,
        IReadOnlyList<DefinicaoFerramenta> ferramentas);
}

public sealed record DefinicaoFerramenta(
    string Nome, string Descricao, string ParametrosJson);

public sealed record ChamadaFerramenta(
    string? Id, string Nome, IReadOnlyDictionary<string, JsonElement> Argumentos);

public sealed record ResultadoFerramenta(
    string? Id, string Nome, string Resultado);

public sealed record MensagemModelo(
    string Papel,
    string Texto = "",
    IReadOnlyList<ChamadaFerramenta>? Chamadas = null,
    IReadOnlyList<ResultadoFerramenta>? Resultados = null,
    // Só o adaptador do provedor interpreta este conteúdo. Preserva, por
    // exemplo, assinaturas de pensamento exigidas em chamadas de ferramentas.
    object? DadosProvedor = null);
