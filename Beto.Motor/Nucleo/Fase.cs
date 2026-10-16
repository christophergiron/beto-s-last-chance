namespace Beto.Motor.Nucleo;

public enum Fase
{
    S0_Inicio, S1_Preparacion, S2_TrabajoProyecto, S3_EntregaProyecto,
    S4_PreparacionFinales, S5_ExamenFinal, S6_Resultado,
    S7_TurnosAdicionales, S8_SegundoIntento, S9_ResultadoFinal,
    F1_AprobacionMinima, F2_MaximoDesempeno, F3_Reprobacion,
    F4_EventoSalud, F5_ExcesoOcio
}

public enum AccionJugador
{
    Estudiar, NoHacerNada, SalirConAmigos, TrabajarProyecto, EstudiarConAmigos,
    Epsilon // ε: transición automática, no la elige el jugador
}