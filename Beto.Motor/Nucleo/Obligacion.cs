namespace Beto.Motor.Nucleo;

// Elemento de la pila. Turnos > 0 significa que bloquea al jugador.
public sealed record Obligacion(string Nombre, int Turnos);