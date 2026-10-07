from pathlib import Path
from time import perf_counter
import winsound

import soundfile as sf
from kokoro_onnx import Kokoro

pasta = Path(__file__).resolve().parent

print("Carregando modelo...", flush=True)
inicio = perf_counter()

modelo = Kokoro(
    str(pasta / "kokoro-v1.0.onnx"),
    str(pasta / "voices-v1.0.bin")
)

print(f"Carregamento: {perf_counter() - inicio:.1f}s")

texto = "Boa noite, senhor. Estou pronto para ajudar com suas tarefas."

for voz in ["pm_alex", "pm_santa"]:
    print(f"\nGerando voz: {voz}", flush=True)
    inicio = perf_counter()

    audio, taxa = modelo.create(
        texto,
        voice=voz,
        speed=1.0,
        lang="pt-br"
    )

    tempo = perf_counter() - inicio
    arquivo = pasta / f"teste_{voz}.wav"
    sf.write(str(arquivo), audio, taxa, subtype="PCM_16")

    print(f"Tempo de geração: {tempo:.1f}s")
    print(f"Duração do áudio: {len(audio) / taxa:.1f}s")
    print(f"Reproduzindo {voz}...", flush=True)

    winsound.PlaySound(str(arquivo), winsound.SND_FILENAME)