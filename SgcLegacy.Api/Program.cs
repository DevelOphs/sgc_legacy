using Microsoft.EntityFrameworkCore;
using SgcLegacy.Api.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using SgcLegacy.Api.Services.Contracts;
using SgcLegacy.Api.Services.Implementations;
using SgcLegacy.Api.Services.Telemetry;
using Npgsql;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Configuración de Observabilidad (OpenTelemetry)
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("SgcLegacy.Monolith"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation() // Captura latencia de los endpoints HTTP
        .AddEntityFrameworkCoreInstrumentation() // Captura eventos de EF Core
        .AddNpgsql()) // Captura las transacciones reales en PostgreSQL
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Inyección de dependencias de Base de Datos y Servicios de Dominio
builder.Services.AddDbContext<LegacyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IPedidoService, PedidoService>();
builder.Services.AddScoped<IMonitorService, MonitorService>();

// Inicializar listener de telemetría de base de datos y peticiones
ColectorTelemetriaReal.InicializarEscucha();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Habilitar entrega de archivos estáticos (HTML, CSS, JS desacoplados del backend)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();
app.MapControllers();

app.Run();