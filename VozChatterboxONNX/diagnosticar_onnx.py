"""Compara selecao de tokens no exemplo ONNX sem alterar os testes existentes."""
from pathlib import Path
import os
from time import perf_counter
import types
import numpy as np
import soundfile as sf
import onnxruntime as ort

PASTA = Path(__file__).resolve().parent
os.environ['HF_HOME'] = str(PASTA / 'cache_hf')
origem = PASTA / 'chatterbox_multi_inference_script.py'
referencia = PASTA / 'referencia.wav'
if not origem.is_file() or not referencia.is_file():
    raise SystemExit('Falta o exemplo ONNX ou referencia.wav nesta pasta.')

rng = np.random.default_rng(42)

def amostrar(logits):
    valores = logits.astype(np.float64) / 0.8
    valores -= valores.max(axis=-1, keepdims=True)
    probabilidades = np.exp(valores)
    probabilidades[probabilidades < probabilidades.max(axis=-1, keepdims=True) * 0.05] = 0
    probabilidades /= probabilidades.sum(axis=-1, keepdims=True)
    return np.array([[rng.choice(probabilidades.shape[-1], p=linha)]
                     for linha in probabilidades], dtype=np.int64)

fonte = origem.read_text(encoding='utf-8')
alvo = 'next_token = np.argmax(next_token_logits, axis=-1, keepdims=True).astype(np.int64)'
if fonte.count(alvo) != 1:
    raise SystemExit('O exemplo mudou: nenhuma alteracao aplicada. Envie o arquivo para revisao.')
fonte = fonte.replace(alvo, 'next_token = amostrar(next_token_logits)')
alvo = 'speech_tokens = generate_tokens[:, 1:-1]'
if fonte.count(alvo) != 1:
    raise SystemExit('Trecho de encerramento inesperado. Nenhuma alteracao aplicada.')
fonte = fonte.replace(alvo, '''np.save(str(Path(output_dir).parent / "tokens_diagnostico.npy"), generate_tokens)
        terminou = bool((generate_tokens[:, -1] == STOP_SPEECH_TOKEN).all())
        print("Encerramento EOS:", terminou, "| Tokens:", generate_tokens.shape[1] - 1, flush=True)
        if not terminou:
            raise RuntimeError("A geracao atingiu o limite sem encerrar. Audio nao sera salvo como sucesso.")
        speech_tokens = generate_tokens[:, 1:-1]''')

original = ort.InferenceSession
def criar_sessao(caminho, *args, **kwargs):
    opcoes = ort.SessionOptions()
    # Mantem as otimizacoes padrao da CPU; DirectML nao e usado nesta comparacao.
    sessao = original(caminho, sess_options=opcoes, providers=['CPUExecutionProvider'])
    sessao.disable_fallback()
    return sessao

modulo = types.ModuleType('exemplo_diagnostico')
modulo.__dict__.update(amostrar=amostrar, Path=Path)
exec(compile(fonte, str(origem), 'exec'), modulo.__dict__)
modulo.prepare_language = lambda texto, idioma: f'[{idioma}]{texto}'
ort.InferenceSession = criar_sessao

saida = PASTA / 'teste_onnx_diagnostico.wav'
inicio = perf_counter()
modulo.run_inference(
    text='Boa noite, senhor. Estou pronto para ajudar com suas tarefas.',
    language_id='pt', target_voice_path=str(referencia),
    max_new_tokens=512, exaggeration=0.3,
    output_dir=str(PASTA / 'modelos'), output_file_name=str(saida),
    apply_watermark=False,
)
print(f'Total incluindo carregamento: {perf_counter() - inicio:.1f}s')
audio, taxa = sf.read(str(saida))
if audio.ndim > 1:
    audio = audio.mean(axis=1)
hop = int(taxa * 0.5)
rms = np.array([np.sqrt(np.mean(audio[i:i+hop] ** 2))
                for i in range(0, len(audio), hop)])
print(f'Duracao: {len(audio) / taxa:.2f}s')
print('Nivel RMS por meio segundo:', np.round(rms, 5).tolist())
print('Arquivo:', saida)
os.startfile(str(saida))
