namespace SgcLegacy.Api.DTOs.Monitor;

public class RelacionFkDto
{
    public string TablaOrigen { get; set; } = string.Empty;
    public string ColumnaFk { get; set; } = string.Empty;
    public string TablaDestino { get; set; } = string.Empty;
    public string ModuloOrigen { get; set; } = string.Empty;
    public string ModuloDestino { get; set; } = string.Empty;
    public bool EsExterna { get; set; }
}
