using System.Collections.Immutable;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SistemaTurnos.Core.Logica;

/// <summary>
/// Motor de inferencia al estilo Prolog (paradigma lógico): resolución SLD con
/// encadenamiento hacia atrás, unificación y vuelta atrás (backtracking).
/// Las sustituciones son inmutables, por lo que cada rama de búsqueda es independiente.
/// Predicados predefinidos: &gt;=, &gt;, &lt;, &lt;=, = (unificación) y \= (diferencia).
/// </summary>
public class MotorLogico
{
	private readonly List<Clausula> _baseConocimiento = new();
	private int _contadorRenombrado;
	private const int ProfundidadMaxima = 64;

	public IReadOnlyList<Clausula> Clausulas => _baseConocimiento;

	public void Agregar(Clausula clausula) => _baseConocimiento.Add(clausula);

	public void AgregarHecho(string functor, params Termino[] argumentos) =>
		Agregar(new Clausula(new Estructura(functor, argumentos), ImmutableArray<Estructura>.Empty));

	/// <summary>Carga reglas escritas en sintaxis tipo Prolog.</summary>
	public void Cargar(string programa)
	{
		foreach (var c in AnalizadorProlog.AnalizarPrograma(programa)) Agregar(c);
	}

	/// <summary>Consulta un objetivo y devuelve cada sustitución que lo satisface.</summary>
	public IEnumerable<ImmutableDictionary<string, Termino>> Consultar(Estructura objetivo)
	{
		var inicial = ImmutableDictionary<string, Termino>.Empty;
		foreach (var s in Resolver(ImmutableList.Create(objetivo), inicial, 0))
			yield return s;
	}

	public IEnumerable<ImmutableDictionary<string, Termino>> Consultar(string objetivo) =>
		Consultar(AnalizadorProlog.AnalizarObjetivo(objetivo));

	/// <summary>Aplica la sustitución hasta obtener el valor final de un término.</summary>
	public static Termino Resolver(Termino t, ImmutableDictionary<string, Termino> s)
	{
		while (t is Variable v && s.TryGetValue(v.Nombre, out var ligado)) t = ligado;
		if (t is Estructura e)
			return new Estructura(e.Functor, e.Argumentos.Select(a => Resolver(a, s)).ToImmutableArray());
		return t;
	}

	private IEnumerable<ImmutableDictionary<string, Termino>> Resolver(
		ImmutableList<Estructura> objetivos, ImmutableDictionary<string, Termino> s, int profundidad)
	{
		if (objetivos.IsEmpty) { yield return s; yield break; }
		if (profundidad > ProfundidadMaxima) yield break;

		var actual = objetivos[0];
		var resto = objetivos.RemoveAt(0);

		if (EsPredefinido(actual))
		{
			var r = EvaluarPredefinido(actual, s);
			if (r is not null)
				foreach (var sol in Resolver(resto, r, profundidad + 1)) yield return sol;
			yield break;
		}

		foreach (var clausula in _baseConocimiento)
		{
			if (clausula.Cabeza.Functor != actual.Functor || clausula.Cabeza.Aridad != actual.Aridad) continue;
			var renombrada = Renombrar(clausula);
			var s2 = Unificar(actual, renombrada.Cabeza, s);
			if (s2 is null) continue;
			var nuevos = resto.InsertRange(0, renombrada.Cuerpo);
			foreach (var sol in Resolver(nuevos, s2, profundidad + 1)) yield return sol;
		}
	}

	/// <summary>Unificación de dos términos bajo una sustitución. Devuelve null si no unifican.</summary>
	public static ImmutableDictionary<string, Termino>? Unificar(Termino a, Termino b, ImmutableDictionary<string, Termino> s)
	{
		a = Desreferenciar(a, s);
		b = Desreferenciar(b, s);
		if (a is Variable va) return a == b ? s : s.SetItem(va.Nombre, b);
		if (b is Variable vb) return s.SetItem(vb.Nombre, a);
		if (a is Atomo x && b is Atomo y) return x.Nombre == y.Nombre ? s : null;
		if (a is Numero n1 && b is Numero n2) return Math.Abs(n1.Valor - n2.Valor) < 1e-9 ? s : null;
		if (a is Estructura e1 && b is Estructura e2)
		{
			if (e1.Functor != e2.Functor || e1.Aridad != e2.Aridad) return null;
			var actual = s;
			for (int i = 0; i < e1.Aridad && actual is not null; i++)
				actual = Unificar(e1.Argumentos[i], e2.Argumentos[i], actual);
			return actual;
		}
		return null;
	}

	private static Termino Desreferenciar(Termino t, ImmutableDictionary<string, Termino> s)
	{
		while (t is Variable v && s.TryGetValue(v.Nombre, out var ligado)) t = ligado;
		return t;
	}

	/// <summary>Renombra las variables de la cláusula para que cada uso sea independiente.</summary>
	private Clausula Renombrar(Clausula c)
	{
		int id = Interlocked.Increment(ref _contadorRenombrado);
		Termino R(Termino t) => t switch
		{
			Variable v => new Variable($"{v.Nombre}_{id}"),
			Estructura e => new Estructura(e.Functor, e.Argumentos.Select(R).ToImmutableArray()),
			_ => t
		};
		return new Clausula((Estructura)R(c.Cabeza), c.Cuerpo.Select(e => (Estructura)R(e)).ToImmutableArray());
	}

	private static readonly HashSet<string> Predefinidos = new() { ">=", ">", "<", "<=", "=", "\\=" };

	private static bool EsPredefinido(Estructura e) => e.Aridad == 2 && Predefinidos.Contains(e.Functor);

	private static ImmutableDictionary<string, Termino>? EvaluarPredefinido(Estructura e, ImmutableDictionary<string, Termino> s)
	{
		var a = Resolver(e.Argumentos[0], s);
		var b = Resolver(e.Argumentos[1], s);
		switch (e.Functor)
		{
			case "=": return Unificar(a, b, s);
			case "\\=": return Unificar(a, b, s) is null ? s : null;
		}
		if (a is not Numero x || b is not Numero y) return null; // comparación con variables libres: falla
		bool ok = e.Functor switch
		{
			">=" => x.Valor >= y.Valor,
			">" => x.Valor > y.Valor,
			"<" => x.Valor < y.Valor,
			"<=" => x.Valor <= y.Valor,
			_ => false
		};
		return ok ? s : null;
	}
}
