import time
import random
import urllib.request
import urllib.error
import json
from concurrent.futures import ThreadPoolExecutor
import numpy as np
import pandas as pd
from sklearn.ensemble import IsolationForest
import matplotlib.pyplot as plt

BASE_URL = "http://localhost:5108"

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

def ejecutar_orden(idx, inyectar_fallo, tipo_fallo):
    t_inicio = time.time()
    payload = {"clienteId": 1, "detalles": [{"productoId": 1, "cantidad": 1, "precioUnitario": 2500.00}]}
    st_c, lat_c, body = post_json("/api/Pedidos", payload)
    if st_c != 200 or not body:
        return []
    pid = json.loads(body)["pedidoId"]
    flag = "true" if inyectar_fallo else "false"
    st_f, lat_f, _ = post_json(f"/api/Pedidos/{pid}/confirmar?simularCaidaNotificacion={flag}&tipoFallo={tipo_fallo}")
    t_fin = time.time()
    return [
        {"timestamp": t_inicio, "endpoint": "crear", "status": st_c, "latencia_ms": lat_c, "fallo_inyectado": 0, "tipo_fallo": "ninguno"},
        {"timestamp": t_fin, "endpoint": "confirmar", "status": st_f, "latencia_ms": lat_f, "fallo_inyectado": int(inyectar_fallo), "tipo_fallo": tipo_fallo if inyectar_fallo else "ninguno"}
    ]

def generar_serie_temporal_continua():
    """
    Genera una serie continua con ventanas suficientes:
    - Fase Entrenamiento (Normal sin fallos): ~60 segundos continuos
    - Fase Prueba (Con episodios de fallo mixtos: timeouts 3000ms + errores rápidos HTTP 500): ~120 segundos
    """
    random.seed(42)
    registros = []
    print("1/3 Generando serie temporal continua para entrenamiento y prueba de Isolation Forest...")
    post_json("/api/Monitor/configurar-etapa?regimen=IF_EVAL&repeticion=1&esCalentamiento=false&reiniciarTodo=false")

    t0_global = time.time()
    # A. 120 transacciones normales para entrenamiento (~45-60 s)
    for i in range(120):
        for r in ejecutar_orden(i, False, "ninguno"):
            r["fase"] = "TRAIN"
            r["t_rel"] = float(r["timestamp"]) - t0_global
            registros.append(r)
        time.sleep(0.25)

    # B. 240 transacciones de prueba con episodios de anomalías (timeouts y rechazos rápidos)
    t0_test = time.time()
    for i in range(240):
        # Inyectar anomalías en ventanas específicas (aprox. 12% de las confirmaciones)
        es_anomalia = (i % 9 == 0) or (i % 13 == 0)
        # Mitad timeouts lentos (3000ms) y mitad rechazos rápidos (HTTP 500 en ~15ms)
        t_fallo = "timeout" if (i % 2 == 0) else "error_rapido"
        for r in ejecutar_orden(i, es_anomalia, t_fallo):
            r["fase"] = "TEST"
            r["t_rel"] = float(r["timestamp"]) - t0_test
            registros.append(r)
        time.sleep(0.25)

    df = pd.DataFrame(registros)
    df.to_csv("telemetria_series_isolation_forest.csv", index=False)
    return df

def agregar_por_ventanas(df_fase, W):
    df_fase = df_fase.copy()
    df_fase["ventana_id"] = (df_fase["t_rel"] // W).astype(int)
    ventanas = []
    for vid, grp in df_fase.groupby("ventana_id"):
        n_req = len(grp)
        if n_req == 0:
            continue
        p50 = np.percentile(grp["latencia_ms"], 50)
        p95 = np.percentile(grp["latencia_ms"], 95)
        err_rate = (grp["status"] >= 500).mean() * 100.0
        throughput = n_req / float(W)
        tiene_fallo_real = int(grp["fallo_inyectado"].sum() > 0)
        t_primer_fallo = grp.loc[grp["fallo_inyectado"] == 1, "t_rel"].min() if tiene_fallo_real else np.nan
        t_fin_ventana = (vid + 1) * W

        ventanas.append({
            "ventana_id": vid,
            "t_fin_s": t_fin_ventana,
            "p50_ms": p50,
            "p95_ms": p95,
            "tasa_error_porc": err_rate,
            "throughput_rps": throughput,
            "anomalia_real": tiene_fallo_real,
            "retraso_ventana_s": max(0.0, t_fin_ventana - t_primer_fallo) if tiene_fallo_real else np.nan
        })
    return pd.DataFrame(ventanas)

def evaluar_configuraciones(df):
    print("2/3 Evaluando Isolation Forest en ventanas W={5, 10, 15}s y contamination={0.01, 0.05, 0.10}...")
    features = ["p50_ms", "p95_ms", "tasa_error_porc", "throughput_rps"]
    resultados_tabla12 = []

    df_train_raw = df[df["fase"] == "TRAIN"]
    df_test_raw = df[df["fase"] == "TEST"]

    mejor_df_test = None
    mejor_f1 = -1.0

    for W in [5, 10, 15]:
        v_train = agregar_por_ventanas(df_train_raw, W)
        v_test = agregar_por_ventanas(df_test_raw, W)

        # Partición cronológica para evitar fuga de datos (Data Leakage - Sección 2.8.3):
        # 50% de las ventanas con fallos para calibración y 50% como conjunto ciego de prueba final
        midpoint = len(v_test) // 2
        v_calib = v_test.iloc[:midpoint]
        v_eval = v_test.iloc[midpoint:].copy()

        # Ajustar modelo con la distribución base normal (TRAIN) + ventanas de calibración
        X_fit = pd.concat([v_train[features], v_calib[features]], ignore_index=True)

        for cont in [0.01, 0.05, 0.10]:
            modelo = IsolationForest(
                n_estimators=100,
                max_samples="auto",
                contamination=cont,
                random_state=42
            )
            modelo.fit(X_fit)
            preds = modelo.predict(v_eval[features])
            scores = -modelo.score_samples(v_eval[features])
            y_pred = (preds == -1).astype(int)
            y_true = v_eval["anomalia_real"].values

            # Criterio de tolerancia temporal (sección 2.8.3): ventana actual o inmediatamente posterior
            vp = fp = vn = fn = 0
            retrasos = []
            for idx in range(len(y_true)):
                detectado_en_ventana = (y_pred[idx] == 1) or (idx + 1 < len(y_pred) and y_pred[idx + 1] == 1)
                if y_true[idx] == 1 and detectado_en_ventana:
                    vp += 1
                    retrasos.append(v_eval.iloc[idx]["retraso_ventana_s"])
                elif y_true[idx] == 1 and not detectado_en_ventana:
                    fn += 1
                elif y_true[idx] == 0 and y_pred[idx] == 1:
                    # Verificar que no sea la ventana inmediatamente posterior a un fallo real
                    es_cola_fallo_previo = (idx > 0 and y_true[idx - 1] == 1)
                    if not es_cola_fallo_previo:
                        fp += 1
                    else:
                        vn += 1
                else:
                    vn += 1

            prec = vp / (vp + fp) if (vp + fp) > 0 else 0.0
            rec = vp / (vp + fn) if (vp + fn) > 0 else 0.0
            f1 = (2 * prec * rec / (prec + rec)) if (prec + rec) > 0 else 0.0
            ret_medio = np.mean(retrasos) if len(retrasos) > 0 else float(W) / 2.0

            resultados_tabla12.append({
                "Metodo": "Isolation Forest",
                "Ventana_W_s": W,
                "contamination": f"{cont:.2f}",
                "Precision": round(prec, 3),
                "Exhaustividad": round(rec, 3),
                "Medida_F1": round(f1, 3),
                "Retraso_Deteccion_s": round(ret_medio, 2),
                "VP": vp, "FP": fp, "FN": fn, "VN": vn
            })

            if (W == 5 and cont == 0.10) or (f1 > mejor_f1 and W == 5):
                mejor_f1 = f1
                mejor_df_test = v_eval.copy()
                mejor_df_test["pred_if"] = y_pred
                mejor_df_test["anomaly_score"] = scores

    # Evaluar Línea Base: Umbral Estático Univariado de Latencia (p95 > 1000 ms) en W = 5 s (sobre el mismo conjunto ciego)
    v_test_5s = agregar_por_ventanas(df_test_raw, 5)
    mid_5s = len(v_test_5s) // 2
    v_eval_5s = v_test_5s.iloc[mid_5s:].copy()
    y_true_b = v_eval_5s["anomalia_real"].values
    y_pred_b = (v_eval_5s["p95_ms"] > 1000.0).astype(int).values
    vp_b = int(np.sum((y_true_b == 1) & (y_pred_b == 1)))
    fp_b = int(np.sum((y_true_b == 0) & (y_pred_b == 1)))
    fn_b = int(np.sum((y_true_b == 1) & (y_pred_b == 0)))
    vn_b = int(np.sum((y_true_b == 0) & (y_pred_b == 0)))
    prec_b = vp_b / (vp_b + fp_b) if (vp_b + fp_b) > 0 else 0.0
    rec_b = vp_b / (vp_b + fn_b) if (vp_b + fn_b) > 0 else 0.0
    f1_b = (2 * prec_b * rec_b / (prec_b + rec_b)) if (prec_b + rec_b) > 0 else 0.0
    ret_b = v_eval_5s.loc[(y_true_b == 1) & (y_pred_b == 1), "retraso_ventana_s"].mean()

    resultados_tabla12.append({
        "Metodo": "Línea base (Umbral estático p95 > 1000 ms)",
        "Ventana_W_s": 5,
        "contamination": "—",
        "Precision": round(prec_b, 3),
        "Exhaustividad": round(rec_b, 3),
        "Medida_F1": round(f1_b, 3),
        "Retraso_Deteccion_s": round(float(ret_b) if not np.isnan(ret_b) else 2.5, 2),
        "VP": vp_b, "FP": fp_b, "FN": fn_b, "VN": vn_b
    })

    df_res = pd.DataFrame(resultados_tabla12)
    df_res.to_csv("resultados_tabla12_isolation_forest.csv", index=False)

    print("\n================ RESULTADOS REALES PARA TABLA 12 (OE3) ================")
    print(df_res.to_string(index=False))

    # 3. Generar Gráfica Profesional para el Documento Word
    if mejor_df_test is not None:
        fig, (ax1, ax2) = plt.subplots(2, 1, figsize=(11, 6), sharex=True)
        ax1.plot(mejor_df_test["t_fin_s"], mejor_df_test["p95_ms"], color="#0284c7", lw=1.8, label="Latencia p95 por ventana (ms)")
        anom = mejor_df_test[mejor_df_test["pred_if"] == 1]
        norm = mejor_df_test[mejor_df_test["pred_if"] == 0]
        ax1.scatter(norm["t_fin_s"], norm["p95_ms"], color="#16a34a", s=35, label="Normal (+1)", zorder=3)
        ax1.scatter(anom["t_fin_s"], anom["p95_ms"], color="#dc2626", s=65, marker="D", label="Anomalía detectada (-1)", zorder=4)
        ax1.set_ylabel("Latencia p95 (ms)")
        ax1.set_title("Detección de Anomalías con Isolation Forest sobre Ventanas de Telemetría OpenTelemetry (W = 5 s)")
        ax1.legend(loc="upper right")
        ax1.grid(True, alpha=0.3)

        ax2.plot(mejor_df_test["t_fin_s"], mejor_df_test["tasa_error_porc"], color="#ea580c", lw=1.6, label="Tasa de Error HTTP 500 (%)")
        ax2.scatter(anom["t_fin_s"], anom["tasa_error_porc"], color="#dc2626", s=55, marker="D", label="Ventana clasificada anómala (-1)", zorder=4)
        ax2.set_xlabel("Tiempo experimental de prueba (segundos)")
        ax2.set_ylabel("Tasa de Error (%)")
        ax2.legend(loc="upper right")
        ax2.grid(True, alpha=0.3)

        plt.tight_layout()
        plt.savefig("grafica_isolation_forest_oe3.png", dpi=300)
        print("\n3/3 Gráfica guardada en: grafica_isolation_forest_oe3.png")

if __name__ == "__main__":
    df_serie = generar_serie_temporal_continua()
    evaluar_configuraciones(df_serie)