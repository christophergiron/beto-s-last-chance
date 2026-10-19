using Beto.Motor.Finales;
using Beto.Motor.Gramatica;
using Beto.Motor.Narrativa;
using Beto.Motor.Nucleo;

// Modo análisis: dotnet run --project Beto.Console -- alcanzabilidad
if (args.Length > 0 && args[0] == "alcanzabilidad")
{
    var r = Alcanzabilidad.Analizar(ReglasBeto.Construir(semilla: 1));
    Console.WriteLine($"Estados explorados: {r.EstadosExplorados} | Callejones: {r.Callejones}");
    foreach (var (fase, seq) in r.SecuenciasMinimas.OrderBy(x => x.Key))
        Console.WriteLine($"{fase} ({seq.Count} acciones): {string.Join(" -> ", seq)}");
    return;
}

//Conexión con la gramatica
if (args.Length > 0 && args[0] == "gramatica")
{
    var simbolo = args.Length > 1 ? args[1] : "TEXTO_BETO";
    int semilla = args.Length > 2 && int.TryParse(args[2], out var valor) ? valor : 1;
    bool conTraza = args.Contains("traza");
    var res = TextosBeto.Gramatica.Derivar(simbolo, new Random(semilla), conTraza: conTraza);
    if (conTraza) foreach (var linea in res.Pasos) Console.WriteLine(linea);
    Console.WriteLine(res.Texto);
    return;
}

// Modo juego
var automata = ReglasBeto.Construir(semilla: Environment.TickCount);
var estado = new EstadoJuego();
foreach (var paso in automata.Estabilizar(estado)) { Mostrar(paso); estado = paso.Despues; }

var azarTexto = new Random();
while (!estado.EsFinal)
{
    var pila = string.Join(", ", estado.Pila.Select(o => $"{o.Nombre}({o.Turnos})"));
    Console.WriteLine($"\n[{estado.Fase}] Dia {estado.Turno} | Prep {estado.Preparacion} | " +
        $"R.Acad {estado.RiesgoAcademico} | R.Salud {estado.RiesgoSalud} | " +
        $"Proyecto {estado.TurnosProyecto}/4 | Pila (tope primero): [{pila}]");

    var opciones = automata.Disponibles(estado);
    for (int i = 0; i < opciones.Count; i++) Console.WriteLine($"  {i + 1}. {opciones[i]}");
    Console.Write("> ");
    if (!int.TryParse(Console.ReadLine(), out var k) || k < 1 || k > opciones.Count) continue;

    foreach (var paso in automata.Avanzar(estado, opciones[k - 1])) { Mostrar(paso); estado = paso.Despues; }
    var texto = TextosBeto.Generar(opciones[k - 1], azarTexto);
    if (texto is not null) Console.WriteLine($"  \"{texto}\"");
}
Console.WriteLine($"\nFIN: {estado.Fase}");

static void Mostrar(RegistroPaso p) =>
    Console.WriteLine($"  δ({p.Origen}, {p.Entrada}) -> {p.Despues.Fase}  [{p.Tomada.Etiqueta}]  candidatas: {p.Candidatas.Count}");