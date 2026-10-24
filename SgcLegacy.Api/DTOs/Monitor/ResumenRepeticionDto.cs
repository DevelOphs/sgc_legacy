namespace SgcLegacy.Api.DTOs.Monitor;

public class ResumenRepeticionDto
{
    public string Regimen { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public int NumRepeticiones { get; set; }
    public int TotalPeticionesAcumuladas { get; set; }
    public double P50Media { get; set; }
    public string P50Rango { get; set; } = string.Empty;
    public double P95Media { get; set; }
    public string P95Rango { get; set; } = string.Empty;
    public double P99Media { get; set; }
    public double MaxGlobal { get; set; }
    public double TasaErrorMedia { get; set; }
    public double ThroughputMedio { get; set; }
    public List<MetricasCorridaDto> DetalleRepeticiones { get; set; } = new();
}
