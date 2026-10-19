using Beto.Motor.Gramatica;
using Beto.Motor.Nucleo;
using Xunit;

public class PruebasGramatica
{
    static readonly string[] Entradas =
        { "TEXTO_BETO", "INVITACION", "INVITACION_ESTUDIO", "MENSAJE_PROFESOR", "MISION" };

    static GeneradorGLC G => TextosBeto.Gramatica;

    [Fact]
    public void Misma_semilla_da_el_mismo_texto()
    {
        foreach (var simbolo in Entradas)
            for (int semilla = 0; semilla < 200; semilla++)
                Assert.Equal(G.Generar(simbolo, new Random(semilla)),
                             G.Generar(simbolo, new Random(semilla)));
    }

    [Fact]
    public void Misma_semilla_da_la_misma_traza()
    {
        foreach (var simbolo in Entradas)
            for (int semilla = 0; semilla < 50; semilla++)
            {
                var a = G.Derivar(simbolo, new Random(semilla), conTraza: true);
                var b = G.Derivar(simbolo, new Random(semilla), conTraza: true);
                Assert.Equal(a.Texto, b.Texto);
                Assert.Equal(a.Pasos, b.Pasos);
            }
    }

    [Fact]
    public void Semillas_distintas_dan_variedad()
    {
        var textos = Enumerable.Range(0, 300)
            .Select(s => G.Generar("TEXTO_BETO", new Random(s)))
            .ToHashSet();
        Assert.True(textos.Count > 50, $"Solo {textos.Count} textos distintos en 300 semillas");
    }

    [Fact]
    public void Tiene_al_menos_15_reglas()
    {
        Assert.True(G.NoTerminales.Count >= 15, $"Solo {G.NoTerminales.Count} reglas");
    }

    [Fact]
    public void Todo_no_terminal_usado_esta_definido()
    {
        Assert.Empty(G.Analizar().SinDefinir.ToArray());
    }

    [Fact]
    public void No_hay_simbolos_inalcanzables()
    {
        Assert.Empty(G.Analizar().Inalcanzables.ToArray());
    }

    [Fact]
    public void Los_simbolos_de_entrada_del_motor_estan_definidos_y_son_alcanzables_desde_el_axioma()
    {
        foreach (var s in Entradas) Assert.Contains(s, G.NoTerminales);
        Assert.Empty(G.Analizar(new[] { G.Axioma }).Inalcanzables);
    }

    [Fact]
    public void No_hay_recursion_infinita_todo_simbolo_deriva_una_cadena_de_terminales()
    {
        var analisis = G.Analizar();
        Assert.Empty(analisis.Improductivos);
        foreach (var n in G.NoTerminales) Assert.NotNull(G.AlturaMinima(n));
        Assert.True(analisis.EsValida);
    }

    [Fact]
    public void Ninguna_derivacion_se_pasa_del_limite_de_profundidad()
    {
        foreach (var simbolo in Entradas)
        {
            int minima = G.AlturaMinima(simbolo)!.Value;
            for (int limite = minima; limite <= minima + 6; limite++)
                for (int semilla = 0; semilla < 200; semilla++)
                {
                    var r = G.Derivar(simbolo, new Random(semilla), limite);
                    Assert.True(r.ProfundidadMaxima <= limite);
                    Assert.False(string.IsNullOrWhiteSpace(r.Texto));
                }
        }
    }

    [Fact]
    public void Un_limite_menor_al_minimo_se_rechaza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => G.Generar("MISION", new Random(1), 1));
    }

    [Fact]
    public void Simbolo_inexistente_se_rechaza()
    {
        Assert.Throws<ArgumentException>(() => G.Generar("NO_EXISTE", new Random(1)));
    }

    [Fact]
    public void Acepta_el_simbolo_con_o_sin_signos_menor_y_mayor()
    {
        Assert.Equal(G.Generar("MISION", new Random(7)), G.Generar("<MISION>", new Random(7)));
    }

    static int ProfundidadDeAnidamiento(string texto, out bool equilibrado)
    {
        var pila = new Stack<char>();
        int max = 0;
        equilibrado = true;
        foreach (char c in texto)
        {
            if (c == '(' || c == '[') { pila.Push(c); max = Math.Max(max, pila.Count); }
            else if (c == ')' || c == ']')
            {
                if (pila.Count == 0 || (c == ')') != (pila.Pop() == '(')) { equilibrado = false; return max; }
            }
        }
        equilibrado = pila.Count == 0;
        return max;
    }

    [Fact]
    public void MISION_es_recursiva()
    {
        Assert.True(G.EsRecursivo("MISION"));
        Assert.Contains("MISION", G.Analizar().Recursivos);
        Assert.False(G.EsRecursivo("AMIGO"));
    }

    [Fact]
    public void Hay_misiones_que_contienen_otras_misiones_a_varios_niveles()
    {
        int maximo = 0;
        for (int semilla = 0; semilla < 3000; semilla++)
        {
            var texto = G.Generar("MISION", new Random(semilla));
            maximo = Math.Max(maximo, ProfundidadDeAnidamiento(texto, out _));
        }
        Assert.True(maximo >= 3, $"Anidamiento maximo observado: {maximo}");
    }

    [Fact]
    public void Los_parentesis_y_corchetes_de_las_misiones_siempre_quedan_balanceados()
    {
        foreach (var simbolo in new[] { "MISION", "MENSAJE_PROFESOR", "TEXTO_BETO" })
            for (int semilla = 0; semilla < 1000; semilla++)
            {
                var texto = G.Generar(simbolo, new Random(semilla));
                ProfundidadDeAnidamiento(texto, out bool ok);
                Assert.True(ok, $"Desbalanceado: {texto}");
            }
    }

    [Fact]
    public void Con_la_profundidad_minima_la_recursion_no_se_dispara()
    {
        int minima = G.AlturaMinima("MISION")!.Value;
        for (int semilla = 0; semilla < 500; semilla++)
        {
            var texto = G.Generar("MISION", new Random(semilla), minima);
            Assert.DoesNotContain("(", texto);
            Assert.DoesNotContain("[", texto);
        }
    }

    [Fact]
    public void La_traza_empieza_en_el_simbolo_y_termina_en_el_texto()
    {
        var r = G.Derivar("INVITACION", new Random(3), conTraza: true);
        Assert.Equal("<INVITACION>", r.Pasos[0].Forma);
        Assert.Equal(r.Texto, r.Pasos[^1].Forma);
        Assert.True(r.Pasos.Count > 1);
    }

    [Fact]
    public void Sin_pedir_traza_la_lista_de_pasos_esta_vacia()
    {
        Assert.Empty(G.Derivar("INVITACION", new Random(3)).Pasos);
    }

    [Fact]
    public void Cada_paso_de_la_traza_usa_una_produccion_real_de_la_gramatica()
    {
        var reglas = G.ListarReglas().ToHashSet();
        foreach (var simbolo in Entradas)
            for (int semilla = 0; semilla < 100; semilla++)
            {
                var r = G.Derivar(simbolo, new Random(semilla), conTraza: true);
                foreach (var paso in r.Pasos.Skip(1)) Assert.Contains(paso.Regla, reglas);
            }
    }

    //Integracion con el motor
    [Fact]
    public void Toda_accion_del_jugador_tiene_un_simbolo_definido_y_genera_texto()
    {
        foreach (var accion in Enum.GetValues<AccionJugador>().Where(a => a != AccionJugador.Epsilon))
        {
            var simbolo = TextosBeto.SimboloPara(accion);
            Assert.NotNull(simbolo);
            Assert.Contains(simbolo, G.NoTerminales);
            Assert.False(string.IsNullOrWhiteSpace(TextosBeto.Generar(accion, new Random(1))));
        }
    }

    [Fact]
    public void Epsilon_no_tiene_texto()
    {
        Assert.Null(TextosBeto.SimboloPara(AccionJugador.Epsilon));
        Assert.Null(TextosBeto.Generar(AccionJugador.Epsilon, new Random(1)));
    }

    //Detector de gramaticas defectuosas
    [Fact]
    public void Detecta_no_terminales_sin_definir()
    {
        var g = GeneradorGLC.DesdeTexto("<S> ::= <A> \"x\"");
        Assert.Equal(new[] { "A" }, g.Analizar().SinDefinir.ToArray());
        Assert.False(g.Analizar().EsValida);
    }

    [Fact]
    public void Detecta_simbolos_inalcanzables()
    {
        var g = GeneradorGLC.DesdeTexto("<S> ::= \"x\"\n<Z> ::= \"y\"");
        Assert.Equal(new[] { "Z" }, g.Analizar().Inalcanzables.ToArray());
    }

    [Fact]
    public void Detecta_recursion_sin_salida_y_nunca_la_elige()
    {
        var g = GeneradorGLC.DesdeTexto("<S> ::= \"x\" | <A>\n<A> ::= <A> \"y\"");
        Assert.Equal(new[] { "A" }, g.Analizar().Improductivos.ToArray());
        Assert.Null(g.AlturaMinima("A"));
        Assert.Throws<InvalidOperationException>(() => g.Generar("A", new Random(1)));
        for (int semilla = 0; semilla < 100; semilla++)
            Assert.Equal("x", g.Generar("S", new Random(semilla)));
    }

    [Fact]
    public void El_lector_de_BNF_entiende_comentarios_continuaciones_escapes_y_epsilon()
    {
        var g = GeneradorGLC.DesdeTexto("""
            # comentario
            <S> ::= "a" <B>
                 | "\"c\""
            <B> ::= "b" | ""
            """);
        Assert.Equal("S", g.Axioma);
        var posibles = new HashSet<string> { "ab", "a", "\"c\"" };
        for (int semilla = 0; semilla < 100; semilla++)
            Assert.Contains(g.Generar("S", new Random(semilla)), posibles);
    }

    [Theory]
    [InlineData("<S> ::= \"abierto")]
    [InlineData("<S> ::= <A")]
    [InlineData("<S> ::= ")]
    [InlineData("<S> ::= \"a\" |")]
    [InlineData("texto suelto")]
    [InlineData("<S> ::= \"a\"\n<S> ::= \"b\"")]
    [InlineData("")]
    public void Una_gramatica_mal_escrita_se_rechaza(string bnf)
    {
        Assert.Throws<GramaticaInvalidaException>(() => GeneradorGLC.DesdeTexto(bnf));
    }
}
