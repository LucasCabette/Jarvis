import os
from pathlib import Path
from time import perf_counter

pasta = Path(__file__).resolve().parent
os.environ["HF_HOME"] = str(pasta / "modelos")

import torch
import torchaudio
from chatterbox.mtl_tts import ChatterboxMultilingualTTS

dispositivo = "cuda" if torch.cuda.is_available() else "cpu"
print(f"Executando em: {dispositivo}", flush=True)

modelo = ChatterboxMultilingualTTS.from_pretrained(
    device=dispositivo
)

inicio = perf_counter()
audio = modelo.generate(
    "Boa noite, senhor. Estou pronto para ajudar com suas tarefas.",
    language_id="pt",
    audio_prompt_path=str(pasta / "referencia.wav"),
    exaggeration=0.3,
    cfg_weight=0.5
)
tempo = perf_counter() - inicio

arquivo = pasta / "teste.wav"
torchaudio.save(str(arquivo), audio, modelo.sr)

print(f"Tempo de geração: {tempo:.1f} segundos")
os.startfile(str(arquivo))