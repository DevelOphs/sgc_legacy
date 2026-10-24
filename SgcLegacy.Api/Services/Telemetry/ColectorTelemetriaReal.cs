using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SgcLegacy.Api.Services.Telemetry;

public static class ColectorTelemetriaReal
{
    private static readonly ConcurrentDictionary<string, List<SpanSqlReal>> _spansSqlPorTrace = new();
    private static readonly ConcurrentQueue<TrazaPeticionReal> _trazasCompletas = new();
    private static bool _iniciado = false;

    public static volatile bool ModoCalentamiento = false;
    public static volatile string RegimenActual = "SIN_FALLOS";
    public static volatile int RepeticionActual = 1;
    public static volatile string EstadoActual = "Listo para recibir carga externa.";

    private static readonly string[] TablasConocidas = [
        "detalle_pedido", "movimiento_inventario", "pedido", "inventario", "comprobante", "notificacion", "cliente", "producto"
    ];

    public static void InicializarEscucha()
    {
        if (_iniciado) return;
        _iniciado = true;

        ActivitySource.AddActivityListener(new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore" || source.Name == "Npgsql",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (ModoCalentamiento) return;

                string traceId = activity.TraceId.ToString();
                if (activity.Source.Name == "Npgsql")
                {
                    string sql = activity.GetTagItem("db.query.text")?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(sql)) return;

                    var tablas = TablasConocidas.Where(t => Regex.IsMatch(sql.ToLowerInvariant(), $@"\b{t}\b")).ToList();
                    var modulos = tablas.Select(ModuloClassifier.ClasificarModulo).Distinct().ToList();

                    var lista = _spansSqlPorTrace.GetOrAdd(traceId, _ => new List<SpanSqlReal>());
                    lock (lista)
                    {
                        lista.Add(new SpanSqlReal
                        {
                            SpanId = activity.SpanId.ToString(),
                            Sql = sql.Trim(),
                            DuracionMs = Math.Round(activity.Duration.TotalMilliseconds, 3),
                            TablasDetectadas = tablas,
                            ModulosDetectados = modulos
                        });
                    }
                }
                else if (activity.Source.Name == "Microsoft.AspNetCore")
                {
                    string path = activity.GetTagItem("url.path")?.ToString() ?? "";
                    if (!path.StartsWith("/api/Pedidos", StringComparison.OrdinalIgnoreCase)) return;

                    string query = activity.GetTagItem("url.query")?.ToString() ?? "";
                    int statusCode = int.TryParse(activity.GetTagItem("http.response.status_code")?.ToString(), out int sc) ? sc : 200;

                    _spansSqlPorTrace.TryRemove(traceId, out var consultas);
                    consultas ??= new List<SpanSqlReal>();

                    _trazasCompletas.Enqueue(new TrazaPeticionReal
                    {
                        TraceId = traceId,
                        FechaUtc = DateTime.UtcNow,
                        HoraLocal = DateTime.Now.ToString("HH:mm:ss"),
                        Regimen = RegimenActual,
                        Repeticion = RepeticionActual,
                        MetodoHttp = activity.GetTagItem("http.request.method")?.ToString() ?? "POST",
                        RutaSinQuery = path,
                        RutaUrl = path + query,
                        StatusCode = statusCode,
                        DuracionTotalMs = Math.Round(activity.Duration.TotalMilliseconds, 2),
                        EsCalentamiento = ModoCalentamiento,
                        ConsultasSql = consultas,
                        ModulosTocados = consultas.SelectMany(c => c.ModulosDetectados).Distinct().ToList()
                    });
                }
            }
        });
    }

    public static List<TrazaPeticionReal> ObtenerTrazas() => _trazasCompletas.ToList();

    public static void Limpiar()
    {
        _trazasCompletas.Clear();
        _spansSqlPorTrace.Clear();
    }
}
