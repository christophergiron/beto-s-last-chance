namespace Beto.Motor.Nucleo;

public static class Configuracion
{
    public const int TurnosPreparacion = 14;
    public const int DiaEvento = 14;                   // día del evento: bonificación de salud, doble turno y menos cansancio
    public const int TurnosDiaEvento = 2;              // doble accion del dia 14
    public const int ReduccionCansancioDiaEvento = 35; // % que baja el riesgo de salud al llegar al día del evento
    public const int TurnosExamen = 3;
    public const int TurnosExtra = 7;
    public const int TurnosProyectoNecesarios = 4;
    public const int ProyectoSeguidosParaAgotamiento = 3;
    public const int TurnosAgotamiento = 3;
    public const int LimitePereza = 4;
    public const int LimiteOcio = 5;
    public const int AprobarPrimerIntento = 55;
    public const int AprobarSegundoIntento = 60;
    public const int NotaAlta = 80;
    public const int UmbralEventoSalud = 60;
    public const int SaludTrasEvento = 25;
    public const int TurnosRecuperacion = 5;
    public const int SaludSinProyecto = 50;            // +riesgo de salud si no entrega
    public const int PrepPerdidaPorTurnoProyecto = 4;  // prep que se pierde si no entrega
    public const int DivisorRiesgoAcademico = 10;      // el riesgo académico resta riesgo/10
}