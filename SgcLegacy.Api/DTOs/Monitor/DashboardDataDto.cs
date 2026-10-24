using SgcLegacy.Api.Services.Telemetry;

namespace SgcLegacy.Api.DTOs.Monitor;

public class DashboardDataDto
{
    public string EstadoCorrida { get; set; } = string.Empty;
    public string RegimenActivo { get; set; } = string.Empty;
    public int RepeticionActiva { get; set; }
    public List<ResumenRepeticionDto> ResumenRepeticiones { get; set; } = new();
    public List<MatrizOe1Dto> MatrizPriorizacionOe1 { get; set; } = new();
    public List<RelacionFkDto> RelacionesBaseDatos { get; set; } = new();
    public List<TrazaPeticionReal> TrazasRecientes { get; set; } = new();
}
