using System.Collections.Immutable;
namespace Beto.Motor.Nucleo;

public sealed record EstadoJuego
{
    public Fase Fase { get; init; } = Fase.S0_Inicio;
    public int Turno { get; init; }
    public int TurnosRestantes { get; init; }
    public int Preparacion { get; init; }
    public int RiesgoAcademico { get; init; } = 50;
    public int RiesgoSalud { get; init; } = 10;
    public int TurnosProyecto { get; init; }
    public int ProyectoConsecutivo { get; init; }
    public int OcioConsecutivo { get; init; }
    public int InactivoConsecutivo { get; init; }
    public bool ProyectoEntregado { get; init; }
    public ImmutableStack<Obligacion> Pila { get; init; } = ImmutableStack<Obligacion>.Empty;
    // Preparación real al rendir el examen: el riesgo académico la reduce
    public int PreparacionEfectiva =>
        Math.Max(0, Preparacion - RiesgoAcademico / Configuracion.DivisorRiesgoAcademico);
    public bool EsFinal => Fase >= Fase.F1_AprobacionMinima;
    public Obligacion? Tope => Pila.IsEmpty ? null : Pila.Peek();
    public bool Bloqueado => Tope is { Turnos: > 0 };

    public EstadoJuego Acotar() => this with
    {
        Preparacion = Math.Clamp(Preparacion, 0, 100),
        RiesgoAcademico = Math.Clamp(RiesgoAcademico, 0, 100),
        RiesgoSalud = Math.Clamp(RiesgoSalud, 0, 100)
    };
}