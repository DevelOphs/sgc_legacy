using System.Data;
using Microsoft.EntityFrameworkCore;
using SgcLegacy.Api.DTOs.Monitor;
using SgcLegacy.Api.Models;
using SgcLegacy.Api.Services.Contracts;
using SgcLegacy.Api.Services.Telemetry;

namespace SgcLegacy.Api.Services.Implementations;

public class MonitorService : IMonitorService
{
    private readonly LegacyDbContext _context;

    public MonitorService(LegacyDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardDataDto> ObtenerDatosDashboardAsync()
    {
        var trazas = ColectorTelemetriaReal.ObtenerTrazas()
            .Where(t => !t.EsCalentamiento)
            .ToList();

        // 1. Extraer FKs reales desde PostgreSQL information_schema
        var relacionesFk = await ExtraerRelacionesFkAsync();

        // 2. Calcular Matriz OE1 de forma 100% determinista a partir de FKs + Trazas OTel
        var matrizOe1 = CalcularMatrizOe1(relacionesFk, trazas);

        // 3. Agrupar métricas por Régimen, Endpoint y Repetición (Media y Rango [Mín-Máx])
        var resumenRepeticiones = ConstruirResumenPorRepeticiones(trazas);

        return new DashboardDataDto
        {
            EstadoCorrida = ColectorTelemetriaReal.EstadoActual,
            RegimenActivo = ColectorTelemetriaReal.RegimenActual,
            RepeticionActiva = ColectorTelemetriaReal.RepeticionActual,
            ResumenRepeticiones = resumenRepeticiones,
            MatrizPriorizacionOe1 = matrizOe1,
            RelacionesBaseDatos = relacionesFk,
            TrazasRecientes = trazas.OrderByDescending(t => t.FechaUtc).Take(25).ToList()
        };
    }

    public async Task<string> ConfigurarEtapaAsync(string regimen, int repeticion, bool esCalentamiento, bool reiniciarTodo)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE inventario SET stockdisponible = 500000, ultimaactualizacion = NOW();");

        if (reiniciarTodo)
            ColectorTelemetriaReal.Limpiar();

        ColectorTelemetriaReal.RegimenActual = regimen;
        ColectorTelemetriaReal.RepeticionActual = repeticion;
        ColectorTelemetriaReal.ModoCalentamiento = esCalentamiento;
        ColectorTelemetriaReal.EstadoActual = esCalentamiento
            ? $"[{regimen}] Ejecutando Calentamiento (descartado de métricas)..."
            : $"[{regimen}] Grabando Repetición #{repeticion}...";

        return ColectorTelemetriaReal.EstadoActual;
    }

    public void LimpiarTrazas()
    {
        ColectorTelemetriaReal.Limpiar();
        ColectorTelemetriaReal.EstadoActual = "Listo para recibir carga externa.";
    }

    private async Task<List<RelacionFkDto>> ExtraerRelacionesFkAsync()
    {
        var relacionesFk = new List<RelacionFkDto>();
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT 
                tc.table_name AS tabla_origen,
                kcu.column_name AS columna_fk,
                ccu.table_name AS tabla_destino
            FROM information_schema.table_constraints AS tc
            JOIN information_schema.key_column_usage AS kcu
              ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
            JOIN information_schema.constraint_column_usage AS ccu
              ON ccu.constraint_name = tc.constraint_name AND ccu.table_schema = tc.table_schema
            WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_schema = 'public'
            ORDER BY tc.table_name;";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string tOrigen = reader.GetString(0);
            string colFk = reader.GetString(1);
            string tDestino = reader.GetString(2);
            string modOrigen = ModuloClassifier.ClasificarModulo(tOrigen);
            string modDestino = ModuloClassifier.ClasificarModulo(tDestino);

            relacionesFk.Add(new RelacionFkDto
            {
                TablaOrigen = tOrigen,
                ColumnaFk = colFk,
                TablaDestino = tDestino,
                ModuloOrigen = modOrigen,
                ModuloDestino = modDestino,
                EsExterna = modOrigen != modDestino
            });
        }

        return relacionesFk;
    }

    private static List<MatrizOe1Dto> CalcularMatrizOe1(List<RelacionFkDto> relacionesFk, List<TrazaPeticionReal> trazas)
    {
        var modulosNombres = new[] { "Comprobantes", "Inventario", "Pedidos" };
        int totalTrazasValidas = trazas.Count;
        var todosLosSpansSql = trazas.SelectMany(t => t.ConsultasSql).ToList();

        return modulosNombres.Select(mod =>
        {
            // Métricas estáticas (PostgreSQL)
            int ca = relacionesFk.Where(r => r.EsExterna && r.ModuloDestino == mod).Select(r => r.ModuloOrigen).Distinct().Count();
            int ce = relacionesFk.Where(r => r.EsExterna && r.ModuloOrigen == mod).Select(r => r.ModuloDestino).Distinct().Count();
            double inestabilidad = (ca + ce) > 0 ? Math.Round((double)ce / (ca + ce), 2) : 0.0;

            // Métricas dinámicas reales (OpenTelemetry + Npgsql)
            int trazasQueTocanModulo = trazas.Count(t => t.ModulosTocados.Contains(mod));
            double participacionOtelPorc = totalTrazasValidas > 0
                ? Math.Round((double)trazasQueTocanModulo / totalTrazasValidas * 100.0, 1)
                : 0.0;

            var spansEscrituraModulo = todosLosSpansSql
                .Where(s => s.ModulosDetectados.Contains(mod) &&
                           (s.Sql.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase) ||
                            s.Sql.Contains("INSERT ", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            int spansEnLoteCruzado = spansEscrituraModulo.Count(s => s.ModulosDetectados.Count > 1);
            double tasaLoteSqlCruzadoPorc = spansEscrituraModulo.Count > 0
                ? Math.Round((double)spansEnLoteCruzado / spansEscrituraModulo.Count * 100.0, 1)
                : 0.0;

            // Identificar si el módulo inicia las trazas transaccionales (primer span SQL de la traza)
            bool esCoordinadorTransaccional = trazas.Any(t =>
                t.RutaSinQuery.EndsWith("/confirmar") &&
                t.ConsultasSql.FirstOrDefault()?.ModulosDetectados.Contains(mod) == true);

            // APLICACIÓN ALGORÍTMICA DE LA RÚBRICA (Sección 2.7 - Tabla 5B)
            // Acoplamiento (x_i1): 1 base + Ca (máx 2) + (Ce >= 1 ? 1 : 0) + (Participación OTel == 100% ? 1 : 0)
            int xAcoplamiento = Math.Clamp(
                1 + Math.Min(ca, 2) + (ce >= 1 ? 1 : 0) + (participacionOtelPorc >= 99.0 ? 1 : 0), 1, 5);

            // Complejidad (x_i2): 1 base + (Ce >= 1 ? 1 : 0) + (TasaLoteCruzado > 0 ? 1 : 0) + (Ciclo Ca>=1 && Ce>=1 ? 1 : 0) + (Coordinador ? 1 : 0)
            bool tieneCicloDatos = ca >= 1 && ce >= 1;
            int xComplejidad = Math.Clamp(
                1 + (ce >= 1 ? 1 : 0) + (tasaLoteSqlCruzadoPorc > 0 ? 1 : 0) + (tieneCicloDatos ? 1 : 0) + (esCoordinadorTransaccional ? 1 : 0), 1, 5);

            // Criticidad intrínseca del dominio
            (bool SoporteDocumental,
             bool OperacionSincronaNoDiferible,
             bool CustodiaSaldosFisicos,
             bool CapturaOrdenesVenta) reglasDominio = mod switch
             {
                 "Comprobantes" => (true, false, false, false),
                 "Inventario" => (true, true, true, false),
                 "Pedidos" => (true, true, false, true),
                 _ => (false, false, false, false)
             };

            int xCriticidad = Math.Clamp(
                1
                + (reglasDominio.SoporteDocumental ? 1 : 0)
                + (reglasDominio.OperacionSincronaNoDiferible ? 1 : 0)
                + (reglasDominio.CustodiaSaldosFisicos ? 1 : 0)
                + (reglasDominio.CapturaOrdenesVenta ? 1 : 0),
                1,
                5
            );

            double irBase = Math.Round(0.40 * xAcoplamiento + 0.35 * xComplejidad + 0.25 * xCriticidad, 2);
            double ipBase = Math.Round(6.0 - irBase, 2);

            return new MatrizOe1Dto
            {
                Modulo = mod,
                Ca = ca,
                Ce = ce,
                Inestabilidad = inestabilidad,
                ParticipacionOtelPorc = participacionOtelPorc,
                TasaLoteSqlCruzadoPorc = tasaLoteSqlCruzadoPorc,
                PuntajeAcoplamiento = xAcoplamiento,
                PuntajeComplejidad = xComplejidad,
                PuntajeCriticidad = xCriticidad,
                IndiceRiesgoBase = irBase,
                IndicePrioridad = ipBase
            };
        }).OrderBy(m => m.IndiceRiesgoBase).ToList();
    }

    private static List<ResumenRepeticionDto> ConstruirResumenPorRepeticiones(List<TrazaPeticionReal> trazas)
    {
        var resultado = new List<ResumenRepeticionDto>();
        var regimenes = trazas.Select(t => t.Regimen).Distinct().OrderBy(r => r).ToList();

        foreach (var reg in regimenes)
        {
            var trazasReg = trazas.Where(t => t.Regimen == reg).ToList();
            var endpoints = new (string Nombre, Func<TrazaPeticionReal, bool> Filtro)[]
            {
                ("POST /api/Pedidos (Creación)", t => !t.RutaSinQuery.EndsWith("/confirmar")),
                ("POST /api/Pedidos/{id}/confirmar", t => t.RutaSinQuery.EndsWith("/confirmar"))
            };

            foreach (var ep in endpoints)
            {
                var porRepeticion = trazasReg
                    .Where(ep.Filtro)
                    .GroupBy(t => t.Repeticion)
                    .OrderBy(g => g.Key)
                    .Select(g => CalcularMetricasCorrida(g.Key, g.ToList()))
                    .ToList();

                if (porRepeticion.Count == 0) continue;

                resultado.Add(new ResumenRepeticionDto
                {
                    Regimen = reg,
                    Endpoint = ep.Nombre,
                    NumRepeticiones = porRepeticion.Count,
                    TotalPeticionesAcumuladas = porRepeticion.Sum(r => r.Total),
                    P50Media = Math.Round(porRepeticion.Average(r => r.P50), 2),
                    P50Rango = $"{porRepeticion.Min(r => r.P50):F2} – {porRepeticion.Max(r => r.P50):F2}",
                    P95Media = Math.Round(porRepeticion.Average(r => r.P95), 2),
                    P95Rango = $"{porRepeticion.Min(r => r.P95):F2} – {porRepeticion.Max(r => r.P95):F2}",
                    P99Media = Math.Round(porRepeticion.Average(r => r.P99), 2),
                    MaxGlobal = Math.Round(porRepeticion.Max(r => r.Max), 2),
                    TasaErrorMedia = Math.Round(porRepeticion.Average(r => r.TasaError), 2),
                    ThroughputMedio = Math.Round(porRepeticion.Average(r => r.Throughput), 2),
                    DetalleRepeticiones = porRepeticion
                });
            }
        }
        return resultado;
    }

    private static MetricasCorridaDto CalcularMetricasCorrida(int rep, List<TrazaPeticionReal> lista)
    {
        var lat = lista.Select(t => t.DuracionTotalMs).OrderBy(x => x).ToList();
        int total = lista.Count;
        int err = lista.Count(t => t.StatusCode >= 500);
        double seg = total > 1 ? Math.Max((lista.Max(t => t.FechaUtc) - lista.Min(t => t.FechaUtc)).TotalSeconds, 0.5) : 1.0;

        return new MetricasCorridaDto
        {
            Repeticion = rep,
            Total = total,
            Errores = err,
            TasaError = total > 0 ? Math.Round((double)err / total * 100.0, 2) : 0.0,
            P50 = Math.Round(CalcularPercentil(lat, 0.50), 2),
            P95 = Math.Round(CalcularPercentil(lat, 0.95), 2),
            P99 = Math.Round(CalcularPercentil(lat, 0.99), 2),
            Max = lat.Count > 0 ? Math.Round(lat.Last(), 2) : 0.0,
            Throughput = Math.Round(total / seg, 2)
        };
    }

    private static double CalcularPercentil(List<double> ordenados, double percentil)
    {
        if (ordenados.Count == 0) return 0;
        int n = ordenados.Count;
        double pos = percentil * (n - 1);
        int inf = (int)Math.Floor(pos);
        int sup = (int)Math.Ceiling(pos);
        if (inf == sup) return ordenados[inf];
        return ordenados[inf] + (pos - inf) * (ordenados[sup] - ordenados[inf]);
    }
}
