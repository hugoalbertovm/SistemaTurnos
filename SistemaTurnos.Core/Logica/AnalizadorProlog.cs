using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SistemaTurnos.Core.Logica;

/// <summary>
/// Analizador léxico y sintáctico mínimo para cláusulas tipo Prolog:
///   cabeza(X, a) :- meta1(X, Y), Y >= 65.
/// Átomos en minúscula, variables en mayúscula, números y comparaciones infijas.
/// Los comentarios empiezan con %.
/// </summary>
public static class AnalizadorProlog
{
	private enum Tipo { Ident, Var, Num, AbreP, CierraP, Coma, Punto, Implica, Op, Fin }
	private record struct Tok(Tipo Tipo, string Texto);

	public static List<Clausula> AnalizarPrograma(string texto)
	{
		var toks = Tokenizar(texto);
		int i = 0;
		var resultado = new List<Clausula>();
		while (toks[i].Tipo != Tipo.Fin)
		{
			var cabeza = Meta(toks, ref i);
			var cuerpo = ImmutableArray.CreateBuilder<Estructura>();
			if (toks[i].Tipo == Tipo.Implica)
			{
				i++;
				cuerpo.Add(Meta(toks, ref i));
				while (toks[i].Tipo == Tipo.Coma) { i++; cuerpo.Add(Meta(toks, ref i)); }
			}
			Esperar(toks, ref i, Tipo.Punto);
			resultado.Add(new Clausula(cabeza, cuerpo.ToImmutable()));
		}
		return resultado;
	}

	public static Estructura AnalizarObjetivo(string texto)
	{
		var toks = Tokenizar(texto.TrimEnd().TrimEnd('.'));
		int i = 0;
		var meta = Meta(toks, ref i);
		if (toks[i].Tipo != Tipo.Fin) throw new FormatException("Texto sobrante en el objetivo.");
		return meta;
	}

	private static Estructura Meta(List<Tok> t, ref int i)
	{
		var izq = Termino(t, ref i);
		if (t[i].Tipo == Tipo.Op)
		{
			string op = t[i].Texto; i++;
			var der = Termino(t, ref i);
			return new Estructura(op, izq, der);
		}
		return izq switch
		{
			Estructura e => e,
			Atomo a => new Estructura(a.Nombre, ImmutableArray<Termino>.Empty),
			_ => throw new FormatException($"Se esperaba un predicado y se encontró '{izq}'.")
		};
	}

	private static Termino Termino(List<Tok> t, ref int i)
	{
		var tok = t[i];
		switch (tok.Tipo)
		{
			case Tipo.Var: i++; return new Variable(tok.Texto);
			case Tipo.Num: i++; return new Numero(double.Parse(tok.Texto, CultureInfo.InvariantCulture));
			case Tipo.Ident:
				i++;
				if (t[i].Tipo != Tipo.AbreP) return new Atomo(tok.Texto);
				i++;
				var args = new List<Termino> { Termino(t, ref i) };
				while (t[i].Tipo == Tipo.Coma) { i++; args.Add(Termino(t, ref i)); }
				Esperar(t, ref i, Tipo.CierraP);
				return new Estructura(tok.Texto, args.ToArray());
			default:
				throw new FormatException($"Símbolo inesperado: '{tok.Texto}'.");
		}
	}

	private static void Esperar(List<Tok> t, ref int i, Tipo tipo)
	{
		if (t[i].Tipo != tipo) throw new FormatException($"Se esperaba {tipo} y se encontró '{t[i].Texto}'.");
		i++;
	}

	private static List<Tok> Tokenizar(string s)
	{
		var r = new List<Tok>();
		int i = 0;
		while (i < s.Length)
		{
			char c = s[i];
			if (char.IsWhiteSpace(c)) { i++; continue; }
			if (c == '%') { while (i < s.Length && s[i] != '\n') i++; continue; }
			if (char.IsLetter(c) || c == '_')
			{
				var sb = new StringBuilder();
				while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) sb.Append(s[i++]);
				string w = sb.ToString();
				r.Add(new Tok(char.IsUpper(w[0]) || w[0] == '_' ? Tipo.Var : Tipo.Ident, w));
				continue;
			}
			if (char.IsDigit(c))
			{
				var sb = new StringBuilder();
				while (i < s.Length && (char.IsDigit(s[i]) || (s[i] == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))) sb.Append(s[i++]);
				r.Add(new Tok(Tipo.Num, sb.ToString()));
				continue;
			}
			if (s.AsSpan(i).StartsWith(":-")) { r.Add(new Tok(Tipo.Implica, ":-")); i += 2; continue; }
			if (s.AsSpan(i).StartsWith(">=") || s.AsSpan(i).StartsWith("<=") || s.AsSpan(i).StartsWith("\\="))
			{ r.Add(new Tok(Tipo.Op, s.Substring(i, 2))); i += 2; continue; }
			switch (c)
			{
				case '(': r.Add(new Tok(Tipo.AbreP, "(")); break;
				case ')': r.Add(new Tok(Tipo.CierraP, ")")); break;
				case ',': r.Add(new Tok(Tipo.Coma, ",")); break;
				case '.': r.Add(new Tok(Tipo.Punto, ".")); break;
				case '>': case '<': case '=': r.Add(new Tok(Tipo.Op, c.ToString())); break;
				default: throw new FormatException($"Carácter no válido en la regla: '{c}'.");
			}
			i++;
		}
		r.Add(new Tok(Tipo.Fin, "<fin>"));
		return r;
	}
}
