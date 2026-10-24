namespace SgcLegacy.Api.DTOs.Monitor;

public class MetricasCorridaDto
{
    public int Repeticion { get; set; }
    public int Total { get; set; }
    public int Errores { get; set; }
    public double TasaError { get; set; }
    public double P50 { get; set; }
    public double P95 { get; set; }
    public double P99 { get; set; }
    public double Max { get; set; }
    public double Throughput { get; set; }
}
