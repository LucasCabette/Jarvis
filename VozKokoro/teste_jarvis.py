from misaki.espeak import EspeakG2P
from pathlib import Path
from time import perf_counter
import winsound

import soundfile as sf
from kokoro_onnx import Kokoro

pasta = Path(__file__).resolve().parent

print("Carregando modelo...", flush=True)
modelo = Kokoro(
    str(pasta / "kokoro-v1.0.onnx"),
    str(pasta / "voices-v1.0.bin")
)

alex = modelo.get_voice_style("pm_alex")
santa = modelo.get_voice_style("pm_santa")
voz = alex

texto = (
    "Boa noite, senhor. Tudo pronto por aqui. "
    "Certamente, posso verificar isso para o senhor. "
    "Um momento, por favor. "
    "A análise foi concluída. Podemos prosseguir."
)

print("Gerando voz...", flush=True)
inicio = perf_counter()

g2p = EspeakG2P(language="pt-br")
fonemas, _ = g2p(texto)

inicio = perf_counter()

audio, taxa = modelo.create(
    fonemas,
    voice=alex,
    speed=1.0,
    is_phonemes=True
)

tempo = perf_counter() - inicio
arquivo = pasta / "jarvis_teste.wav"
sf.write(str(arquivo), audio, taxa, subtype="PCM_16")

print(f"Tempo de geração: {tempo:.1f}s")
print(f"Duração do áudio: {len(audio) / taxa:.1f}s")

winsound.PlaySound(str(arquivo), winsound.SND_FILENAME)