using Microsoft.AspNetCore.Mvc;
using SgcLegacy.Api.DTOs.Pedidos;
using SgcLegacy.Api.Services.Contracts;

namespace SgcLegacy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PedidosController : ControllerBase
{
    private readonly IPedidoService _pedidoService;

    public PedidosController(IPedidoService pedidoService)
    {
        _pedidoService = pedidoService;
    }

    [HttpPost]
    public async Task<IActionResult> CrearPedido([FromBody] NuevoPedidoDto dto)
    {
        var resultado = await _pedidoService.CrearPedidoAsync(dto);
        if (resultado.ClienteNoEncontrado)
            return NotFound(resultado.Error);

        if (!resultado.Exitoso)
            return BadRequest(resultado.Error);

        return Ok(new
        {
            Mensaje = resultado.Mensaje,
            PedidoId = resultado.PedidoId
        });
    }

    [HttpPost("{id}/confirmar")]
    public async Task<IActionResult> ConfirmarPedido(
       long id,
       [FromQuery] bool simularCaidaNotificacion = false,
       [FromQuery] string tipoFallo = "timeout")
    {
        var resultado = await _pedidoService.ConfirmarPedidoAsync(id, simularCaidaNotificacion, tipoFallo);

        if (resultado.PedidoInvalido || !string.IsNullOrEmpty(resultado.ErrorValidacion))
        {
            return BadRequest(resultado.ErrorValidacion);
        }

        if (!string.IsNullOrEmpty(resultado.ErrorServidor))
        {
            return StatusCode(500, new
            {
                error = "Fallo transaccional en el monolito",
                detalle = resultado.ErrorServidor
            });
        }

        return Ok(new
        {
            mensaje = "Pedido confirmado",
            pedidoId = resultado.PedidoId,
            comprobante = resultado.CodigoComprobante
        });
    }
}