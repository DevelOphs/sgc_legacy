import csv
import time
import random
import urllib.request
import urllib.error
import json
from concurrent.futures import ThreadPoolExecutor, as_completed

BASE_URL = "http://localhost:5108"
CONCURRENCIA = 4
TRANSACCIONES_WARMUP = 20
TRANSACCIONES_POR_REPETICION = 50
REPETICIONES = 3

def post_json(ruta, payload=None):
    url = f"{BASE_URL}{ruta}"
    data = json.dumps(payload).encode("utf-8") if payload else b""
    req = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"}, method="POST")
    t0 = time.perf_counter()
    try:
        with urllib.request.urlopen(req, timeout=15) as resp:
            body = resp.read().decode("utf-8")
            return resp.status, (time.perf_counter() - t0) * 1000.0, body
    except urllib.error.HTTPError as e:
        return e.code, (time.perf_counter() - t0) * 1000.0, ""
    except Exception:
        return 500, (time.perf_counter() - t0) * 1000.0, ""

def ejecutar_transaccion(indice, inyectar_fallo_bool):
    # 1. Crear pedido usando el precio base consistente con el catálogo de productos
    payload = {"clienteId": 1, "detalles": [{"productoId": 1, "cantidad": 1, "precioUnitario": 2500.00}]}
    status_crear, lat_crear, body = post_json("/api/Pedidos", payload)
    if status_crear != 200 or not body:
        return []
    pedido_id = json.loads(body)["pedidoId"]

    # 2. Confirmar pedido (si hay fallo, alterna entre timeout de 3000ms y error rápido sin espera)
    flag = "true" if inyectar_fallo_bool else "false"
    tipo_fallo = "timeout" if (indice % 2 == 0) else "error_rapido"
    ruta_conf = f"/api/Pedidos/{pedido_id}/confirmar?simularCaidaNotificacion={flag}&tipoFallo={tipo_fallo}"
    status_conf, lat_conf, _ = post_json(ruta_conf)
    return [
        ("POST /api/Pedidos", status_crear, round(lat_crear, 2), False),
        ("POST /api/Pedidos/{id}/confirmar", status_conf, round(lat_conf, 2), inyectar_fallo_bool)
    ]

def correr_protocolo():
    random.seed(42)  # Semilla fija para reproducibilidad científica
    filas_csv = []

    # 1. Calentamiento (20 transacciones concurrentes descartadas)
    print(f"Iniciando calentamiento ({TRANSACCIONES_WARMUP} transacciones)...")
    post_json(f"/api/Monitor/configurar-etapa?regimen=WARMUP&repeticion=0&esCalentamiento=true&reiniciarTodo=true")
    with ThreadPoolExecutor(max_workers=CONCURRENCIA) as ex:
        list(ex.map(lambda i: ejecutar_transaccion(i, False), range(TRANSACCIONES_WARMUP)))

    # 2. Ejecutar los 2 regímenes con 3 repeticiones cada uno
    for regimen, prob_fallo in [("SIN_FALLOS_0%", 0.0), ("CON_FALLOS_10%", 0.10)]:
        for rep in range(1, REPETICIONES + 1):
            print(f"Ejecutando {regimen} - Repeticion {rep}/{REPETICIONES}...")
            post_json(f"/api/Monitor/configurar-etapa?regimen={regimen}&repeticion={rep}&esCalentamiento=false&reiniciarTodo=false")

            # Selección pseudoaleatoria sin patrón periódico fijo
            vector_fallos = [random.random() < prob_fallo for _ in range(TRANSACCIONES_POR_REPETICION)]
            if prob_fallo > 0 and sum(vector_fallos) == 0:
                vector_fallos[0] = True

            with ThreadPoolExecutor(max_workers=CONCURRENCIA) as ex:
                futuros = [ex.submit(ejecutar_transaccion, i, vector_fallos[i]) for i in range(TRANSACCIONES_POR_REPETICION)]
                for fut in as_completed(futuros):
                    for endpoint, status, lat_ms, fallo_iny in fut.result():
                        filas_csv.append([regimen, rep, endpoint, status, lat_ms, fallo_iny, time.strftime("%H:%M:%S")])

    with open("corridas_linea_base_local.csv", "w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow(["regimen", "repeticion", "endpoint", "status_code", "latencia_cliente_ms", "fallo_inyectado", "hora"])
        writer.writerows(filas_csv)

    print("Protocolo finalizado. Archivo corridas_linea_base_local.csv guardado y panel /monitor actualizado.")

if __name__ == "__main__":
    correr_protocolo()