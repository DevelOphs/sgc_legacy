using System;
using System.Collections.Generic;

namespace SgcLegacy.Api.Models;

public partial class MovimientoInventario
{
    public long Id { get; set; }

    public long Productoid { get; set; }

    public long Pedidoid { get; set; }

    public string Tipomovimiento { get; set; } = null!;

    public int Cantidad { get; set; }

    public DateTime Fechamovimiento { get; set; }

    public virtual Pedido Pedido { get; set; } = null!;

    public virtual Producto Producto { get; set; } = null!;
}
