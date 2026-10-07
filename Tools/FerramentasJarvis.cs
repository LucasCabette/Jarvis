using System.Text.Json;
using Jarvis.AI;

namespace Jarvis.Tools;

public sealed class FerramentasJarvis : IExecutorFerramentas
{
    private const string ParametrosPesquisa = """
        {
            "type": "object",
            "properties": {
                "pesquisa": {
                    "type": "string",
                    "description": "Texto da pesquisa com espaços normais. Não codifique como URL nem transforme espaços em +."
                }
            },
            "required": ["pesquisa"]
        }
        """;

    public IReadOnlyList<DefinicaoFerramenta> Definicoes { get; } =
    [
        new("spotify_pesquisar",
            "Abre uma pesquisa no Spotify sem reproduzir música. Use para pesquisar, procurar ou buscar. Não use para pedidos de tocar ou ouvir.",
            ParametrosPesquisa),
        new("spotify_tocar",
            "Tenta reproduzir o resultado da busca rápida no Spotify por automação de teclado. Use para toca, coloca para tocar, dá play em ou quero ouvir. Pergunte antes se não houver indicação do que ouvir. A faixa e o início da reprodução não são verificados.",
            ParametrosPesquisa)
    ];

    public async Task<string> ExecutarAsync(ChamadaFerramenta chamada)
    {
        if (chamada.Nome is not ("spotify_pesquisar" or "spotify_tocar"))
            return "Ferramenta desconhecida. Nenhuma ação foi executada.";

        if (!chamada.Argumentos.TryGetValue("pesquisa", out var valor)
            || valor.ValueKind != JsonValueKind.String)
            return "Pesquisa ausente ou inválida. Pergunte o que a pessoa deseja ouvir.";

        var pesquisa = valor.GetString()?.Trim();

        if (string.IsNullOrWhiteSpace(pesquisa))
            return "Pesquisa ausente. Pergunte o que a pessoa deseja ouvir.";

        if (pesquisa.Any(char.IsControl))
            return "Pesquisa inválida: contém caracteres de controle. Nenhuma ação foi executada.";

        return chamada.Nome == "spotify_pesquisar"
            ? await SpotifyService.PesquisarAsync(pesquisa)
            : await SpotifyService.TocarAsync(pesquisa);
    }
}
