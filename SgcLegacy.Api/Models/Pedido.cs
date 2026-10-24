using System;
using System.Collections.Generic;

namespace SgcLegacy.Api.Models;

public partial class Pedido
{
    public long Id { get; set; }

    public long Clienteid { get; set; }

    public DateTime Fechacreacion { get; set; }

    public DateTime? Fechaconfirmacion { get; set; }

    public string Estado { get; set; } = null!;

    public virtual Cliente Cliente { get; set; } = null!;

    public virtual Comprobante? Comprobante { get; set; }

    public virtual ICollection<DetallePedido> DetallePedidos { get; set; } = new List<DetallePedido>();

    public virtual ICollection<MovimientoInventario> MovimientoInventarios { get; set; } = new List<MovimientoInventario>();
}
