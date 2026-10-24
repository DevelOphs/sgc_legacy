namespace SgcLegacy.Api.DTOs.Pedidos;

public class CrearPedidoResult
{
    public bool Exitoso { get; set; }
    public string? Error { get; set; }
    public bool ClienteNoEncontrado { get; set; }
    public long? PedidoId { get; set; }
    public string Mensaje { get; set; } = string.Empty;

    public static CrearPedidoResult NoEncontrado(string mensaje) =>
        new() { Exitoso = false, ClienteNoEncontrado = true, Error = mensaje };

    public static CrearPedidoResult ErrorOperacion(string mensaje) =>
        new() { Exitoso = false, Error = mensaje };

    public static CrearPedidoResult Ok(long pedidoId, string mensaje = "Pedido Creado como PENDIENTE") =>
        new() { Exitoso = true, PedidoId = pedidoId, Mensaje = mensaje };
}
