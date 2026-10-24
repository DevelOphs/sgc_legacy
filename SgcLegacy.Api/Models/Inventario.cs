using System;
using System.Collections.Generic;

namespace SgcLegacy.Api.Models;

public partial class Inventario
{
    public long Productoid { get; set; }

    public int Stockdisponible { get; set; }

    public DateTime Ultimaactualizacion { get; set; }

    public virtual Producto Producto { get; set; } = null!;
}
