using System.Collections.Immutable;
using System.Text.Json;
using Beto.Motor.Mundo;
using Beto.Motor.Narrativa;
using Beto.Motor.Nucleo;
using Xunit;

public class PruebasGuardado
{
    static ImmutableStack<Obligacion> Armar(params Obligacion[] fondoATope)
    {
        var pila = ImmutableStack<Obligacion>.Empty;
        foreach (var o in fondoATope) pila = pila.Push(o);
        return pila;
    }

    // Estado con pila anidada y registros para detectar campos que se pierdan.
    static EstadoJuego Ejemplo() => new EstadoJuego
    {
        Fase = Fase.S2_TrabajoProyecto,
        Turno = 9, TurnosRestantes = 6, Preparacion = 37, RiesgoAcademico = 22, RiesgoSalud = 41,
        TurnosProyecto = 2, ProyectoConsecutivo = 1, OcioConsecutivo = 3, InactivoConsecutivo = 2,
        ProyectoEntregado = false,
        Pila = Armar(new Obligacion("Examen", 0), new Obligacion("Entrega", 0), new Obligacion("Agotamiento", 2), new Obligacion("Recuperacion", 4))
    };

    // EstadoJuego es un record, pero ImmutableStack se compara por referencia:
    // se compara la pila elemento a elemento y el resto vaciando la pila de ambos.
    static void AssertMismoEstado(EstadoJuego esperado, EstadoJuego real)
    {
        Assert.Equal(esperado.Pila.ToArray(), real.Pila.ToArray());
        var vacia = ImmutableStack<Obligacion>.Empty;
        Assert.Equal(esperado with { Pila = vacia }, real with { Pila = vacia });
    }

    [Fact]
    public void Ida_y_vuelta_conserva_registros_y_orden_de_la_pila()
    {
        var original = Ejemplo();

        var cargado = GuardadoPartida.Deserializar(GuardadoPartida.Serializar(original));

        AssertMismoEstado(original, cargado);
        Assert.Equal("Recuperacion", cargado.Tope!.Nombre);         
        Assert.Equal("Examen", cargado.Pila.Last().Nombre);          
    }

    [Fact]
    public void La_pila_se_guarda_como_lista_con_el_tope_primero()
    {
        using var doc = JsonDocument.Parse(GuardadoPartida.Serializar(Ejemplo()));
        var pila = doc.RootElement.GetProperty("Pila");

        Assert.Equal(JsonValueKind.Array, pila.ValueKind);
        Assert.Equal(4, pila.GetArrayLength());
        Assert.Equal("Recuperacion", pila[0].GetProperty("Nombre").GetString());
        Assert.Equal("Examen", pila[3].GetProperty("Nombre").GetString());
    }

    [Fact]
    public void No_se_guardan_las_propiedades_derivadas()
    {
        var json = GuardadoPartida.Serializar(Ejemplo());

        Assert.DoesNotContain("PreparacionEfectiva", json);
        Assert.DoesNotContain("\"Tope\"", json);
        Assert.DoesNotContain("Bloqueado", json);
        Assert.DoesNotContain("EsFinal", json);
    }

    [Fact]
    public void Se_puede_guardar_en_archivo_y_cargar()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"beto_{Guid.NewGuid():N}.json");
        try
        {
            var original = Ejemplo();
            GuardadoPartida.Guardar(original, ruta);
            AssertMismoEstado(original, GuardadoPartida.Cargar(ruta));
        }
        finally { if (File.Exists(ruta)) File.Delete(ruta); }
    }

    [Fact]
    public void Un_estado_cargado_sigue_jugando_con_las_mismas_reglas()
    {
        var automata = ReglasBeto.Construir(semilla: 1);
        var cargado = GuardadoPartida.Deserializar(GuardadoPartida.Serializar(Ejemplo()));

        var despues = automata.Avanzar(cargado, AccionJugador.Estudiar).Last().Despues;

    
        Assert.Equal("Recuperacion", despues.Tope!.Nombre);
        Assert.Equal(3, despues.Tope!.Turnos);
    }

    [Fact]
    public void Rechaza_un_simbolo_fuera_de_Gamma()
    {
        var json = GuardadoPartida.Serializar(Ejemplo()).Replace("\"Entrega\"", "\"Mochila\"");
        Assert.Throws<InvalidDataException>(() => GuardadoPartida.Deserializar(json));
    }

    [Fact]
    public void Rechaza_una_fase_inexistente()
    {
        var json = GuardadoPartida.Serializar(Ejemplo()).Replace("S2_TrabajoProyecto", "S99_Nada");
        Assert.Throws<InvalidDataException>(() => GuardadoPartida.Deserializar(json));
    }

    [Fact]
    public void Rechaza_una_obligacion_bloqueante_sin_turnos()
    {
        var malo = Ejemplo() with { Pila = Armar(new Obligacion("Examen", 0), new Obligacion("Agotamiento", 0)) };
        Assert.Throws<InvalidDataException>(() => GuardadoPartida.Deserializar(GuardadoPartida.Serializar(malo)));
    }

    [Fact]
    public void Rechaza_un_json_corrupto()
    {
        Assert.Throws<InvalidDataException>(() => GuardadoPartida.Deserializar("{ esto no es json"));
    }
}
