namespace SgcLegacy.Api.Services.Telemetry;

public static class ModuloClassifier
{
    public static string ClasificarModulo(string tabla) => tabla.ToLowerInvariant() switch
    {
        "cliente" or "pedido" or "detalle_pedido" => "Pedidos",
        "producto" or "inventario" or "movimiento_inventario" => "Inventario",
        "comprobante" or "notificacion" => "Comprobantes",
        _ => "Otro"
    };
}
