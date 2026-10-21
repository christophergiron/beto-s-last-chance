using System.Collections.Immutable;
using System.Text.Json;
using Beto.Motor.Narrativa;
using Beto.Motor.Nucleo;

namespace Beto.Motor.Mundo;


//Un símbolo de la pila tal como se escribe en el JSON
public sealed record ObligacionGuardada(string Nombre, int Turnos);

public sealed record PartidaGuardada
{
    public int Version { get; init; } = GuardadoPartida.VersionActual;
    public string Fase { get; init; } = "S0_Inicio";
    public int Turno { get; init; }
    public int TurnosRestantes { get; init; }
    public int Preparacion { get; init; }
    public int RiesgoAcademico { get; init; }
    public int RiesgoSalud { get; init; }
    public int TurnosProyecto { get; init; }
    public int ProyectoConsecutivo { get; init; }
    public int OcioConsecutivo { get; init; }
    public int InactivoConsecutivo { get; init; }
    public bool ProyectoEntregado { get; init; }

    public List<ObligacionGuardada> Pila { get; init; } = new();
}

public static class GuardadoPartida
{
    public const int VersionActual = 1;

    // el alfabeto de la pila. Un guardado con otro símbolo no es un estado válido del APD.
    static readonly HashSet<string> Gamma = new() { "Examen", "Entrega", "Agotamiento", "Recuperacion" };
    // Símbolos que bloquean turnos (Turnos > 0); los otros dos tienen Turnos == 0.
    static readonly HashSet<string> Bloqueantes = new() { "Agotamiento", "Recuperacion" };

    static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

 

    public static PartidaGuardada ADto(EstadoJuego e) => new()
    {
        Version = VersionActual,
        Fase = e.Fase.ToString(),
        Turno = e.Turno,
        TurnosRestantes = e.TurnosRestantes,
        Preparacion = e.Preparacion,
        RiesgoAcademico = e.RiesgoAcademico,
        RiesgoSalud = e.RiesgoSalud,
        TurnosProyecto = e.TurnosProyecto,
        ProyectoConsecutivo = e.ProyectoConsecutivo,
        OcioConsecutivo = e.OcioConsecutivo,
        InactivoConsecutivo = e.InactivoConsecutivo,
        ProyectoEntregado = e.ProyectoEntregado,
        
        Pila = e.Pila.Select(o => new ObligacionGuardada(o.Nombre, o.Turnos)).ToList()
    };

    public static EstadoJuego DeDto(PartidaGuardada p)
    {
        Validar(p);

       
        var pila = ImmutableStack<Obligacion>.Empty;
        for (int i = p.Pila.Count - 1; i >= 0; i--)
            pila = pila.Push(new Obligacion(p.Pila[i].Nombre, p.Pila[i].Turnos));

        return new EstadoJuego
        {
            Fase = Enum.Parse<Fase>(p.Fase),
            Turno = p.Turno,
            TurnosRestantes = p.TurnosRestantes,
            Preparacion = p.Preparacion,
            RiesgoAcademico = p.RiesgoAcademico,
            RiesgoSalud = p.RiesgoSalud,
            TurnosProyecto = p.TurnosProyecto,
            ProyectoConsecutivo = p.ProyectoConsecutivo,
            OcioConsecutivo = p.OcioConsecutivo,
            InactivoConsecutivo = p.InactivoConsecutivo,
            ProyectoEntregado = p.ProyectoEntregado,
            Pila = pila
        };
    }


    static void Validar(PartidaGuardada p)
    {
        if (p.Version != VersionActual)
            throw new InvalidDataException($"Versión de guardado no soportada: {p.Version} (se esperaba {VersionActual}).");

        if (!Enum.TryParse<Fase>(p.Fase, out var fase) || !Enum.IsDefined(fase))
            throw new InvalidDataException($"Fase desconocida: '{p.Fase}'.");

        if (p.Pila is null)
            throw new InvalidDataException("El guardado no incluye la pila.");

        foreach (var o in p.Pila)
        {
            if (o is null || !Gamma.Contains(o.Nombre))
                throw new InvalidDataException(
                    $"Símbolo fuera de Γ en la pila: '{o?.Nombre}'. Permitidos: {string.Join(", ", Gamma)}.");

            bool bloquea = Bloqueantes.Contains(o.Nombre);
            if (bloquea && o.Turnos <= 0)
                throw new InvalidDataException($"{o.Nombre} debe tener Turnos > 0 (bloquea turnos).");
            if (!bloquea && o.Turnos != 0)
                throw new InvalidDataException($"{o.Nombre} debe tener Turnos == 0 (no bloquea).");
        }
    }

    //JSON y archivos

    public static string Serializar(EstadoJuego e) => JsonSerializer.Serialize(ADto(e), Opciones);

    public static EstadoJuego Deserializar(string json)
    {
        PartidaGuardada? dto;
        try { dto = JsonSerializer.Deserialize<PartidaGuardada>(json, Opciones); }
        catch (JsonException ex) { throw new InvalidDataException("El guardado no es un JSON válido: " + ex.Message, ex); }

        return DeDto(dto ?? throw new InvalidDataException("El guardado está vacío."));
    }

    public static void Guardar(EstadoJuego e, string ruta)
    {
        var carpeta = Path.GetDirectoryName(Path.GetFullPath(ruta));
        if (!string.IsNullOrEmpty(carpeta)) Directory.CreateDirectory(carpeta);
        File.WriteAllText(ruta, Serializar(e));
    }

    public static EstadoJuego Cargar(string ruta) => Deserializar(File.ReadAllText(ruta));

    public static string Describir(EstadoJuego e, AutomataNarrativo automata)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Fase: {e.Fase} | Turno {e.Turno} | Turnos restantes {e.TurnosRestantes}");

        var pila = e.Pila.IsEmpty
            ? "(vacía)"
            : string.Join(" · ", e.Pila.Select(o => o.Turnos > 0 ? $"{o.Nombre}({o.Turnos})" : o.Nombre));
        sb.AppendLine($"Pila (tope → fondo): {pila}");

        if (e.Tope is { } tope)
            sb.AppendLine(e.Bloqueado
                ? $"Tope: {tope.Nombre} — BLOQUEA: cualquier acción pierde el turno."
                : $"Tope: {tope.Nombre} — no bloquea.");

        sb.AppendLine($"Registros: Prep {e.Preparacion} | R.Acad {e.RiesgoAcademico} | R.Salud {e.RiesgoSalud} | " +
                      $"Proyecto {e.TurnosProyecto} | Entregado {e.ProyectoEntregado}");

        if (e.EsFinal) { sb.AppendLine("Estado FINAL (de aceptación)."); return sb.ToString(); }

        var acciones = automata.Disponibles(e);
        sb.AppendLine("Acciones disponibles:");
        foreach (var a in acciones)
            sb.AppendLine($"  {a} → {string.Join(" | ", automata.Candidatas(e, a).Select(t => t.Etiqueta))}");
        if (acciones.Count == 0) sb.AppendLine("  (ninguna: solo se disparan ε-transiciones)");

        var eps = automata.Candidatas(e, AccionJugador.Epsilon);
        if (eps.Count > 0)
        {
            sb.AppendLine("ε-transiciones pendientes:");
            foreach (var t in eps)
                sb.AppendLine($"  ε → {t.Destino ?? e.Fase} [{t.Etiqueta}, peso {t.Peso}]");
        }
        return sb.ToString();
    }
}
