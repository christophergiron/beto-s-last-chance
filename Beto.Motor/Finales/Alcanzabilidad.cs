using Beto.Motor.Narrativa;
using Beto.Motor.Nucleo;
namespace Beto.Motor.Finales;

public sealed record ResultadoAlcanzabilidad(
    Dictionary<Fase, List<AccionJugador>> SecuenciasMinimas,
    int EstadosExplorados,
    int Callejones);

public static class Alcanzabilidad
{
    private static readonly AccionJugador[] Acciones =
    {
        AccionJugador.Estudiar, AccionJugador.NoHacerNada, AccionJugador.SalirConAmigos,
        AccionJugador.TrabajarProyecto, AccionJugador.EstudiarConAmigos
    };

    private static string Clave(EstadoJuego e) =>
        $"{(int)e.Fase}|{e.Turno}|{e.TurnosRestantes}|{e.Preparacion}|{e.RiesgoAcademico}|{e.RiesgoSalud}|" +
        $"{e.TurnosProyecto}|{e.ProyectoConsecutivo}|{e.OcioConsecutivo}|{e.InactivoConsecutivo}|" +
        $"{e.ProyectoEntregado}|{string.Join(",", e.Pila.Reverse().Select(o => o.Nombre + ":" + o.Turnos))}";

    public static ResultadoAlcanzabilidad Analizar(AutomataNarrativo automata)
    {
        var padre = new Dictionary<string, (string? Padre, AccionJugador? Accion)>();
        var cola = new Queue<(string Clave, EstadoJuego Estado)>();
        var finales = new Dictionary<Fase, string>();
        int callejones = 0;

        foreach (var inicial in automata.Cierre(new EstadoJuego()))
        {
            var k = Clave(inicial);
            if (padre.TryAdd(k, (null, null))) cola.Enqueue((k, inicial));
        }

        while (cola.Count > 0)
        {
            var (clave, estado) = cola.Dequeue();
            if (estado.EsFinal) { finales.TryAdd(estado.Fase, clave); continue; }

            bool tuvoSalida = false;
            foreach (var accion in Acciones)
                foreach (var sig in automata.SucesoresEstables(estado, accion))
                {
                    tuvoSalida = true;
                    var k = Clave(sig);
                    if (padre.TryAdd(k, (clave, accion))) cola.Enqueue((k, sig));
                }
            if (!tuvoSalida) callejones++;
        }

        var secuencias = new Dictionary<Fase, List<AccionJugador>>();
        foreach (var (fase, clave) in finales)
        {
            var secuencia = new List<AccionJugador>();
            var actual = clave;
            while (padre[actual].Padre is { } p)
            {
                secuencia.Add(padre[actual].Accion!.Value);
                actual = p;
            }
            secuencia.Reverse();
            secuencias[fase] = secuencia;
        }
        return new ResultadoAlcanzabilidad(secuencias, padre.Count, callejones);
    }
}