using Microsoft.AspNetCore.Mvc;
using SgcLegacy.Api.Services.Contracts;
using SgcLegacy.Api.Services.Telemetry;

namespace SgcLegacy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MonitorController : ControllerBase
{
    private readonly IMonitorService _monitorService;
    private readonly IWebHostEnvironment _env;

    public MonitorController(IMonitorService monitorService, IWebHostEnvironment env)
    {
        _monitorService = monitorService;
        _env = env;
    }

    [HttpGet("/monitor")]
    [Produces("text/html")]
    public IActionResult VerDashboard()
    {
        if (!_env.IsDevelopment())
            return NotFound("El panel de diagnóstico está deshabilitado fuera del entorno local de desarrollo.");

        var basePath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var filePath = Path.Combine(basePath, "monitor", "index.html");

        if (!System.IO.File.Exists(filePath))
        {
            filePath = Path.Combine(basePath, "index.html");
        }

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound("El panel estático de diagnóstico no fue encontrado en wwwroot.");
        }

        return PhysicalFile(filePath, "text/html");
    }

    [HttpGet("datos-reales")]
    public async Task<IActionResult> ObtenerDatosReales()
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var datos = await _monitorService.ObtenerDatosDashboardAsync();
        return Ok(datos);
    }

    [HttpPost("configurar-etapa")]
    public async Task<IActionResult> ConfigurarEtapa(
        [FromQuery] string regimen = "SIN_FALLOS",
        [FromQuery] int repeticion = 1,
        [FromQuery] bool esCalentamiento = false,
        [FromQuery] bool reiniciarTodo = false)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var estado = await _monitorService.ConfigurarEtapaAsync(regimen, repeticion, esCalentamiento, reiniciarTodo);
        return Ok(new { estado });
    }

    [HttpPost("limpiar")]
    public IActionResult LimpiarTrazas()
    {
        if (!_env.IsDevelopment())
            return NotFound();

        _monitorService.LimpiarTrazas();
        return Ok();
    }

    // Helper retrocompatible de clasificación
    public static string ClasificarModulo(string tabla) => ModuloClassifier.ClasificarModulo(tabla);
}