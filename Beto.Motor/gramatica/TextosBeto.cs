using Beto.Motor.Nucleo;
namespace Beto.Motor.Gramatica;

public static class TextosBeto
{
    private static readonly Lazy<GeneradorGLC> _gramatica = new(GeneradorGLC.DesdeRecursoEmbebido);
    public const int ProfundidadEnJuego = 4;

    public static GeneradorGLC Gramatica => _gramatica.Value;
    public static string? SimboloPara(AccionJugador accion) => accion switch
    {
        AccionJugador.Estudiar => "RESUMEN_ESTUDIAR",
        AccionJugador.EstudiarConAmigos => "RESUMEN_ESTUDIAR_AMIGOS",
        AccionJugador.SalirConAmigos => "RESUMEN_SALIR",
        AccionJugador.TrabajarProyecto => "RESUMEN_PROYECTO",
        AccionJugador.NoHacerNada => "RESUMEN_DESCANSO",
        _ => null
    };

    public static string? Generar(AccionJugador accion, Random azar)
    {
        var simbolo = SimboloPara(accion);
        return simbolo is null ? null : Gramatica.Generar(simbolo, azar, ProfundidadEnJuego);
    }
}

