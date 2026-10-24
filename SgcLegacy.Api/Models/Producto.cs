using System;
using System.Collections.Generic;

namespace SgcLegacy.Api.Models;

public partial class Producto
{
    public long Id { get; set; }

    public string Codigosku { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public decimal Preciobase { get; set; }

    public virtual ICollection<DetallePedido> DetallePedidos { get; set; } = new List<DetallePedido>();

    public virtual Inventario? Inventario { get; set; }

    public virtual ICollection<MovimientoInventario> MovimientoInventarios { get; set; } = new List<MovimientoInventario>();
}
