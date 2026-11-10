using System.Collections.Immutable;
using System.Globalization;

namespace SistemaTurnos.Core.Logica;

/// <summary>Término de la lógica de primer orden: átomo, número, variable o estructura.</summary>
public abstract record Termino;

public sealed record Atomo(string Nombre) : Termino
{
	public override string ToString() => Nombre;
}

public sealed record Numero(double Valor) : Termino
{
	public override string ToString() => Valor.ToString(CultureInfo.InvariantCulture);
}

public sealed record Variable(string Nombre) : Termino
{
	public override string ToString() => Nombre;
}

public sealed record Estructura(string Functor, ImmutableArray<Termino> Argumentos) : Termino
{
	public Estructura(string functor, params Termino[] argumentos) : this(functor, argumentos.ToImmutableArray()) { }
	public int Aridad => Argumentos.Length;
	public override string ToString() => $"{Functor}({string.Join(", ", Argumentos)})";
}

/// <summary>
/// Cláusula de Horn: Cabeza :- Cuerpo1, Cuerpo2, ...  (un hecho tiene el cuerpo vacío).
/// </summary>
public sealed record Clausula(Estructura Cabeza, ImmutableArray<Estructura> Cuerpo)
{
	public bool EsHecho => Cuerpo.IsEmpty;
	public override string ToString() =>
		EsHecho ? $"{Cabeza}." : $"{Cabeza} :- {string.Join(", ", Cuerpo)}.";
}
