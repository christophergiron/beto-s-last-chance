using Beto.Motor.Nucleo;
using Beto.Motor.Narrativa;
using Xunit;

public class PruebasAutomata
{
    static (AutomataNarrativo automata, EstadoJuego estado) Iniciar()
    {
        var automata = ReglasBeto.Construir(semilla: 1);
        var estado = new EstadoJuego();
        foreach (var paso in automata.Estabilizar(estado)) estado = paso.Despues;
        return (automata, estado);
    }

    [Fact]
    public void Inicio_llega_a_S1_con_la_pila_cargada()
    {
        var (_, e) = Iniciar();
        Assert.Equal(Fase.S1_Preparacion, e.Fase);
        Assert.Equal("Entrega", e.Tope!.Nombre);
        Assert.Equal(14, e.TurnosRestantes);
    }

    [Fact]
    public void Tres_turnos_de_proyecto_seguidos_apilan_Agotamiento()
    {
        var (a, e) = Iniciar();
        for (int i = 0; i < 3; i++) e = a.Avanzar(e, AccionJugador.TrabajarProyecto).Last().Despues;
        Assert.Equal("Agotamiento", e.Tope!.Nombre);
        Assert.Equal(3, e.TurnosProyecto);
    }

    [Fact]
    public void Con_obligacion_bloqueante_el_turno_se_pierde_sin_sumar_preparacion()
    {
        var (a, e) = Iniciar();
        for (int i = 0; i < 3; i++) e = a.Avanzar(e, AccionJugador.TrabajarProyecto).Last().Despues;
        int prep = e.Preparacion;
        e = a.Avanzar(e, AccionJugador.Estudiar).Last().Despues;
        Assert.Equal(prep, e.Preparacion);
    }
}