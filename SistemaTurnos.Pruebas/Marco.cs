using System.Diagnostics;
using System.Reflection;

namespace SistemaTurnos.Pruebas;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PruebaAttribute : Attribute
{
	public string Descripcion { get; }
	public string Requisito { get; }
	public PruebaAttribute(string descripcion, string requisito = "") { Descripcion = descripcion; Requisito = requisito; }
}

public class FalloPrueba : Exception { public FalloPrueba(string m) : base(m) { } }

/// <summary>Indica que la prueba no se pudo ejecutar (por ejemplo, no hay servidor de base de datos).</summary>
public class PruebaOmitida : Exception { public PruebaOmitida(string m) : base(m) { } }

public static class Afirmar
{
	public static void Verdadero(bool condicion, string mensaje)
	{
		if (!condicion) throw new FalloPrueba(mensaje);
	}

	public static void Igual<T>(T esperado, T obtenido, string contexto = "")
	{
		if (!EqualityComparer<T>.Default.Equals(esperado, obtenido))
			throw new FalloPrueba($"{contexto} Se esperaba «{esperado}» y se obtuvo «{obtenido}».".Trim());
	}

	public static TExc Lanza<TExc>(Action accion, string contexto) where TExc : Exception
	{
		try { accion(); }
		catch (TExc ex) { return ex; }
		catch (Exception ex) { throw new FalloPrueba($"{contexto}: se esperaba {typeof(TExc).Name} y ocurrió {ex.GetType().Name}."); }
		throw new FalloPrueba($"{contexto}: se esperaba la excepción {typeof(TExc).Name}.");
	}
}

/// <summary>Ejecuta por reflexión todos los métodos marcados con [Prueba].</summary>
public static class Ejecutor
{
	public static int Ejecutar(TextWriter salida, params Type[] clases)
	{
		int ok = 0, fallos = 0, omitidas = 0, n = 0;
		var total = Stopwatch.StartNew();
		foreach (var clase in clases)
		{
			salida.WriteLine();
			salida.WriteLine($"== {clase.Name} ==");
			foreach (var m in clase.GetMethods(BindingFlags.Public | BindingFlags.Static))
			{
				var attr = m.GetCustomAttribute<PruebaAttribute>();
				if (attr is null) continue;
				n++;
				var sw = Stopwatch.StartNew();
				string? detalle = null;
				string estado;
				try
				{
					detalle = m.Invoke(null, null) as string;
					estado = "PASA   ";
					ok++;
				}
				catch (TargetInvocationException tie) when (tie.InnerException is PruebaOmitida po)
				{
					estado = "OMITIDA";
					detalle = po.Message;
					omitidas++;
				}
				catch (TargetInvocationException tie)
				{
					estado = "FALLA  ";
					fallos++;
					detalle = tie.InnerException is FalloPrueba f ? f.Message
						: $"{tie.InnerException?.GetType().Name}: {tie.InnerException?.Message}";
				}
				sw.Stop();
				string req = string.IsNullOrEmpty(attr.Requisito) ? "" : $" [{attr.Requisito}]";
				salida.WriteLine($"  {estado}  P{n:00}{req} {attr.Descripcion} ({sw.Elapsed.TotalMilliseconds:0.0} ms)");
				if (!string.IsNullOrWhiteSpace(detalle)) salida.WriteLine($"         → {detalle}");
			}
		}
		total.Stop();
		salida.WriteLine();
		salida.WriteLine($"Resultado: {ok} de {n} pruebas aprobadas, {fallos} fallidas, {omitidas} omitidas. Tiempo total: {total.ElapsedMilliseconds} ms.");
		return fallos;
	}
}
