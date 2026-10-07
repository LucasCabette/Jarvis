from pathlib import Path
import os
import librosa
import soundfile as sf

pasta = Path(__file__).resolve().parent

audio, taxa = sf.read(str(pasta / "teste.wav"))

grave = librosa.effects.pitch_shift(
    y=audio,
    sr=taxa,
    n_steps=-1.0
)

arquivo = pasta / "teste_grave.wav"
sf.write(str(arquivo), grave, taxa, subtype="PCM_16")
os.startfile(str(arquivo))