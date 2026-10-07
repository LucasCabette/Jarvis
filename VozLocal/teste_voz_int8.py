import os
from pathlib import Path
from time import perf_counter

pasta = Path(__file__).resolve().parent
os.environ["HF_HOME"] = str(pasta / "modelos")

import torch
print(f"Threads anteriores: {torch.get_num_threads()}")
torch.set_num_threads(6)
torch.set_num_interop_threads(1)
print(f"Threads do teste: {torch.get_num_threads()}")
import torchaudio
from chatterbox.mtl_tts import ChatterboxMultilingualTTS

print("Carregando modelo...", flush=True)
inicio = perf_counter()
modelo = ChatterboxMultilingualTTS.from_pretrained(device="cpu")
print(f"Carregamento: {perf_counter() - inicio:.1f}s")

print("Preparando referência...", flush=True)
inicio = perf_counter()
modelo.prepare_conditionals(
    str(pasta / "referencia.wav"),
    exaggeration=0.3
)
print(f"Preparação da voz: {perf_counter() - inicio:.1f}s")

print("Convertendo gerador de tokens para INT8...", flush=True)
inicio = perf_counter()

modelo.t3.tfmr.eval()

torch.ao.quantization.quantize_dynamic(
    modelo.t3.tfmr,
    qconfig_spec={torch.nn.Linear},
    dtype=torch.qint8,
    inplace=True,
)

quantizadas = sum(
    isinstance(
        camada,
        torch.ao.nn.quantized.dynamic.Linear,
    )
    for camada in modelo.t3.tfmr.modules()
)

print(f"Camadas INT8: {quantizadas}", flush=True)
print(f"Conversão para INT8: {perf_counter() - inicio:.1f}s")

def medir_etapa(nome, funcao):
    def executar(*args, **kwargs):
        inicio = perf_counter()
        resultado = funcao(*args, **kwargs)
        print(f"{nome}: {perf_counter() - inicio:.2f}s", flush=True)
        return resultado
    return executar

modelo.t3.inference = medir_etapa(
    "Geracao dos tokens", modelo.t3.inference
)

modelo.s3gen.inference = medir_etapa(
    "Conversao dos tokens em audio", modelo.s3gen.inference
)

decodificar_original = modelo.s3gen.inference

def decodificar_rapido(*args, **kwargs):
    kwargs["n_cfm_timesteps"] = 2
    return decodificar_original(*args, **kwargs)

modelo.s3gen.inference = decodificar_rapido

modelo.watermarker.apply_watermark = medir_etapa(
    "Marca dagua", modelo.watermarker.apply_watermark
)

texto = "Boa noite, senhor. Estou pronto para ajudar com suas tarefas."

for numero in range(1, 4):
    print(f"\nGerando teste {numero}...", flush=True)
    inicio = perf_counter()

    audio = modelo.generate(
        texto,
        language_id="pt",
        exaggeration=0.3,
        cfg_weight=0.5
    )

    tempo = perf_counter() - inicio
    arquivo = pasta / f"teste_int8_{numero}.wav"
    torchaudio.save(str(arquivo), audio, modelo.sr)

    print(f"Geração {numero}: {tempo:.1f}s")
    print(f"Duração do áudio: {audio.shape[-1] / modelo.sr:.1f}s")

os.startfile(str(pasta / "teste_int8_1.wav"))