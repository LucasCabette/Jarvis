using Jarvis.AI;
using Jarvis.Personality;
using Jarvis.Tools;

namespace Jarvis.Agent;

public sealed class JarvisAgent
{
    private readonly IModeloLinguagem _modelo;
    private readonly IExecutorFerramentas _ferramentas;
    private readonly string _instrucoes;
    private readonly List<MensagemModelo> _historico = [];
    private readonly SemaphoreSlim _controleEnvio = new(1, 1);

    public JarvisAgent(IModeloLinguagem modelo, IExecutorFerramentas ferramentas,
        string? instrucoes = null)
    {
        _modelo = modelo;
        _ferramentas = ferramentas;
        _instrucoes = instrucoes ?? PersonalidadeJarvis.Instrucoes;
    }

    public async Task<string> EnviarMensagemAsync(string mensagem)
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
            return "Não consegui concluir a comunicação com o modelo de IA. "
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
        var conversa = new List<MensagemModelo>(_historico)
        {
            new("user", mensagem)
        };

        var resposta = await _modelo.GerarRespostaAsync(
            conversa, _instrucoes, _ferramentas.Definicoes);

        var chamadas = resposta.Chamadas ?? [];

        if (chamadas.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(resposta.Texto))
                return "O modelo não retornou uma resposta de texto.";

            conversa.Add(resposta);
            SalvarHistorico(conversa);
            return resposta.Texto;
        }

        conversa.Add(resposta);
        var resultados = new List<ResultadoFerramenta>();

        foreach (var chamada in chamadas)
        {
            string resultado;
            try
            {
                resultado = await _ferramentas.ExecutarAsync(chamada);
            }
            catch (Exception)
            {
                resultado = "A ferramenta falhou durante a execução. Uma ação parcial pode ter ocorrido. "
                    + "Não confirme sucesso nem repita automaticamente.";
            }

            resultados.Add(new(chamada.Id, chamada.Nome, resultado));
        }

        conversa.Add(new("user", Resultados: resultados));

        // Guarda as ações e seus resultados antes de consultar novamente o
        // modelo. Uma falha na resposta final não deve repetir uma ação.
        SalvarHistorico(conversa);

        try
        {
            var respostaFinal = await _modelo.GerarRespostaAsync(conversa, _instrucoes, []);

            if (!string.IsNullOrWhiteSpace(respostaFinal.Texto)
                && (respostaFinal.Chamadas?.Count ?? 0) == 0)
            {
                conversa.Add(respostaFinal);
                SalvarHistorico(conversa);
                return respostaFinal.Texto;
            }
        }
        catch (Exception)
        {
            // As ferramentas já foram tentadas. Usa os resultados disponíveis.
        }

        return "Não consegui obter a resposta final do modelo. Resultado das ferramentas:\n"
            + string.Join("\n", resultados.Select(r => r.Resultado));
    }

    private void SalvarHistorico(List<MensagemModelo> conversa)
    {
        _historico.Clear();
        _historico.AddRange(conversa);
    }
}
