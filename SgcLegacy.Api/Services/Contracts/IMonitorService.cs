using SgcLegacy.Api.DTOs.Monitor;

namespace SgcLegacy.Api.Services.Contracts;

public interface IMonitorService
{
    Task<DashboardDataDto> ObtenerDatosDashboardAsync();
    Task<string> ConfigurarEtapaAsync(string regimen, int repeticion, bool esCalentamiento, bool reiniciarTodo);
    void LimpiarTrazas();
}
