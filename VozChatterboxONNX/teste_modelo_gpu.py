from pathlib import Path
from time import perf_counter

import onnx
import onnxruntime as ort
from huggingface_hub import hf_hub_download

pasta = Path(__file__).resolve().parent
repositorio = "onnx-community/chatterbox-multilingual-ONNX"

print("Baixando modelo de geração...", flush=True)

for nome in ["language_model.onnx", "language_model.onnx_data"]:
    hf_hub_download(
        repo_id=repositorio,
        filename=f"onnx/{nome}",
        local_dir=str(pasta / "modelos")
    )

arquivo = pasta / "modelos" / "onnx" / "language_model.onnx"

grafo = onnx.load(str(arquivo), load_external_data=False)
print("Opsets:", [(o.domain, o.version) for o in grafo.opset_import])
del grafo

opcoes = ort.SessionOptions()
opcoes.enable_profiling = True
opcoes.enable_mem_pattern = False
opcoes.execution_mode = ort.ExecutionMode.ORT_SEQUENTIAL

print("Carregando modelo com DirectML...", flush=True)
inicio = perf_counter()

sessao = ort.InferenceSession(
    str(arquivo),
    sess_options=opcoes,
    providers=["CPUExecutionProvider"]
)
sessao.disable_fallback()

print(f"Carregamento concluído em {perf_counter() - inicio:.1f}s")
print("Recursos:", sessao.get_providers())
print("Modelo de geração carregado.")

import json
from collections import Counter
import numpy as np

entradas = {
    "inputs_embeds": np.zeros((1, 1, 1024), dtype=np.float32),
    "attention_mask": np.ones((1, 2), dtype=np.int64),
}

for entrada in sessao.get_inputs():
    if entrada.name.startswith("past_key_values."):
        entradas[entrada.name] = np.zeros(
            (1, 16, 1, 64),
            dtype=np.float32
        )

print("Executando um passo de geração...", flush=True)
inicio = perf_counter()
resultados = sessao.run(None, entradas)
print(f"Primeira execução: {perf_counter() - inicio:.2f}s")

perfil = sessao.end_profiling()

with open(perfil, encoding="utf-8") as arquivo_perfil:
    eventos = json.load(arquivo_perfil)

contagem = Counter(
    evento.get("args", {}).get("provider")
    for evento in eventos
    if evento.get("args", {}).get("provider")
)

print("Operações registradas por recurso:", dict(contagem))
print("Passo de geração executado.")