using Beto.Motor.Nucleo;
using static Beto.Motor.Nucleo.Fase;
using static Beto.Motor.Nucleo.AccionJugador;

namespace Beto.Motor.Narrativa;

public static class ReglasBeto
{
    // (preparación, riesgo académico, riesgo de salud)
    static readonly Dictionary<AccionJugador, (int Prep, int Acad, int Salud)> Efectos = new()
    {
        [Estudiar] = (8, -3, 1),
        [NoHacerNada] = (0, 8, -5),
        [SalirConAmigos] = (0, 5, -3),
        [TrabajarProyecto] = (4, -10, 5),
        [EstudiarConAmigos] = (16, -5, -2),
    };

    static bool EnExamenes(Fase f) => f is S4_PreparacionFinales or S7_TurnosAdicionales;

    static EstadoJuego Apilar(EstadoJuego e, string nombre, int turnos) =>
        e with { Pila = e.Pila.Push(new Obligacion(nombre, turnos)) };

    static EstadoJuego Desapilar(EstadoJuego e) => e with { Pila = e.Pila.Pop() };

    static EstadoJuego AvanzarTurno(EstadoJuego e)
    {
        var n = e with { Turno = e.Turno + 1, TurnosRestantes = e.TurnosRestantes - 1 };
        if (n.Turno == 14) n = n with { RiesgoSalud = n.RiesgoSalud - 25 }; // evento del día 14
        return n.Acotar();
    }

    static EstadoJuego Ejecutar(EstadoJuego e, AccionJugador a)
    {
        var (prep, acad, salud) = Efectos[a];
        if (EnExamenes(e.Fase) && !e.ProyectoEntregado && a == Estudiar) salud += 3;
        return AvanzarTurno(e with
        {
            Preparacion = e.Preparacion + prep,
            RiesgoAcademico = e.RiesgoAcademico + acad,
            RiesgoSalud = e.RiesgoSalud + salud,
            TurnosProyecto = e.TurnosProyecto + (a == TrabajarProyecto ? 1 : 0),
            ProyectoConsecutivo = a == TrabajarProyecto ? e.ProyectoConsecutivo + 1 : 0,
            OcioConsecutivo = a == SalirConAmigos ? e.OcioConsecutivo + 1 : 0,
            InactivoConsecutivo = a == NoHacerNada ? e.InactivoConsecutivo + 1 : 0,
        });
    }

    // Turno perdido por obligación en el tope de la pila
    static EstadoJuego PerderTurno(EstadoJuego e)
    {
        var tope = e.Pila.Peek();
        var pila = e.Pila.Pop();
        if (tope.Turnos > 1) pila = pila.Push(tope with { Turnos = tope.Turnos - 1 });
        var n = e with { Pila = pila, ProyectoConsecutivo = 0, OcioConsecutivo = 0, InactivoConsecutivo = 0 };
        if (tope.Nombre == "Agotamiento") n = n with { RiesgoSalud = n.RiesgoSalud + 2 };
        return AvanzarTurno(n);
    }

    static EstadoJuego PerderTurnoPorPereza(EstadoJuego e) =>
        AvanzarTurno(e with { InactivoConsecutivo = 0 });

    public static AutomataNarrativo Construir(int semilla)
    {
        var automata = new AutomataNarrativo(semilla);

        // Acción del jugador + sus dos variantes de "turno perdido"
        void Jugable(Fase fase, AccionJugador accion, Func<EstadoJuego, bool> disponible, Fase destino)
        {
            bool esEstudio = accion is Estudiar or EstudiarConAmigos;
            automata.Agregar(fase, accion, new Transicion(accion.ToString(), destino,
                e => Ejecutar(e, accion),
                e => disponible(e) && !e.Bloqueado
                     && !(esEstudio && e.InactivoConsecutivo >= Configuracion.LimitePereza)));
            automata.Agregar(fase, accion, new Transicion("Turno perdido (obligacion en el tope)", null,
                PerderTurno, e => disponible(e) && e.Bloqueado, Prioridad: 100));
            if (esEstudio)
                automata.Agregar(fase, accion, new Transicion("Pereza: turno perdido", null,
                    PerderTurnoPorPereza,
                    e => disponible(e) && !e.Bloqueado && e.InactivoConsecutivo >= Configuracion.LimitePereza,
                    Prioridad: 90));
        }

        // ---------- δ sobre las acciones del jugador ----------
        foreach (var x in new[] { Estudiar, NoHacerNada, SalirConAmigos })
        {
            Jugable(S1_Preparacion, x, _ => true, S1_Preparacion);
            Jugable(S2_TrabajoProyecto, x, _ => true, S1_Preparacion);
        }
        Jugable(S1_Preparacion, TrabajarProyecto,
            e => e.TurnosProyecto < Configuracion.TurnosProyectoNecesarios, S2_TrabajoProyecto);
        Jugable(S2_TrabajoProyecto, TrabajarProyecto,
            e => e.TurnosProyecto < Configuracion.TurnosProyectoNecesarios, S2_TrabajoProyecto);

        foreach (var f in new[] { S4_PreparacionFinales, S7_TurnosAdicionales })
        {
            Jugable(f, Estudiar, _ => true, f);
            foreach (var x in new[] { NoHacerNada, SalirConAmigos, EstudiarConAmigos })
                Jugable(f, x, e => e.ProyectoEntregado, f);
        }

        // ---------- ε-transiciones: eventos en fases jugables ----------
        foreach (var f in new[] { S1_Preparacion, S2_TrabajoProyecto, S4_PreparacionFinales, S7_TurnosAdicionales })
        {
            automata.Agregar(f, Epsilon, new Transicion("Exceso de ocio", F5_ExcesoOcio, e => e,
                e => e.OcioConsecutivo >= Configuracion.LimiteOcio, Prioridad: 30));

            automata.Agregar(f, Epsilon, new Transicion("Evento de salud: termina la partida", F4_EventoSalud, e => e,
                e => e.RiesgoSalud >= Configuracion.UmbralEventoSalud, Peso: 1, Prioridad: 20));
            automata.Agregar(f, Epsilon, new Transicion("Evento de salud: sobrevive", null,
                e => Apilar(e with { RiesgoSalud = Configuracion.SaludTrasEvento },
                            "Recuperacion", Configuracion.TurnosRecuperacion),
                e => e.RiesgoSalud >= Configuracion.UmbralEventoSalud, Peso: 1, Prioridad: 20));
        }

        foreach (var f in new[] { S1_Preparacion, S2_TrabajoProyecto })
        {
            automata.Agregar(f, Epsilon, new Transicion("Agotamiento", S1_Preparacion,
                e => Apilar(e with { ProyectoConsecutivo = 0 }, "Agotamiento", Configuracion.TurnosAgotamiento),
                e => e.ProyectoConsecutivo >= Configuracion.ProyectoSeguidosParaAgotamiento, Prioridad: 10));
            automata.Agregar(f, Epsilon, new Transicion("Llega el dia 15", S3_EntregaProyecto, e => e,
                e => e.TurnosRestantes <= 0 && e.Tope?.Nombre == "Entrega"));
        }

        // ---------- ε-transiciones: calendario y resultados ----------
        automata.Agregar(S0_Inicio, Epsilon, new Transicion("Inicio", S1_Preparacion, e =>
            Apilar(Apilar(e with { Turno = 1, TurnosRestantes = Configuracion.TurnosPreparacion }, "Examen", 0),
                   "Entrega", 0)));

        automata.Agregar(S3_EntregaProyecto, Epsilon, new Transicion("Proyecto entregado", S4_PreparacionFinales,
            e => (Desapilar(e) with
            {
                ProyectoEntregado = true,
                Preparacion = e.Preparacion + 5,
                RiesgoSalud = 0,
                TurnosRestantes = Configuracion.TurnosExamen
            }).Acotar(),
            e => e.TurnosProyecto >= Configuracion.TurnosProyectoNecesarios));
        //automata.Agregar(S3_EntregaProyecto, Epsilon, new Transicion("Proyecto NO entregado", S4_PreparacionFinales,
        //    e => (Desapilar(e) with
        //    {
        //        Preparacion = e.Preparacion - 4 * e.TurnosProyecto,
        //        RiesgoSalud = e.RiesgoSalud + 50,
        //        TurnosRestantes = Configuracion.TurnosExamen
        //    }).Acotar(),
        //    e => e.TurnosProyecto < Configuracion.TurnosProyectoNecesarios));

        automata.Agregar(S3_EntregaProyecto, Epsilon, new Transicion("Proyecto NO entregado", S4_PreparacionFinales,
    e => (Desapilar(e) with
    {
        Preparacion = e.Preparacion - Configuracion.PrepPerdidaPorTurnoProyecto * e.TurnosProyecto,
        RiesgoSalud = e.RiesgoSalud + Configuracion.SaludSinProyecto,
        TurnosRestantes = Configuracion.TurnosExamen
    }).Acotar(),
    e => e.TurnosProyecto < Configuracion.TurnosProyectoNecesarios));

        automata.Agregar(S4_PreparacionFinales, Epsilon, new Transicion("Empieza el examen", S5_ExamenFinal, e => e,
            e => e.TurnosRestantes <= 0 && e.Tope?.Nombre == "Examen"));
        automata.Agregar(S5_ExamenFinal, Epsilon, new Transicion("Presenta el examen", S6_Resultado, Desapilar));

        //automata.Agregar(S6_Resultado, Epsilon, new Transicion("Preparacion alta", F2_MaximoDesempeno, e => e,
        //    e => e.Preparacion >= Configuracion.NotaAlta));
        //automata.Agregar(S6_Resultado, Epsilon, new Transicion("Preparacion suficiente", F1_AprobacionMinima, e => e,
        //    e => e.Preparacion >= Configuracion.AprobarPrimerIntento && e.Preparacion < Configuracion.NotaAlta));
        //automata.Agregar(S6_Resultado, Epsilon, new Transicion("Insuficiente: aprueba por suerte (20%)", F1_AprobacionMinima, e => e,
        //    e => e.Preparacion < Configuracion.AprobarPrimerIntento, Peso: 20));
        //automata.Agregar(S6_Resultado, Epsilon, new Transicion("Insuficiente: reprueba (80%)", S7_TurnosAdicionales,
        //    e => Apilar((e with { RiesgoSalud = e.RiesgoSalud + 20, TurnosRestantes = Configuracion.TurnosExtra }).Acotar(),
        //                "Examen", 0),
        //    e => e.Preparacion < Configuracion.AprobarPrimerIntento, Peso: 80));

        automata.Agregar(S6_Resultado, Epsilon, new Transicion("Preparacion alta", F2_MaximoDesempeno, e => e,
    e => e.PreparacionEfectiva >= Configuracion.NotaAlta));
        automata.Agregar(S6_Resultado, Epsilon, new Transicion("Preparacion suficiente", F1_AprobacionMinima, e => e,
            e => e.PreparacionEfectiva >= Configuracion.AprobarPrimerIntento
                 && e.PreparacionEfectiva < Configuracion.NotaAlta));
        automata.Agregar(S6_Resultado, Epsilon, new Transicion("Insuficiente: aprueba por suerte (20%)", F1_AprobacionMinima, e => e,
            e => e.PreparacionEfectiva < Configuracion.AprobarPrimerIntento, Peso: 20));
        automata.Agregar(S6_Resultado, Epsilon, new Transicion("Insuficiente: reprueba (80%)", S7_TurnosAdicionales,
            e => Apilar((e with { RiesgoSalud = e.RiesgoSalud + 20, TurnosRestantes = Configuracion.TurnosExtra }).Acotar(),
                        "Examen", 0),
            e => e.PreparacionEfectiva < Configuracion.AprobarPrimerIntento, Peso: 80));
        automata.Agregar(S7_TurnosAdicionales, Epsilon, new Transicion("Termina el plazo extra", S8_SegundoIntento, e => e,
            e => e.TurnosRestantes <= 0 && e.Tope?.Nombre == "Examen"));
        automata.Agregar(S8_SegundoIntento, Epsilon, new Transicion("Presenta el segundo examen", S9_ResultadoFinal, Desapilar));

        //automata.Agregar(S9_ResultadoFinal, Epsilon, new Transicion("Maximo desempeno", F2_MaximoDesempeno, e => e,
        //    e => e.Preparacion >= Configuracion.NotaAlta));
        //automata.Agregar(S9_ResultadoFinal, Epsilon, new Transicion("Aprobacion minima", F1_AprobacionMinima, e => e,
        //    e => e.Preparacion >= Configuracion.AprobarSegundoIntento && e.Preparacion < Configuracion.NotaAlta));
        //automata.Agregar(S9_ResultadoFinal, Epsilon, new Transicion("Reprobacion", F3_Reprobacion, e => e,
        //    e => e.Preparacion < Configuracion.AprobarSegundoIntento));

        automata.Agregar(S9_ResultadoFinal, Epsilon, new Transicion("Maximo desempeno", F2_MaximoDesempeno, e => e,
    e => e.PreparacionEfectiva >= Configuracion.NotaAlta));
        automata.Agregar(S9_ResultadoFinal, Epsilon, new Transicion("Aprobacion minima", F1_AprobacionMinima, e => e,
            e => e.PreparacionEfectiva >= Configuracion.AprobarSegundoIntento
                 && e.PreparacionEfectiva < Configuracion.NotaAlta));
        automata.Agregar(S9_ResultadoFinal, Epsilon, new Transicion("Reprobacion", F3_Reprobacion, e => e,
            e => e.PreparacionEfectiva < Configuracion.AprobarSegundoIntento));
        return automata;
    }
}