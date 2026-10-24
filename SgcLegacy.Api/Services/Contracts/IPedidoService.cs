using SgcLegacy.Api.DTOs.Pedidos;

namespace SgcLegacy.Api.Services.Contracts;

public interface IPedidoService
{
    Task<CrearPedidoResult> CrearPedidoAsync(NuevoPedidoDto dto);
    Task<ConfirmarPedidoResult> ConfirmarPedidoAsync(long id, bool simularCaidaNotificacion = false, string tipoFallo = "timeout");
}
