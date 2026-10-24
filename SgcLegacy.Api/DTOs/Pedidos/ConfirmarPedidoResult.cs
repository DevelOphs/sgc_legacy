namespace SgcLegacy.Api.DTOs.Pedidos;

public class ConfirmarPedidoResult
{
    public bool Exitoso { get; set; }
    public bool PedidoInvalido { get; set; }
    public string? ErrorValidacion { get; set; }
    public string? ErrorServidor { get; set; }
    public long? PedidoId { get; set; }
    public string? CodigoComprobante { get; set; }

    public static ConfirmarPedidoResult Invalido(string mensaje) =>
        new() { Exitoso = false, PedidoInvalido = true, ErrorValidacion = mensaje };

    public static ConfirmarPedidoResult Fallo(string detalle) =>
        new() { Exitoso = false, ErrorServidor = detalle };

    public static ConfirmarPedidoResult Ok(long pedidoId, string codigoComprobante) =>
        new() { Exitoso = true, PedidoId = pedidoId, CodigoComprobante = codigoComprobante };
}
