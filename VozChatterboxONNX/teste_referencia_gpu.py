from pathlib import Path
from time import perf_counter

import librosa
import numpy as np
import onnxruntime as ort
from huggingface_hub import hf_hub_download

pasta = Path(__file__).resolve().parent
repositorio = "onnx-community/chatterbox-multilingual-ONNX"

print("Baixando componente da referência...", flush=True)

for nome in ["speech_encoder.onnx", "speech_encoder.onnx_data"]:
    hf_hub_download(
        repo_id=repositorio,
        filename=f"onnx/{nome}",
        local_dir=str(pasta / "modelos")
    )

opcoes = ort.SessionOptions()
opcoes.enable_mem_pattern = False
opcoes.execution_mode = ort.ExecutionMode.ORT_SEQUENTIAL
opcoes.log_severity_level = 2

print("Carregando componente com DirectML...", flush=True)

sessao = ort.InferenceSession(
    str(pasta / "modelos" / "onnx" / "speech_encoder.onnx"),
    sess_options=opcoes,
    providers=[
        ("DmlExecutionProvider", {"device_id": 0}),
        "CPUExecutionProvider"
    ]
)
sessao.disable_fallback()

audio, _ = librosa.load(
    str(pasta / "referencia.wav"),
    sr=24000,
    mono=True
)
entrada = audio[np.newaxis, :].astype(np.float32)

print("Processando referência...", flush=True)
inicio = perf_counter()
resultados = sessao.run(None, {"audio_values": entrada})

print(f"Referência processada em {perf_counter() - inicio:.1f}s")
print("Formatos das saídas:", [r.shape for r in resultados])
print("Teste do componente concluído.")