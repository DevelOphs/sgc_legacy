using System;
using System.Collections.Generic;

namespace SgcLegacy.Api.Models;

public partial class Comprobante
{
    public long Id { get; set; }

    public long Pedidoid { get; set; }

    public string Codigoreferencia { get; set; } = null!;

    public DateTime Fechaemision { get; set; }

    public virtual ICollection<Notificacion> Notificacions { get; set; } = new List<Notificacion>();

    public virtual Pedido Pedido { get; set; } = null!;
}
