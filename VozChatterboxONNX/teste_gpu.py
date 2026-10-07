from pathlib import Path
import numpy as np
import onnx
from onnx import helper, TensorProto
import onnxruntime as ort

pasta = Path(__file__).resolve().parent

entrada = helper.make_tensor_value_info(
    "entrada", TensorProto.FLOAT, [2, 2]
)
saida = helper.make_tensor_value_info(
    "saida", TensorProto.FLOAT, [2, 2]
)

operacao = helper.make_node(
    "MatMul", ["entrada", "entrada"], ["saida"]
)

grafo = helper.make_graph(
    [operacao], "teste_gpu", [entrada], [saida]
)

modelo = helper.make_model(
    grafo,
    opset_imports=[helper.make_opsetid("", 17)]
)
modelo.ir_version = 8
onnx.save(modelo, str(pasta / "teste_gpu.onnx"))

opcoes = ort.SessionOptions()
opcoes.enable_mem_pattern = False
opcoes.execution_mode = ort.ExecutionMode.ORT_SEQUENTIAL
opcoes.add_session_config_entry(
    "session.disable_cpu_ep_fallback", "1"
)

sessao = ort.InferenceSession(
    str(pasta / "teste_gpu.onnx"),
    sess_options=opcoes,
    providers=[
        ("DmlExecutionProvider", {"device_id": 0})
    ]
)
sessao.disable_fallback()

dados = np.array([[1, 2], [3, 4]], dtype=np.float32)
resultado = sessao.run(None, {"entrada": dados})[0]

np.testing.assert_allclose(
    resultado,
    [[7, 10], [15, 22]]
)

print("Teste de cálculo com DirectML passou.")