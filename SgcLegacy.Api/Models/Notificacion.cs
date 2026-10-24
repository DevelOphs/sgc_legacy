using System;
using System.Collections.Generic;

namespace SgcLegacy.Api.Models;

public partial class Notificacion
{
    public long Id { get; set; }

    public long Comprobanteid { get; set; }

    public string Emaildestino { get; set; } = null!;

    public string Estado { get; set; } = null!;

    public DateTime Fechaintento { get; set; }

    public virtual Comprobante Comprobante { get; set; } = null!;
}
