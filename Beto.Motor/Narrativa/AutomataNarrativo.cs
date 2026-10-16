using Beto.Motor.Nucleo;
namespace Beto.Motor.Narrativa;

public sealed record RegistroPaso(
    Fase Origen, AccionJugador Entrada, Transicion Tomada,
    IReadOnlyList<Transicion> Candidatas, EstadoJuego Despues);

public sealed class AutomataNarrativo
{
    private readonly Dictionary<(Fase, AccionJugador), List<Transicion>> _delta = new();
    private readonly Random _azar;

    public AutomataNarrativo(int semilla) => _azar = new Random(semilla);

    public void Agregar(Fase desde, AccionJugador entrada, Transicion t)
    {
        if (!_delta.TryGetValue((desde, entrada), out var lista))
            _delta[(desde, entrada)] = lista = new List<Transicion>();
        lista.Add(t);
    }

    // δ(q, a): transiciones con guarda válida y de mayor prioridad
    public IReadOnlyList<Transicion> Candidatas(EstadoJuego e, AccionJugador a)
    {
        if (!_delta.TryGetValue((e.Fase, a), out var lista)) return Array.Empty<Transicion>();
        var validas = lista.Where(t => t.Guarda?.Invoke(e) ?? true).ToList();
        if (validas.Count == 0) return validas;
        int mayor = validas.Max(t => t.Prioridad);
        return validas.Where(t => t.Prioridad == mayor).ToList();
    }

    public IReadOnlyList<AccionJugador> Disponibles(EstadoJuego e) =>
        Enum.GetValues<AccionJugador>()
            .Where(a => a != AccionJugador.Epsilon && Candidatas(e, a).Count > 0)
            .ToList();

    // Aplica una transición concreta: ejecuta su efecto y cambia de fase
    private static EstadoJuego Aplicar(EstadoJuego e, Transicion t) =>
        t.Efecto(e) with { Fase = t.Destino ?? e.Fase };

    private RegistroPaso Disparar(EstadoJuego e, AccionJugador a)
    {
        var opciones = Candidatas(e, a);
        if (opciones.Count == 0)
            throw new InvalidOperationException($"Sin transicion: {e.Fase} --{a}-->");

        int tirada = _azar.Next(opciones.Sum(o => o.Peso)); // no determinismo ponderado
        var elegida = opciones[0];
        foreach (var o in opciones)
        {
            if (tirada < o.Peso) { elegida = o; break; }
            tirada -= o.Peso;
        }
        var siguiente = Aplicar(e, elegida);
        return new RegistroPaso(e.Fase, a, elegida, opciones, siguiente);
    }

    // Clausura-ε: dispara transiciones automáticas hasta que no haya más
    public IReadOnlyList<RegistroPaso> Estabilizar(EstadoJuego e)
    {
        var lista = new List<RegistroPaso>();
        for (int i = 0; i < 50 && Candidatas(e, AccionJugador.Epsilon).Count > 0; i++)
        {
            var paso = Disparar(e, AccionJugador.Epsilon);
            lista.Add(paso);
            e = paso.Despues;
        }
        return lista;
    }

    public IReadOnlyList<RegistroPaso> Avanzar(EstadoJuego e, AccionJugador a)
    {
        var primero = Disparar(e, a);
        var todos = new List<RegistroPaso> { primero };
        todos.AddRange(Estabilizar(primero.Despues));
        return todos;
    }

    // ---------- Para la prueba de alcanzabilidad (exploran todas las ramas, sin azar) ----------

    // Todas las ramas posibles de la clausura-ε
    public IEnumerable<EstadoJuego> Cierre(EstadoJuego e, int profundidad = 0)
    {
        var candidatas = Candidatas(e, AccionJugador.Epsilon);
        if (candidatas.Count == 0 || profundidad > 50) { yield return e; yield break; }
        foreach (var t in candidatas)
            foreach (var r in Cierre(Aplicar(e, t), profundidad + 1))
                yield return r;
    }

    // Todos los estados estables a los que se puede llegar ejecutando la acción
    public IEnumerable<EstadoJuego> SucesoresEstables(EstadoJuego e, AccionJugador a)
    {
        foreach (var t in Candidatas(e, a))
            foreach (var r in Cierre(Aplicar(e, t)))
                yield return r;
    }
}