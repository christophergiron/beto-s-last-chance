using System.Text;
using System.Text.RegularExpressions;

namespace Beto.Motor.Gramatica;

public sealed record Simbolo(bool EsNoTerminal, string Valor);

public sealed record PasoDerivacion(string Regla, string Forma)
{
    public override string ToString() =>
        Regla.Length == 0 ? Forma : $"=> {Forma}    [{Regla}]";
}

public sealed record ResultadoDerivacion(
    string Texto, IReadOnlyList<PasoDerivacion> Pasos, int ProfundidadMaxima);

//Resultado del analisis estatico de la gramatica.
public sealed record AnalisisGramatica(
    string Axioma,
    IReadOnlyList<string> SinDefinir,
    IReadOnlyList<string> Inalcanzables,
    IReadOnlyList<string> Improductivos,
    IReadOnlyList<string> Recursivos)
{
    public bool EsValida =>
        SinDefinir.Count == 0 && Inalcanzables.Count == 0 && Improductivos.Count == 0;
}

public sealed class GramaticaInvalidaException : Exception
{
    public GramaticaInvalidaException(string mensaje) : base(mensaje) { }
}

//Generador de texto a partir de una gramatica libre de contexto en BNF.
public sealed class GeneradorGLC
{
    public const int ProfundidadPorDefecto = 12;

    private const int Infinito = int.MaxValue / 2;
    private const string RecursoBnf = "Beto.Motor.Gramatica.gramatica.bnf";

    private static readonly Regex Encabezado =
        new(@"^<([A-Za-z0-9_]+)>\s*::=(.*)$", RegexOptions.Compiled);
    private static readonly Regex NombreValido =
        new(@"^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    private sealed class Alternativa
    {
        public Alternativa(Simbolo[] simbolos) => Simbolos = simbolos;
        public Simbolo[] Simbolos { get; }
        public int Altura { get; set; } = Infinito;
    }

    private readonly record struct Item(Simbolo Sim, int Restante);

    private readonly Dictionary<string, List<Alternativa>> _reglas;
    private readonly List<string> _orden;
    private readonly Dictionary<string, int> _altura = new();

    private GeneradorGLC(Dictionary<string, List<Alternativa>> reglas, List<string> orden)
    {
        _reglas = reglas;
        _orden = orden;
        CalcularAlturas();
    }

    //El axioma es la primera regla del archivo.
    public string Axioma => _orden[0];

    //No terminales definidos, en el orden del archivo.
    public IReadOnlyList<string> NoTerminales => _orden;

    public static GeneradorGLC DesdeArchivo(string ruta) =>
        DesdeTexto(File.ReadAllText(ruta, Encoding.UTF8));

    //Carga gramatica.bnf, que se encontrara incrustado dentro de Beto.Motor.
    public static GeneradorGLC DesdeRecursoEmbebido()
    {
        using var flujo = typeof(GeneradorGLC).Assembly.GetManifestResourceStream(RecursoBnf)
            ?? throw new InvalidOperationException($"No se encontro el recurso incrustado '{RecursoBnf}'.");
        using var lector = new StreamReader(flujo, Encoding.UTF8);
        return DesdeTexto(lector.ReadToEnd());
    }

    public static GeneradorGLC DesdeTexto(string bnf)
    {
        ArgumentNullException.ThrowIfNull(bnf);

        var reglas = new Dictionary<string, List<Alternativa>>();
        var orden = new List<string>();
        string? actual = null;
        var cuerpo = new StringBuilder();
        int lineaRegla = 0;

        void Cerrar()
        {
            if (actual is null) return;
            if (reglas.ContainsKey(actual))
                throw new GramaticaInvalidaException($"Linea {lineaRegla}: <{actual}> esta definida mas de una vez.");
            reglas[actual] = ParsearCuerpo(cuerpo.ToString(), lineaRegla);
            orden.Add(actual);
        }

        var lineas = bnf.Split('\n');
        for (int i = 0; i < lineas.Length; i++)
        {
            var t = lineas[i].Trim();
            if (t.Length == 0 || t[0] == '#') continue;

            var m = Encabezado.Match(t);
            if (m.Success)
            {
                Cerrar();
                actual = m.Groups[1].Value;
                lineaRegla = i + 1;
                cuerpo.Clear().Append(m.Groups[2].Value);
            }
            else if (t[0] == '|' && actual is not null)
            {
                cuerpo.Append(' ').Append(t);
            }
            else
            {
                throw new GramaticaInvalidaException(
                    $"Linea {i + 1}: se esperaba '<NOMBRE> ::= ...' o una continuacion que empiece con '|'.");
            }
        }
        Cerrar();

        if (orden.Count == 0)
            throw new GramaticaInvalidaException("La gramatica no tiene ninguna regla.");
        return new GeneradorGLC(reglas, orden);
    }

    private static List<Alternativa> ParsearCuerpo(string s, int linea)
    {
        var alts = new List<List<Simbolo>> { new() };
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == '|') { alts.Add(new List<Simbolo>()); i++; continue; }

            if (c == '<')
            {
                int fin = s.IndexOf('>', i + 1);
                if (fin < 0)
                    throw new GramaticaInvalidaException($"Linea {linea}: falta '>' para cerrar un no terminal.");
                var nombre = s.Substring(i + 1, fin - i - 1);
                if (!NombreValido.IsMatch(nombre))
                    throw new GramaticaInvalidaException($"Linea {linea}: nombre de no terminal invalido '<{nombre}>'.");
                alts[^1].Add(new Simbolo(true, nombre));
                i = fin + 1;
                continue;
            }

            if (c == '"')
            {
                var sb = new StringBuilder();
                bool cerrado = false;
                i++;
                while (i < s.Length)
                {
                    char ch = s[i];
                    if (ch == '\\' && i + 1 < s.Length) { sb.Append(s[i + 1]); i += 2; continue; }
                    if (ch == '"') { cerrado = true; i++; break; }
                    sb.Append(ch);
                    i++;
                }
                if (!cerrado)
                    throw new GramaticaInvalidaException($"Linea {linea}: falta '\"' para cerrar un terminal.");
                alts[^1].Add(new Simbolo(false, sb.ToString()));
                continue;
            }

            throw new GramaticaInvalidaException($"Linea {linea}: caracter inesperado '{c}'.");
        }

        if (alts.Any(a => a.Count == 0))
            throw new GramaticaInvalidaException(
                $"Linea {linea}: alternativa vacia (usa \"\" para la cadena vacia).");

        return alts.Select(a => new Alternativa(a.ToArray())).ToList();
    }

    public string Generar(string simbolo, Random azar, int limiteProfundidad = ProfundidadPorDefecto) =>
        Derivar(simbolo, azar, limiteProfundidad).Texto;

    public ResultadoDerivacion Derivar(string simbolo, Random azar,
        int limiteProfundidad = ProfundidadPorDefecto, bool conTraza = false)
    {
        ArgumentNullException.ThrowIfNull(azar);
        var nombre = Normalizar(simbolo);

        if (!_reglas.ContainsKey(nombre))
            throw new ArgumentException($"El simbolo <{nombre}> no esta definido en la gramatica.", nameof(simbolo));
        int minima = _altura[nombre];
        if (minima >= Infinito)
            throw new InvalidOperationException(
                $"<{nombre}> no deriva ninguna cadena de terminales (recursion sin salida o simbolos sin definir).");
        if (limiteProfundidad < minima)
            throw new ArgumentOutOfRangeException(nameof(limiteProfundidad), limiteProfundidad,
                $"<{nombre}> necesita una profundidad de al menos {minima}.");

        var forma = new List<Item> { new(new Simbolo(true, nombre), limiteProfundidad) };
        var pasos = new List<PasoDerivacion>();
        if (conTraza) pasos.Add(new PasoDerivacion(string.Empty, RenderizarForma(forma)));

        int profundidadMaxima = 0;
        int pos = 0;
        while (true)
        {
            while (pos < forma.Count && !forma[pos].Sim.EsNoTerminal) pos++;
            if (pos == forma.Count) break;

            var (sim, restante) = forma[pos];
            var permitidas = _reglas[sim.Valor].Where(a => a.Altura <= restante).ToList();
            var elegida = permitidas[azar.Next(permitidas.Count)];

            profundidadMaxima = Math.Max(profundidadMaxima, limiteProfundidad - restante + 1);
            forma.RemoveAt(pos);
            forma.InsertRange(pos, elegida.Simbolos.Select(x => new Item(x, restante - 1)));

            if (conTraza)
                pasos.Add(new PasoDerivacion(RenderizarRegla(sim.Valor, elegida), RenderizarForma(forma)));
        }

        var texto = string.Concat(forma.Select(it => it.Sim.Valor));
        return new ResultadoDerivacion(texto, pasos, profundidadMaxima);
    }

    public int? AlturaMinima(string simbolo)
    {
        var nombre = Normalizar(simbolo);
        if (!_altura.TryGetValue(nombre, out var h))
            throw new ArgumentException($"El simbolo <{nombre}> no esta definido en la gramatica.", nameof(simbolo));
        return h >= Infinito ? null : h;
    }

    public IEnumerable<string> ListarReglas() =>
        _orden.SelectMany(n => _reglas[n].Select(a => RenderizarRegla(n, a)));

    //Seccion de analisis
    public bool EsRecursivo(string simbolo)
    {
        var nombre = Normalizar(simbolo);
        return _reglas.ContainsKey(nombre) && Cierre(Hijos(nombre)).Contains(nombre);
    }

    public AnalisisGramatica Analizar(IEnumerable<string>? raices = null)
    {
        var inicio = raices is null ? new List<string> { Axioma } : raices.Select(Normalizar).ToList();

        var usados = new HashSet<string>(_orden.SelectMany(Hijos));
        var sinDefinir = usados.Where(u => !_reglas.ContainsKey(u))
                               .OrderBy(u => u, StringComparer.Ordinal).ToList();

        var alcanzables = Cierre(inicio);
        var inalcanzables = _orden.Where(n => !alcanzables.Contains(n)).ToList();
        var improductivos = _orden.Where(n => _altura[n] >= Infinito).ToList();
        var recursivos = _orden.Where(EsRecursivo).ToList();

        return new AnalisisGramatica(Axioma, sinDefinir, inalcanzables, improductivos, recursivos);
    }

    private IEnumerable<string> Hijos(string nombre) =>
        _reglas[nombre].SelectMany(a => a.Simbolos).Where(s => s.EsNoTerminal).Select(s => s.Valor);

    private HashSet<string> Cierre(IEnumerable<string> semillas)
    {
        var vistos = new HashSet<string>();
        var pila = new Stack<string>(semillas);
        while (pila.Count > 0)
        {
            var n = pila.Pop();
            if (!_reglas.ContainsKey(n) || !vistos.Add(n)) continue;
            foreach (var h in Hijos(n)) pila.Push(h);
        }
        return vistos;
    }

    private void CalcularAlturas()
    {
        foreach (var n in _orden) _altura[n] = Infinito;
        bool cambio;
        do
        {
            cambio = false;
            foreach (var n in _orden)
                foreach (var alt in _reglas[n])
                {
                    int max = 0;
                    foreach (var s in alt.Simbolos)
                        if (s.EsNoTerminal) max = Math.Max(max, AlturaDe(s.Valor));
                    int v = max >= Infinito ? Infinito : 1 + max;
                    alt.Altura = v;
                    if (v < _altura[n]) { _altura[n] = v; cambio = true; }
                }
        } while (cambio);
    }

    private int AlturaDe(string nombre) => _altura.TryGetValue(nombre, out var h) ? h : Infinito;
    private static string Normalizar(string simbolo)
    {
        ArgumentNullException.ThrowIfNull(simbolo);
        var s = simbolo.Trim();
        if (s.Length >= 2 && s[0] == '<' && s[^1] == '>') s = s[1..^1];
        return s;
    }

    private static string RenderizarForma(List<Item> forma) =>
        string.Concat(forma.Select(it => it.Sim.EsNoTerminal ? $"<{it.Sim.Valor}>" : it.Sim.Valor));

    private static string RenderizarRegla(string nombre, Alternativa alt) =>
        $"<{nombre}> ::= " + string.Join(" ", alt.Simbolos.Select(s =>
            s.EsNoTerminal
                ? $"<{s.Valor}>"
                : "\"" + s.Valor.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""));
}
