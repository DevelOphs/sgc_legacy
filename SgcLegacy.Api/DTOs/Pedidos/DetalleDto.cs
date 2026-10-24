namespace SgcLegacy.Api.DTOs.Pedidos;

public class DetalleDto
{
    public long ProductoId { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
}
