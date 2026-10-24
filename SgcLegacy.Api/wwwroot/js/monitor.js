/**
 * Monitor Experimental SGC Legacy - Client-side Controller (JavaScript)
 * Encapsula la comunicación con la API, renderizado del DOM y gestión del gráfico Chart.js.
 */

let chartInstance = null;
let isUpdating = false;

// Inicialización del gráfico Chart.js
function iniciarGrafico() {
    const canvas = document.getElementById('chartLatencia');
    if (!canvas) return;

    const ctx = canvas.getContext('2d');
    chartInstance = new Chart(ctx, {
        type: 'line',
        data: {
            labels: [],
            datasets: [{
                label: 'Latencia HTTP (ms)',
                data: [],
                borderColor: '#38bdf8',
                backgroundColor: 'rgba(56, 189, 248, 0.08)',
                borderWidth: 2,
                tension: 0.25,
                fill: true,
                pointRadius: 4,
                pointHoverRadius: 6,
                pointBackgroundColor: []
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: false,
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    backgroundColor: 'rgba(15, 23, 42, 0.9)',
                    titleColor: '#f8fafc',
                    bodyColor: '#94a3b8',
                    borderColor: 'rgba(51, 65, 85, 0.6)',
                    borderWidth: 1,
                    padding: 8,
                    displayColors: false,
                    callbacks: {
                        label: (ctx) => `Latencia: ${ctx.parsed.y.toFixed(2)} ms`
                    }
                }
            },
            scales: {
                x: {
                    grid: {
                        color: 'rgba(51, 65, 85, 0.2)'
                    },
                    ticks: {
                        color: '#64748b',
                        font: { size: 10 }
                    }
                },
                y: {
                    beginAtZero: true,
                    grid: {
                        color: 'rgba(51, 65, 85, 0.2)'
                    },
                    ticks: {
                        color: '#64748b',
                        font: { size: 10 },
                        callback: (v) => `${v} ms`
                    }
                }
            }
        }
    });
}

// Formateador de badges de módulos
function badgeModulo(mod) {
    switch (mod) {
        case 'Pedidos':
            return `<span class="badge-modulo badge-pedidos">Pedidos</span>`;
        case 'Inventario':
            return `<span class="badge-modulo badge-inventario">Inventario</span>`;
        case 'Comprobantes':
            return `<span class="badge-modulo badge-comprobantes">Comprobantes</span>`;
        default:
            return `<span class="badge-modulo" style="background:#334155;color:#e2e8f0;">${mod}</span>`;
    }
}

// Formateador de régimen de prueba
function badgeRegimen(regimen) {
    if (regimen.includes('CON')) {
        return `<span class="badge-regimen badge-regimen-con">${regimen}</span>`;
    }
    return `<span class="badge-regimen badge-regimen-sin">${regimen}</span>`;
}

// Petición y actualización reactiva del dashboard
async function actualizar() {
    if (isUpdating) return;
    isUpdating = true;

    try {
        const res = await fetch('/api/Monitor/datos-reales');
        if (!res.ok) throw new Error(`HTTP Error ${res.status}`);
        const data = await res.json();

        // 1. Estado y píldora de telemetría
        const estadoElem = document.getElementById('estado-corrida');
        const pulseElem = document.getElementById('pulse-indicator');
        if (estadoElem) {
            estadoElem.innerText = data.estadoCorrida || 'Listo para recibir carga externa.';
            if (pulseElem) {
                if (data.estadoCorrida && data.estadoCorrida.includes('Calentamiento')) {
                    pulseElem.className = 'pulse-dot warning';
                } else {
                    pulseElem.className = 'pulse-dot';
                }
            }
        }

        // 2. Cálculo de KPIs globales
        let totalReqs = 0;
        let sumP50 = 0;
        let sumP95 = 0;
        let sumThroughput = 0;
        let sumError = 0;
        let count = (data.resumenRepeticiones || []).length;

        (data.resumenRepeticiones || []).forEach(r => {
            totalReqs += r.totalPeticionesAcumuladas || 0;
            sumP50 += r.p50Media || 0;
            sumP95 += r.p95Media || 0;
            sumThroughput += r.throughputMedio || 0;
            sumError += r.tasaErrorMedia || 0;
        });

        const kpiPeticiones = document.getElementById('kpi-peticiones');
        const kpiP50 = document.getElementById('kpi-p50');
        const kpiP95 = document.getElementById('kpi-p95');
        const kpiThroughput = document.getElementById('kpi-throughput');

        if (kpiPeticiones) kpiPeticiones.innerText = totalReqs.toLocaleString();
        if (kpiP50) kpiP50.innerText = count > 0 ? `${(sumP50 / count).toFixed(2)} ms` : '--';
        if (kpiP95) kpiP95.innerText = count > 0 ? `${(sumP95 / count).toFixed(2)} ms` : '--';
        if (kpiThroughput) kpiThroughput.innerText = count > 0 ? `${(sumThroughput / count).toFixed(1)} ops/s` : '--';

        // 3. Tabla 1: Resumen por Repeticiones
        const tablaReps = document.getElementById('tabla-repeticiones');
        if (tablaReps) {
            if (!data.resumenRepeticiones || data.resumenRepeticiones.length === 0) {
                tablaReps.innerHTML = `<tr><td colspan="9" class="empty-state">No hay repeticiones grabadas aún. Inicie 'ejecutar_protocolo_carga.py' para registrar datos.</td></tr>`;
            } else {
                tablaReps.innerHTML = data.resumenRepeticiones.map(r => `
                    <tr>
                        <td>${badgeRegimen(r.regimen)}</td>
                        <td class="fw-semibold" style="color:#e2e8f0;">${r.endpoint}</td>
                        <td class="num-cell">${r.numRepeticiones} <small style="color:var(--text-muted);">(${r.totalPeticionesAcumuladas})</small></td>
                        <td class="num-cell" style="color:#34d399;font-weight:600;">${r.p50Media} ms <small style="color:var(--text-muted);font-weight:normal;">[${r.p50Rango}]</small></td>
                        <td class="num-cell" style="color:#fbbf24;font-weight:600;">${r.p95Media} ms <small style="color:var(--text-muted);font-weight:normal;">[${r.p95Rango}]</small></td>
                        <td class="num-cell">${r.p99Media} ms</td>
                        <td class="num-cell">${r.maxGlobal} ms</td>
                        <td class="num-cell ${r.tasaErrorMedia > 0 ? 'text-danger fw-bold' : 'text-success'}">${r.tasaErrorMedia} %</td>
                        <td class="num-cell" style="color:#38bdf8;">${r.throughputMedio}</td>
                    </tr>
                `).join('');
            }
        }

        // 4. Tabla 2: Matriz OE1 Multicriterio
        const tablaOe1 = document.getElementById('tabla-oe1');
        if (tablaOe1) {
            if (!data.matrizPriorizacionOe1 || data.matrizPriorizacionOe1.length === 0) {
                tablaOe1.innerHTML = `<tr><td colspan="9" class="empty-state">A la espera de trazas de PostgreSQL y OpenTelemetry...</td></tr>`;
            } else {
                tablaOe1.innerHTML = data.matrizPriorizacionOe1.map((m, idx) => `
                    <tr>
                        <td class="fw-semibold">
                            <span style="color:var(--text-muted);margin-right:4px;">${idx + 1}.º</span>
                            ${badgeModulo(m.modulo)}
                        </td>
                        <td class="num-cell">${m.ca} / ${m.ce} <small style="color:var(--text-muted);">(I=${m.inestabilidad})</small></td>
                        <td class="num-cell" style="color:var(--accent-blue);">${m.participacionOtelPorc} %</td>
                        <td class="num-cell ${m.tasaLoteSqlCruzadoPorc > 0 ? 'text-danger fw-bold' : 'text-success'}">${m.tasaLoteSqlCruzadoPorc} %</td>
                        <td class="num-cell">${m.puntajeAcoplamiento}</td>
                        <td class="num-cell">${m.puntajeComplejidad}</td>
                        <td class="num-cell">${m.puntajeCriticidad}</td>
                        <td class="num-cell" style="color:#fbbf24;font-weight:700;">${m.indiceRiesgoBase.toFixed(2)}</td>
                        <td class="num-cell" style="color:#34d399;font-weight:700;">${m.indicePrioridad.toFixed(2)}</td>
                    </tr>
                `).join('');
            }
        }

        // 5. Gráfico de Latencias
        if (chartInstance && data.trazasRecientes) {
            const cronologico = [...data.trazasRecientes].reverse();
            chartInstance.data.labels = cronologico.map(t => `R${t.repeticion} ${t.horaLocal}`);
            chartInstance.data.datasets[0].data = cronologico.map(t => t.duracionTotalMs);
            chartInstance.data.datasets[0].pointBackgroundColor = cronologico.map(t =>
                t.statusCode >= 500 ? '#f43f5e' : '#10b981'
            );
            chartInstance.update();
        }
    } catch (err) {
        console.error('Error al actualizar datos del monitor:', err);
    } finally {
        isUpdating = false;
    }
}

// Acción de reinicio completo
async function limpiar() {
    if (!confirm('¿Desea reiniciar todas las trazas de telemetría y métricas acumuladas?')) {
        return;
    }

    try {
        const btn = document.querySelector('.btn-modern-danger');
        if (btn) btn.disabled = true;
        await fetch('/api/Monitor/limpiar', { method: 'POST' });
        await actualizar();
    } catch (err) {
        alert('Error al reiniciar métricas: ' + err.message);
    } finally {
        const btn = document.querySelector('.btn-modern-danger');
        if (btn) btn.disabled = false;
    }
}

// Ciclo de inicialización
document.addEventListener('DOMContentLoaded', () => {
    iniciarGrafico();
    actualizar();
    setInterval(actualizar, 1500);
});
