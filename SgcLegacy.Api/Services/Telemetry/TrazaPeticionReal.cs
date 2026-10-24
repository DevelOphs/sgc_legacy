namespace SgcLegacy.Api.Services.Telemetry;

public class TrazaPeticionReal
{
    public string TraceId { get; set; } = string.Empty;
    public DateTime FechaUtc { get; set; }
    public string HoraLocal { get; set; } = string.Empty;
    public string Regimen { get; set; } = string.Empty;
    public int Repeticion { get; set; }
    public string MetodoHttp { get; set; } = string.Empty;
    public string RutaSinQuery { get; set; } = string.Empty;
    public string RutaUrl { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public double DuracionTotalMs { get; set; }
    public bool EsCalentamiento { get; set; }
    public List<SpanSqlReal> ConsultasSql { get; set; } = new();
    public List<string> ModulosTocados { get; set; } = new();
}
