namespace SgcLegacy.Api.DTOs.Pedidos;

public class NuevoPedidoDto
{
    public long ClienteId { get; set; }
    public List<DetalleDto> Detalles { get; set; } = new();
}
