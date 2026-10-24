namespace SgcLegacy.Api.Services.Telemetry;

public class SpanSqlReal
{
    public string SpanId { get; set; } = string.Empty;
    public string Sql { get; set; } = string.Empty;
    public double DuracionMs { get; set; }
    public List<string> TablasDetectadas { get; set; } = new();
    public List<string> ModulosDetectados { get; set; } = new();
}
