using Beto.Motor.Nucleo;

namespace Beto.Motor.Gramatica;

public static class TextosBeto
{
    private static readonly Lazy<GeneradorGLC> _gramatica = new(GeneradorGLC.DesdeRecursoEmbebido);

    public const int ProfundidadEnJuego = 8;

    public static GeneradorGLC Gramatica => _gramatica.Value;

    public static string? SimboloPara(AccionJugador accion) => accion switch
    {
        AccionJugador.SalirConAmigos => "INVITACION",
        AccionJugador.EstudiarConAmigos => "INVITACION_ESTUDIO",
        AccionJugador.Estudiar => "MISION",
        AccionJugador.TrabajarProyecto => "MENSAJE_PROFESOR",
        AccionJugador.NoHacerNada => "MENSAJE_PROFESOR",
        _ => null
    };

    public static string? Generar(AccionJugador accion, Random azar)
    {
        var simbolo = SimboloPara(accion);
        return simbolo is null ? null : Gramatica.Generar(simbolo, azar, ProfundidadEnJuego);
    }
}
