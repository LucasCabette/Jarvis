using Google.GenAI;
using Google.GenAI.Types;
using Jarvis.Tools;

namespace Jarvis.AI;
using Jarvis.Tools;

public class GeminiService
{
    private readonly Client _client;
    private readonly List<Content> _historico = new ();
    private readonly SemaphoreSlim _controleEnvio = new(1, 1);

    private const string Modelo = "gemini-3.5-flash-lite";
    private const string InstrucoesSistema = """
    Você é Jarvis, um assistente de inteligência artificial inspirado
    na postura do J.A.R.V.I.S. do Homem de Ferro, adaptado ao mundo real.

    Responda em português brasileiro, com clareza, educação, calma
    e objetividade. Seja natural e use humor sutil apenas quando
    o contexto permitir. Ajuste o nível de detalhe à pergunta.

    Não presuma o nome, as preferências ou informações pessoais
    de quem está conversando. Use as informações fornecidas
    na conversa quando forem relevantes. Sempre tratamento formal
    de autoridade e respeito, como "Senhor".

    Não interprete o universo dos filmes como realidade.
    Não invente armaduras, equipamentos, acesso ao computador
    ou capacidades que você não possui.

    Admita dúvidas e limitações. Nunca afirme que abriu um aplicativo,
    reproduziu uma música ou executou outra ação sem confirmação
    de uma ferramenta. Quando uma capacidade não estiver disponível,
    explique isso brevemente.

    Sua função é ajudar a pessoa a entender assuntos, resolver
    problemas e realizar tarefas dentro das capacidades disponíveis.
    """;
    private readonly Tool _ferramentaSpotify = new Tool
    {
        FunctionDeclarations =
        [
            FunctionDeclaration.FromJson("""
            {
                "name": "spotify_pesquisar",
                "description": "Abre uma pesquisa no Spotify sem reproduzir música. Use para pedidos como pesquisar, procurar ou buscar. Não use quando a pessoa pedir para tocar ou ouvir.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "pesquisa": {
                            "type": "string",
                            "description": "Texto original da pesquisa, com espaços normais. Não transforme espaços em + nem codifique como URL."
                        }
                    },
                    "required": ["pesquisa"]
                }
            }
            """)!,

            FunctionDeclaration.FromJson("""
            {
                "name": "spotify_tocar",
                "description": "Solicita pesquisar e reproduzir a primeira faixa da lista de músicas do Spotify. Use para pedidos como toca, coloca para tocar, dá play em ou quero ouvir. Se não houver indicação do que ouvir, pergunte antes.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "pesquisa": {
                            "type": "string",
                            "description": "Música ou artista solicitado, com espaços normais. Não transforme espaços em + nem codifique como URL."
                        }
                    },
                    "required": ["pesquisa"]
                }
            }
            """)!
        ]
    };
    public GeminiService()
    {
        _client = new Client();
    }
public async Task<string> EnviarMensagem(string mensagem)
{
    if (string.IsNullOrWhiteSpace(mensagem))
        return "Digite uma mensagem antes de enviar.";

    await _controleEnvio.WaitAsync();

    try
    {
        return await ProcessarMensagemAsync(mensagem.Trim());
    }
    catch (Exception)
    {
        return "Não consegui concluir a comunicação com o Gemini. "
            + "Verifique sua conexão e a configuração da API. "
            + "Não repeti nenhuma ação automaticamente.";
    }
    finally
    {
        _controleEnvio.Release();
    }
}

private async Task<string> ProcessarMensagemAsync(string mensagem)
{
    // Trabalha em uma cópia até termos uma troca válida.
    var conversa = new List<Content>(_historico);

    conversa.Add(new Content
    {
        Role = "user",
        Parts = [new Part { Text = mensagem }]
    });

    var config = new GenerateContentConfig
    {
        SystemInstruction = new Content
        {
            Parts = [new Part { Text = InstrucoesSistema }]
        },
        Tools = [_ferramentaSpotify]
    };

    var response = await _client.Models.GenerateContentAsync(
        model: Modelo,
        contents: conversa,
        config: config
    );

    var conteudo = response.Candidates?.FirstOrDefault()?.Content;

    if (conteudo?.Parts is null || conteudo.Parts.Count == 0)
        return "O Gemini não retornou conteúdo utilizável.";

    var chamadas = conteudo.Parts
        .Where(p => p.FunctionCall is not null)
        .Select(p => p.FunctionCall!)
        .ToList();

    if (chamadas.Count == 0)
    {
        var texto = ExtrairTexto(conteudo);

        if (string.IsNullOrWhiteSpace(texto))
            return "O Gemini não retornou uma resposta de texto.";

        conversa.Add(conteudo);
        SalvarHistorico(conversa);

        return texto;
    }

    // Preserva o conteúdo completo, incluindo os dados das ferramentas.
    conversa.Add(conteudo);

    var resultados = new List<Part>();
    var relatos = new List<string>();

    foreach (var chamada in chamadas)
    {
        string resultado;

        try
        {
            resultado = await ExecutarFerramentaAsync(chamada);
        }
        catch (Exception)
        {
            resultado = "A ferramenta falhou durante a execução. "
                + "Uma ação parcial pode ter ocorrido. "
                + "Não confirme sucesso nem repita automaticamente.";
        }

        relatos.Add(resultado);

        resultados.Add(new Part
        {
            FunctionResponse = new FunctionResponse
            {
                Id = chamada.Id,
                Name = chamada.Name,
                Response = new()
                {
                    ["output"] = resultado
                }
            }
        });
    }

    conversa.Add(new Content
    {
        Role = "user",
        Parts = resultados
    });

    // As ações já foram tentadas: guarda seus resultados antes
    // de pedir ao Gemini que escreva a resposta final.
    SalvarHistorico(conversa);

    try
    {
        var respostaFinal = await _client.Models.GenerateContentAsync(
            model: Modelo,
            contents: conversa,
            config: new GenerateContentConfig
            {
                SystemInstruction = config.SystemInstruction
            }
        );

        var conteudoFinal =
            respostaFinal.Candidates?.FirstOrDefault()?.Content;

        var textoFinal = ExtrairTexto(conteudoFinal);

        if (!string.IsNullOrWhiteSpace(textoFinal))
        {
            conversa.Add(conteudoFinal!);
            SalvarHistorico(conversa);

            return textoFinal;
        }
    }
    catch (Exception)
    {
        // Não executa a ferramenta novamente se a resposta final falhar.
    }

    return "Não consegui obter a resposta final do Gemini. "
        + "Resultado das ferramentas:\n"
        + string.Join("\n", relatos);
}

private async Task<string> ExecutarFerramentaAsync(FunctionCall chamada)
{
    if (chamada.Name is not ("spotify_pesquisar" or "spotify_tocar"))
        return "Ferramenta desconhecida. Nenhuma ação foi executada.";

    var pesquisa = chamada.Args is not null
        && chamada.Args.TryGetValue("pesquisa", out var valor)
            ? valor?.ToString()
            : null;

    if (string.IsNullOrWhiteSpace(pesquisa))
        return "Pesquisa ausente. Pergunte o que a pessoa deseja ouvir.";

    pesquisa = pesquisa.Trim();

    if (pesquisa.Any(char.IsControl))
        return "Pesquisa inválida: contém caracteres de controle. "
            + "Nenhuma ação foi executada.";

    return chamada.Name switch
    {
        "spotify_pesquisar" =>
            await SpotifyService.PesquisarAsync(pesquisa),

        "spotify_tocar" =>
            await SpotifyService.TocarAsync(pesquisa),

        _ => "Ferramenta desconhecida."
    };
}

private static string ExtrairTexto(Content? conteudo)
{
    if (conteudo?.Parts is null)
        return string.Empty;

    return string.Join(
        "\n",
        conteudo.Parts
            .Where(p => !string.IsNullOrWhiteSpace(p.Text))
            .Select(p => p.Text)
    );
}

private void SalvarHistorico(List<Content> conversa)
{
    _historico.Clear();
    _historico.AddRange(conversa);
}
}