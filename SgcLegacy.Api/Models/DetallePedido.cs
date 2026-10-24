using System;
using System.Collections.Generic;

namespace SgcLegacy.Api.Models;

public partial class DetallePedido
{
    public long Id { get; set; }

    public long Pedidoid { get; set; }

    public long Productoid { get; set; }

    public int Cantidad { get; set; }

    public decimal Preciounitario { get; set; }

    public virtual Pedido Pedido { get; set; } = null!;

    public virtual Producto Producto { get; set; } = null!;
}
