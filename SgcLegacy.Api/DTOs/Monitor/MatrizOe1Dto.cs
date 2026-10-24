namespace SgcLegacy.Api.DTOs.Monitor;

public class MatrizOe1Dto
{
    public string Modulo { get; set; } = string.Empty;
    public int Ca { get; set; }
    public int Ce { get; set; }
    public double Inestabilidad { get; set; }
    public double ParticipacionOtelPorc { get; set; }
    public double TasaLoteSqlCruzadoPorc { get; set; }
    public int PuntajeAcoplamiento { get; set; }
    public int PuntajeComplejidad { get; set; }
    public int PuntajeCriticidad { get; set; }
    public double IndiceRiesgoBase { get; set; }
    public double IndicePrioridad { get; set; }
}
