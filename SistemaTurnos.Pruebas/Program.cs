using System.Globalization;
using System.Text;
using SistemaTurnos.Pruebas;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("es-CR");
Console.OutputEncoding = Encoding.UTF8;

var salida = new StringWriter();
var doble = new EscritorDoble(Console.Out, salida);
doble.WriteLine("PRUEBAS AUTOMATIZADAS — Sistema de gestión priorizada de turnos");
doble.WriteLine($"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss} · .NET {Environment.Version} · {Environment.ProcessorCount} núcleos lógicos");

var clases = new List<Type> { typeof(PruebasEstructuras), typeof(PruebasSistema) };
#if !SIN_BASE_DATOS
clases.Add(typeof(PruebasBaseDatos));
#endif
int fallos = Ejecutor.Ejecutar(doble, clases.ToArray());

string archivo = Path.Combine(AppContext.BaseDirectory, "resultados-pruebas.txt");
File.WriteAllText(archivo, salida.ToString(), Encoding.UTF8);
Console.WriteLine($"\nResultados guardados en: {archivo}");
return fallos == 0 ? 0 : 1;

internal sealed class EscritorDoble : TextWriter
{
	private readonly TextWriter _a, _b;
	public EscritorDoble(TextWriter a, TextWriter b) { _a = a; _b = b; }
	public override Encoding Encoding => Encoding.UTF8;
	public override void Write(char value) { _a.Write(value); _b.Write(value); }
	public override void Write(string? value) { _a.Write(value); _b.Write(value); }
	public override void WriteLine(string? value) { _a.WriteLine(value); _b.WriteLine(value); }
}
