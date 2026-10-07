"""Teste local: Chatterbox ONNX, tokens na CPU e audio com DirectML."""
import os
from pathlib import Path
from time import perf_counter
import importlib.util
import winsound

PASTA = Path(__file__).resolve().parent
os.environ["HF_HOME"] = str(PASTA / "cache_hf")

import onnxruntime as ort
import soundfile as sf
from transformers import AutoTokenizer

REFERENCIA = PASTA / "referencia.wav"
ORIGEM = PASTA / "chatterbox_multi_inference_script.py"
SAIDA = PASTA / "teste_onnx.wav"
if not REFERENCIA.is_file() or not ORIGEM.is_file():
    raise SystemExit("Falta referencia.wav ou chatterbox_multi_inference_script.py nesta pasta.")

construtor_original = ort.InferenceSession
sessoes = []


class SessaoMedida:
    def __init__(self, caminho, *args, **kwargs):
        self.caminho = str(caminho)
        self.nome = Path(caminho).stem
        self.tempo = 0.0
        self.chamadas = 0
        # Evita transferencias entre CPU e GPU a cada token.
        self.gpu = False
        self.sessao = self.carregar(self.gpu)
        sessoes.append(self)

    def carregar(self, gpu):
        opcoes = ort.SessionOptions()
        opcoes.enable_mem_pattern = False
        opcoes.execution_mode = ort.ExecutionMode.ORT_SEQUENTIAL
        recursos = ["CPUExecutionProvider"]
        if gpu:
            recursos.insert(0, ("DmlExecutionProvider", {"device_id": 0}))
        print(f"Carregando {self.nome}: {'DirectML + CPU' if gpu else 'CPU'}", flush=True)
        try:
            sessao = construtor_original(
                self.caminho, sess_options=opcoes, providers=recursos
            )
            sessao.disable_fallback()
            return sessao
        except Exception as erro:
            if not gpu:
                raise
            print(f"DirectML falhou ao carregar {self.nome}: {erro}", flush=True)
            self.gpu = False
            return self.carregar(False)

    def run(self, *args, **kwargs):
        inicio = perf_counter()
        try:
            try:
                resultado = self.sessao.run(*args, **kwargs)
            except Exception as erro:
                if not self.gpu:
                    raise
                print(f"DirectML falhou em {self.nome}; repetindo na CPU: {erro}", flush=True)
                self.sessao = None
                self.gpu = False
                self.sessao = self.carregar(False)
                resultado = self.sessao.run(*args, **kwargs)
            return resultado
        finally:
            self.tempo += perf_counter() - inicio
            self.chamadas += 1

    def __getattr__(self, nome):
        return getattr(self.sessao, nome)


spec = importlib.util.spec_from_file_location("exemplo_chatterbox", ORIGEM)
exemplo = importlib.util.module_from_spec(spec)
spec.loader.exec_module(exemplo)
# Portugues nao precisa da preparacao adicional de outros idiomas.
exemplo.prepare_language = lambda texto, idioma: f"[{idioma}]{texto}"

print("Preparando tokenizer...", flush=True)
AutoTokenizer.from_pretrained("onnx-community/chatterbox-multilingual-ONNX")
exemplo.onnxruntime.InferenceSession = SessaoMedida

inicio = perf_counter()
exemplo.run_inference(
    text="Boa noite, senhor. Estou pronto para ajudar com suas tarefas.",
    language_id="pt",
    target_voice_path=str(REFERENCIA),
    max_new_tokens=512,
    exaggeration=0.3,
    output_dir=str(PASTA / "modelos"),
    output_file_name=str(SAIDA),
    apply_watermark=False,
)
total = perf_counter() - inicio
print("\nTempos das operacoes do modelo:", flush=True)
for sessao in sessoes:
    print(f"{sessao.nome}: {sessao.tempo:.2f}s ({sessao.chamadas} chamadas)")
print(f"Soma das operacoes: {sum(s.tempo for s in sessoes):.2f}s")
print(f"Tempo total, incluindo downloads e carregamento: {total:.2f}s")
audio, taxa = sf.read(str(SAIDA))
print(f"Duracao do audio: {len(audio) / taxa:.2f}s")
print(f"Arquivo: {SAIDA}")
winsound.PlaySound(str(SAIDA), winsound.SND_FILENAME)
