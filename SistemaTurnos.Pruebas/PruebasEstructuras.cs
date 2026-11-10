using System.Diagnostics;
using SistemaTurnos.Core.Estructuras;
using SistemaTurnos.Core.Modelo;

namespace SistemaTurnos.Pruebas;

public static class PruebasEstructuras
{
	[Prueba("Montículo: extrae 10 000 valores aleatorios en orden ascendente", "RF04")]
	public static string MonticuloOrden()
	{
		var rnd = new Random(1);
		var m = new MonticuloMinimo<int>(Comparer<int>.Default);
		for (int i = 0; i < 10_000; i++) m.Insertar(rnd.Next(1_000_000));
		Afirmar.Verdadero(m.EsValido(), "La propiedad de montículo no se cumple tras insertar.");
		int anterior = int.MinValue;
		while (!m.EstaVacio)
		{
			int x = m.ExtraerMinimo();
			Afirmar.Verdadero(x >= anterior, "Los valores no salieron en orden.");
			anterior = x;
		}
		return "10 000 inserciones y extracciones en orden correcto.";
	}

	[Prueba("Montículo: con igual nivel se respeta el orden de llegada (T-03 antes que T-12)", "RF04")]
	public static string MonticuloEmpate()
	{
		var p = new Paciente(Cedula.Parse("1-1111-1111"), "Prueba Uno", new DateTime(1990, 1, 1));
		var h = new DateTime(2026, 11, 8, 8, 0, 0);
		var m = new MonticuloMinimo<Turno>(ComparadorTurnos.Instancia);
		var t12 = new Turno("T-12", p, NivelPrioridad.Rojo, "x", false, h.AddMinutes(30));
		var t03 = new Turno("T-03", p, NivelPrioridad.Rojo, "x", false, h.AddMinutes(5));
		var t07 = new Turno("T-07", p, NivelPrioridad.Azul, "x", false, h.AddMinutes(40));
		var t05 = new Turno("T-05", p, NivelPrioridad.Verde, "x", false, h);
		foreach (var t in new[] { t12, t03, t07, t05 }) m.Insertar(t);
		string orden = string.Join(" → ", Enumerable.Range(0, 4).Select(_ => m.ExtraerMinimo().Codigo));
		Afirmar.Igual("T-07 → T-03 → T-12 → T-05", orden, "Orden de atención:");
		return orden;
	}

	[Prueba("Montículo: el paciente preferencial pasa antes dentro de su mismo nivel", "RF03")]
	public static string MonticuloPreferencial()
	{
		var p = new Paciente(Cedula.Parse("1-1111-1111"), "Prueba Uno", new DateTime(1990, 1, 1));
		var h = new DateTime(2026, 11, 8, 8, 0, 0);
		var m = new MonticuloMinimo<Turno>(ComparadorTurnos.Instancia);
		m.Insertar(new Turno("T-01", p, NivelPrioridad.Amarillo, "x", false, h));
		m.Insertar(new Turno("T-02", p, NivelPrioridad.Amarillo, "x", true, h.AddMinutes(10)));
		Afirmar.Igual("T-02", m.ExtraerMinimo().Codigo);
		return "El turno preferencial T-02 salió antes que T-01 (mismo nivel 3).";
	}

	[Prueba("Montículo: eliminar un elemento intermedio conserva la propiedad de montículo")]
	public static void MonticuloEliminar()
	{
		var m = new MonticuloMinimo<int>(Comparer<int>.Default);
		foreach (var x in new[] { 5, 3, 8, 1, 9, 2, 7 }) m.Insertar(x);
		Afirmar.Verdadero(m.Eliminar(x => x == 3), "No se encontró el elemento.");
		Afirmar.Verdadero(m.EsValido(), "La propiedad de montículo se rompió.");
		Afirmar.Igual("1,2,5,7,8,9", string.Join(",", m.ComoListaOrdenada()));
	}

	[Prueba("Lista doble: insertar, eliminar un nodo intermedio y recorrer en ambos sentidos", "RF08")]
	public static string ListaDoble()
	{
		var l = new ListaDobleEnlazada<string>();
		l.AgregarAlFinal("B");
		var c = l.AgregarAlFinal("C");
		l.AgregarAlInicio("A");
		l.AgregarAlFinal("D");
		l.Eliminar(c);
		Afirmar.Igual("A,B,D", string.Join(",", l));
		Afirmar.Igual("D,B,A", string.Join(",", l.RecorridoInverso()));
		Afirmar.Igual(3, l.Count);
		Afirmar.Verdadero(c.Anterior is null && c.Siguiente is null, "El nodo eliminado conserva referencias.");
		return "Adelante: A,B,D · Atrás: D,B,A";
	}

	[Prueba("Árbol AVL: 10 000 cédulas en orden creciente quedan balanceadas", "RF02")]
	public static string ArbolBalanceado()
	{
		var a = new ArbolAVL<string, int>();
		for (int i = 0; i < 10_000; i++) a.Insertar($"1-{i / 10000:0000}-{i:0000}", i);
		Afirmar.Verdadero(a.EsValido(), "El árbol no está ordenado o no está balanceado.");
		double limite = 1.45 * Math.Log2(10_000 + 2);
		Afirmar.Verdadero(a.Altura <= limite, $"Altura {a.Altura} mayor que el límite AVL {limite:0.0}.");
		Afirmar.Verdadero(a.TryBuscar("1-0000-5000", out int v) && v == 5000, "No encontró la clave.");
		Afirmar.Verdadero(!a.Contiene("9-9999-9999"), "Encontró una clave inexistente.");
		return $"Altura {a.Altura} para 10 000 nodos (un ABB sin balancear tendría altura 10 000).";
	}

	[Prueba("Rendimiento: insertar y extraer en el montículo con 100 000 turnos tarda menos de 1 ms por operación", "RNF02")]
	public static string Rendimiento()
	{
		var p = new Paciente(Cedula.Parse("1-1111-1111"), "Prueba Uno", new DateTime(1990, 1, 1));
		var rnd = new Random(3);
		var h = new DateTime(2026, 11, 8);
		var turnos = Enumerable.Range(0, 100_000)
			.Select(i => new Turno($"T-{i}", p, (NivelPrioridad)rnd.Next(1, 6), "x", rnd.NextDouble() < .15, h.AddSeconds(i)))
			.ToList();
		var m = new MonticuloMinimo<Turno>(ComparadorTurnos.Instancia);
		var sw = Stopwatch.StartNew();
		foreach (var t in turnos) m.Insertar(t);
		sw.Stop();
		double usIns = sw.Elapsed.TotalMilliseconds * 1000 / turnos.Count;
		sw.Restart();
		while (!m.EstaVacio) m.ExtraerMinimo();
		sw.Stop();
		double usExt = sw.Elapsed.TotalMilliseconds * 1000 / turnos.Count;
		Afirmar.Verdadero(usIns < 1000 && usExt < 1000, "Alguna operación superó 1 ms.");
		return $"Promedio: inserción {usIns:0.00} µs, extracción {usExt:0.00} µs (meta: < 1000 µs).";
	}
}
