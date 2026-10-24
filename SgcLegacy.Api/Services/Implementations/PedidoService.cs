using Microsoft.EntityFrameworkCore;
using SgcLegacy.Api.DTOs.Pedidos;
using SgcLegacy.Api.Models;
using SgcLegacy.Api.Services.Contracts;

namespace SgcLegacy.Api.Services.Implementations;

/// <summary>
/// Servicio que encapsula la lógica de negocio monolítica acoplada de pedidos.
/// Mantiene la transacción ACID conjunta (Pedidos, Inventario, Comprobante y Notificación).
/// </summary>
public class PedidoService : IPedidoService
{
    private readonly LegacyDbContext _context;

    public PedidoService(LegacyDbContext context)
    {
        _context = context;
    }

    public async Task<CrearPedidoResult> CrearPedidoAsync(NuevoPedidoDto dto)
    {
        var cliente = await _context.Clientes.FindAsync(dto.ClienteId);
        if (cliente == null)
            return CrearPedidoResult.NoEncontrado("Cliente no válido.");

        var pedido = new Pedido
        {
            Clienteid = dto.ClienteId, // Ajustado a la convención de Postgres
            Estado = "PENDIENTE",
            Fechacreacion = DateTime.UtcNow
        };

        _context.Pedidos.Add(pedido);
        await _context.SaveChangesAsync();

        foreach (var det in dto.Detalles)
        {
            var detalle = new DetallePedido
            {
                Pedidoid = pedido.Id,
                Productoid = det.ProductoId,
                Cantidad = det.Cantidad,
                Preciounitario = det.PrecioUnitario
            };
            _context.DetallePedidos.Add(detalle);
        }
        await _context.SaveChangesAsync();

        return CrearPedidoResult.Ok(pedido.Id);
    }

    public async Task<ConfirmarPedidoResult> ConfirmarPedidoAsync(
        long id,
        bool simularCaidaNotificacion = false,
        string tipoFallo = "timeout")
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var pedido = await _context.Pedidos
                .Include(p => p.DetallePedidos)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pedido == null || pedido.Estado != "PENDIENTE")
            {
                await transaction.RollbackAsync();
                return ConfirmarPedidoResult.Invalido("Pedido inválido o ya procesado.");
            }

            // Orden consistente de bloqueo para reducir el riesgo de interbloqueos.
            foreach (var item in pedido.DetallePedidos.OrderBy(d => d.Productoid))
            {
                var inv = await _context.Inventarios
                    .FromSqlInterpolated(
                        $"SELECT * FROM inventario WHERE productoid = {item.Productoid} FOR UPDATE")
                    .FirstOrDefaultAsync();

                if (inv == null || inv.Stockdisponible < item.Cantidad)
                {
                    await transaction.RollbackAsync();
                    return ConfirmarPedidoResult.Invalido($"Stock insuficiente para el producto {item.Productoid}.");
                }

                inv.Stockdisponible -= item.Cantidad;
                inv.Ultimaactualizacion = DateTime.UtcNow;

                _context.MovimientoInventarios.Add(new MovimientoInventario
                {
                    Productoid = item.Productoid,
                    Pedidoid = pedido.Id,
                    Tipomovimiento = "SALIDA",
                    Cantidad = item.Cantidad,
                    Fechamovimiento = DateTime.UtcNow
                });
            }

            pedido.Estado = "CONFIRMADO";
            pedido.Fechaconfirmacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Generación del comprobante dentro de la misma transacción.
            var comprobante = new Comprobante
            {
                Pedidoid = pedido.Id,
                Codigoreferencia = $"COMP-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                Fechaemision = DateTime.UtcNow
            };

            _context.Comprobantes.Add(comprobante);
            await _context.SaveChangesAsync();

            // Inyección experimental de un fallo de notificación.
            if (simularCaidaNotificacion)
            {
                if (string.Equals(tipoFallo, "timeout", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(3000);
                    throw new Exception("Timeout simulado de 3000 ms en el servicio de notificaciones.");
                }

                throw new Exception("Rechazo inmediato de conexión en el servicio de notificaciones.");
            }

            _context.Notificacions.Add(new Notificacion
            {
                Comprobanteid = comprobante.Id,
                Emaildestino = "cliente@empresa.ec",
                Estado = "ENVIADO",
                Fechaintento = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return ConfirmarPedidoResult.Ok(pedido.Id, comprobante.Codigoreferencia);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return ConfirmarPedidoResult.Fallo(ex.Message);
        }
    }
}
