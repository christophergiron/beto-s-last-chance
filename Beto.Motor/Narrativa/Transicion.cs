using Beto.Motor.Nucleo;
namespace Beto.Motor.Narrativa;

// Destino == null significa "se queda en la misma fase".
public sealed record Transicion(
    string Etiqueta,
    Fase? Destino,
    Func<EstadoJuego, EstadoJuego> Efecto,
    Func<EstadoJuego, bool>? Guarda = null,
    int Peso = 1,
    int Prioridad = 0);