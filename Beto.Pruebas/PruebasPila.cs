using System.Collections.Immutable;
using Beto.Motor.Narrativa;
using Beto.Motor.Nucleo;
using Xunit;

// Pruebas del autómata de pila de obligaciones
public class PruebasPila
{
    //ayudas
    static readonly Obligacion Examen = new("Examen", 0);
    static readonly Obligacion Entrega = new("Entrega", 0);
    static Obligacion Agotamiento(int turnos = 3) => new("Agotamiento", turnos);
    static Obligacion Recuperacion(int turnos = 5) => new("Recuperacion", turnos);
    static ImmutableStack<Obligacion> Armar(params Obligacion[] fondoATope)
    {
        var pila = ImmutableStack<Obligacion>.Empty;
        foreach (var o in fondoATope) pila = pila.Push(o);
        return pila;
    }

    static string[] Nombres(EstadoJuego e) => e.Pila.Select(o => o.Nombre).ToArray();

    static (AutomataNarrativo automata, EstadoJuego estado) Iniciar()
    {
        var automata = ReglasBeto.Construir(semilla: 1);
        var estado = new EstadoJuego();
        foreach (var paso in automata.Estabilizar(estado)) estado = paso.Despues;
        return (automata, estado);
    }

    static EstadoJuego Jugar(AutomataNarrativo a, EstadoJuego e, AccionJugador accion, int veces = 1)
    {
        for (int i = 0; i < veces; i++) e = a.Avanzar(e, accion).Last().Despues;
        return e;
    }

    //Orden LIFO

    [Fact]
    public void Inicio_apila_Examen_en_el_fondo_y_Entrega_encima()
    {
        var (_, e) = Iniciar();
        Assert.Equal(new[] { "Entrega", "Examen" }, Nombres(e));   // lo último en entrar es el tope
        Assert.False(e.Bloqueado);                                  // Entrega no bloquea turnos
    }

    [Fact]
    public void Lifo_Agotamiento_se_apila_sobre_Entrega_y_sale_antes_que_ella()
    {
        var (a, e) = Iniciar();
        e = Jugar(a, e, AccionJugador.TrabajarProyecto, 3);
        Assert.Equal(new[] { "Agotamiento", "Entrega", "Examen" }, Nombres(e));
        Assert.Equal(3, e.Tope!.Turnos);

        e = Jugar(a, e, AccionJugador.Estudiar);
        Assert.Equal(2, e.Tope!.Turnos);                            // sigue siendo Agotamiento
        e = Jugar(a, e, AccionJugador.Estudiar);
        Assert.Equal(1, e.Tope!.Turnos);
        e = Jugar(a, e, AccionJugador.Estudiar);                    // tercer turno perdido
        Assert.Equal(new[] { "Entrega", "Examen" }, Nombres(e));
        Assert.False(e.Bloqueado);
    }

    [Fact]
    public void Un_turno_perdido_solo_modifica_el_tope()
    {
        var (a, e) = Iniciar();
        e = Jugar(a, e, AccionJugador.TrabajarProyecto, 3);
        var antes = e.Pila.Pop().ToArray();                       

        var despues = Jugar(a, e, AccionJugador.NoHacerNada);

        Assert.Equal(antes, despues.Pila.Pop().ToArray());         
    }

    [Fact]
    public void Con_obligacion_bloqueante_toda_accion_pierde_el_turno()
    {
        var (a, e) = Iniciar();
        e = Jugar(a, e, AccionJugador.TrabajarProyecto, 3);
        var disponibles = a.Disponibles(e);
        Assert.NotEmpty(disponibles);

        foreach (var accion in disponibles)
        {
            var d = a.Avanzar(e, accion).Last().Despues;
            Assert.Equal(2, d.Tope!.Turnos);                        
            Assert.Equal(e.TurnosProyecto, d.TurnosProyecto);      
            Assert.Equal(e.Preparacion, d.Preparacion);
            Assert.Equal(e.Fase, d.Fase);
        }
    }

    //Agotamiento y Recuperacion

    [Fact]
    public void Evento_de_salud_apila_Recuperacion_encima_de_Agotamiento()
    {
        
        var (a, e) = Iniciar();
        e = e with { Pila = Armar(Examen, Entrega, Agotamiento(3)), RiesgoSalud = Configuracion.UmbralEventoSalud };

        var sobrevive = a.Candidatas(e, AccionJugador.Epsilon)
                         .Single(t => t.Etiqueta.StartsWith("Evento de salud: sobrevive"));
        var n = sobrevive.Efecto(e);

        Assert.Equal(new[] { "Recuperacion", "Agotamiento", "Entrega", "Examen" }, Nombres(n));
        Assert.Equal(Configuracion.TurnosRecuperacion, n.Tope!.Turnos);
        Assert.Equal(3, n.Pila.Pop().Peek().Turnos);               
    }

    [Fact]
    public void Anidado_al_resolver_Recuperacion_se_retoma_Agotamiento_con_sus_turnos_intactos()
    {
        var (a, e) = Iniciar();
       
        e = e with { Pila = Armar(Examen, Entrega, Agotamiento(2), Recuperacion(2)) };
        Assert.Equal(new[] { "Recuperacion", "Agotamiento", "Entrega", "Examen" }, Nombres(e));

        e = Jugar(a, e, AccionJugador.Estudiar);
        Assert.Equal(new[] { "Recuperacion", "Agotamiento", "Entrega", "Examen" }, Nombres(e));
        Assert.Equal(1, e.Tope!.Turnos);

        e = Jugar(a, e, AccionJugador.SalirConAmigos);              
        Assert.Equal(new[] { "Agotamiento", "Entrega", "Examen" }, Nombres(e));
        Assert.Equal(2, e.Tope!.Turnos);                             

        e = Jugar(a, e, AccionJugador.NoHacerNada, 2);              
        Assert.Equal(new[] { "Entrega", "Examen" }, Nombres(e));
        Assert.False(e.Bloqueado);
    }

    [Fact]
    public void Un_contador_no_basta_misma_cantidad_de_obligaciones_distinto_tope()
    {
        
        var (a, e) = Iniciar();
        var conAgotamiento = e with { Pila = Armar(Examen, Entrega, Agotamiento(1)) };
        var conEntrega = e with { Pila = Armar(Examen, Agotamiento(1), Entrega) };

        Assert.True(conAgotamiento.Bloqueado);
        Assert.False(conEntrega.Bloqueado);
        Assert.Equal(conAgotamiento.Pila.Count(), conEntrega.Pila.Count());
    }

    // No se pasa a S3 si el tope no es Entrega

    [Fact]
    public void No_pasa_a_S3_si_el_tope_no_es_Entrega_hasta_resolver_la_obligacion()
    {
        var (a, e) = Iniciar();
        // Llegó el día 15 (TurnosRestantes = 0) pero Agotamiento tapa a Entrega.
        e = e with
        {
            TurnosRestantes = 0,
            TurnosProyecto = Configuracion.TurnosProyectoNecesarios,
            Pila = Armar(Examen, Entrega, Agotamiento(1))
        };

        Assert.DoesNotContain(a.Candidatas(e, AccionJugador.Epsilon), t => t.Etiqueta == "Llega el dia 15");
        Assert.Empty(a.Estabilizar(e));                             
        Assert.Equal(Fase.S1_Preparacion, e.Fase);

        // Se pierde el último turno de Agotamiento: ahora el tope es Entrega y S3 se habilita.
        var pasos = a.Avanzar(e, AccionJugador.Estudiar);
        var etiquetas = pasos.Select(p => p.Tomada.Etiqueta).ToList();
        Assert.Contains("Turno perdido (obligacion en el tope)", etiquetas);
        Assert.Contains("Llega el dia 15", etiquetas);
        Assert.Equal(Fase.S4_PreparacionFinales, pasos.Last().Despues.Fase);
    }

    [Fact]
    public void Con_Entrega_en_el_tope_el_dia_15_si_pasa_a_S3()
    {
        var (a, e) = Iniciar();
        e = e with { TurnosRestantes = 0 };

        Assert.Contains(a.Candidatas(e, AccionJugador.Epsilon), t => t.Etiqueta == "Llega el dia 15");
    }

    [Fact]
    public void S3_consume_exactamente_Entrega_y_deja_Examen_en_el_tope()
    {
        var (a, e) = Iniciar();
        e = e with { TurnosRestantes = 0, TurnosProyecto = Configuracion.TurnosProyectoNecesarios };

        var final = a.Estabilizar(e).Last().Despues;

        Assert.Equal(Fase.S4_PreparacionFinales, final.Fase);
        Assert.Equal(new[] { "Examen" }, Nombres(final));
        Assert.True(final.ProyectoEntregado);
    }

    [Fact]
    public void No_empieza_el_examen_si_el_tope_no_es_Examen()
    {
        var (a, e) = Iniciar();
        var s4 = e with
        {
            Fase = Fase.S4_PreparacionFinales,
            TurnosRestantes = 0,
            ProyectoEntregado = true,
            Pila = Armar(Examen, Recuperacion(5))
        };
        Assert.DoesNotContain(a.Candidatas(s4, AccionJugador.Epsilon), t => t.Etiqueta == "Empieza el examen");

        var libre = s4 with { Pila = Armar(Examen) };
        Assert.Contains(a.Candidatas(libre, AccionJugador.Epsilon), t => t.Etiqueta == "Empieza el examen");
    }

    //pop y re-push

    [Fact]
    public void Presentar_el_examen_vacia_la_pila_y_reprobar_apila_otro_Examen()
    {
        var (a, e) = Iniciar();
        var s5 = e with { Fase = Fase.S5_ExamenFinal, Pila = Armar(Examen) };

        var s6 = a.Candidatas(s5, AccionJugador.Epsilon).Single().Efecto(s5) with { Fase = Fase.S6_Resultado };
        Assert.True(s6.Pila.IsEmpty);                              

        var s6Mal = s6 with { Preparacion = 0 };
        var reprueba = a.Candidatas(s6Mal, AccionJugador.Epsilon)
                        .Single(t => t.Etiqueta.StartsWith("Insuficiente: reprueba"));
        Assert.Equal(new[] { "Examen" }, Nombres(reprueba.Efecto(s6Mal)));
        Assert.Equal(Fase.S7_TurnosAdicionales, reprueba.Destino);
    }
}
