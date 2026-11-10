using System.Globalization;
using SistemaTurnos.Core.Modelo;

namespace SistemaTurnos.Core.Lexico;

/// <summary>Resultado de analizar una consulta: un predicado o la lista de errores encontrados.</summary>
public record ResultadoConsulta(Func<AtencionHistorial, bool> Predicado, IReadOnlyList<string> Errores, IReadOnlyList<Token> Tokens)
{
	public bool EsValida => Errores.Count == 0;
}

/// <summary>
/// Analizador sintáctico y semántico del lenguaje de filtros.
/// Gramática:  consulta → termino*      termino → IDENT ':' valor | PALABRA | TEXTO
/// Usa una TABLA DE SÍMBOLOS con los campos válidos y su tipo; la etapa semántica
/// verifica que cada valor sea del tipo correcto. Los términos se combinan con Y lógico
/// mediante composición de funciones (paradigma funcional).
/// </summary>
public class AnalizadorConsultas
{
	private enum TipoCampo { Numero, Texto, Fecha }

	private sealed record Simbolo(TipoCampo Tipo, string Descripcion, Func<string, Func<AtencionHistorial, bool>> Constructor);

	private static readonly Dictionary<string, Simbolo> TablaSimbolos = new(StringComparer.OrdinalIgnoreCase)
	{
		["nivel"] = new(TipoCampo.Numero, "nivel de prioridad (1 a 5)",
			v => a => (int)a.Nivel == int.Parse(v, CultureInfo.InvariantCulture)),
		["motivo"] = new(TipoCampo.Texto, "texto contenido en el motivo",
			v => a => Contiene(a.Motivo, v)),
		["medico"] = new(TipoCampo.Texto, "nombre del médico",
			v => a => Contiene(a.Medico, v)),
		["desde"] = new(TipoCampo.Fecha, "fecha inicial (AAAA-MM-DD)",
			v => a => a.Fecha.Date >= DateTime.ParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture)),
		["hasta"] = new(TipoCampo.Fecha, "fecha final (AAAA-MM-DD)",
			v => a => a.Fecha.Date <= DateTime.ParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture)),
	};

	public static IEnumerable<string> CamposDisponibles =>
		TablaSimbolos.Select(kv => $"{kv.Key}: {kv.Value.Descripcion}");

	public ResultadoConsulta Analizar(string texto)
	{
		var errores = new List<string>();
		List<Token> tokens;
		try
		{
			tokens = new AnalizadorLexico(texto).Analizar();
		}
		catch (ErrorLexico ex)
		{
			return new ResultadoConsulta(_ => true, new[] { "Error léxico: " + ex.Message }, Array.Empty<Token>());
		}

		Func<AtencionHistorial, bool> predicado = _ => true;
		int i = 0;
		while (tokens[i].Tipo != TipoToken.Fin)
		{
			var t = tokens[i];
			if (t.Tipo is TipoToken.Palabra or TipoToken.Texto or TipoToken.Numero)
			{
				// Término libre: busca en el motivo.
				string valor = t.Lexema;
				predicado = Y(predicado, a => Contiene(a.Motivo, valor));
				i++;
				continue;
			}
			if (t.Tipo != TipoToken.Identificador)
			{
				errores.Add($"Error sintáctico: no se esperaba '{t.Lexema}' en la posición {t.Posicion + 1}.");
				i++;
				continue;
			}
			// termino → IDENT ':' valor
			if (tokens[i + 1].Tipo != TipoToken.DosPuntos)
			{
				errores.Add($"Error sintáctico: falta ':' después de '{t.Lexema}'.");
				i++;
				continue;
			}
			var valorTok = tokens[i + 2];
			if (valorTok.Tipo is TipoToken.Fin or TipoToken.DosPuntos or TipoToken.Identificador)
			{
				errores.Add($"Error sintáctico: falta el valor del campo '{t.Lexema}'.");
				i += 2;
				continue;
			}
			i += 3;

			// Análisis semántico: el campo debe existir en la tabla de símbolos y el tipo debe coincidir.
			if (!TablaSimbolos.TryGetValue(t.Lexema, out var simbolo))
			{
				errores.Add($"Error semántico: el campo '{t.Lexema}' no existe. Campos válidos: {string.Join(", ", TablaSimbolos.Keys)}.");
				continue;
			}
			string? errorTipo = VerificarTipo(simbolo.Tipo, valorTok);
			if (errorTipo is not null)
			{
				errores.Add($"Error semántico en '{t.Lexema}': {errorTipo}");
				continue;
			}
			predicado = Y(predicado, simbolo.Constructor(valorTok.Lexema));
		}
		return new ResultadoConsulta(predicado, errores, tokens);
	}

	private static string? VerificarTipo(TipoCampo tipo, Token valor) => tipo switch
	{
		TipoCampo.Numero when valor.Tipo != TipoToken.Numero => $"se esperaba un número y se recibió '{valor.Lexema}'.",
		TipoCampo.Numero when !(int.TryParse(valor.Lexema, out int n) && NivelPrioridadExtensiones.EsValido(n)) => "el nivel debe estar entre 1 y 5.",
		TipoCampo.Fecha when valor.Tipo != TipoToken.Fecha => $"se esperaba una fecha AAAA-MM-DD y se recibió '{valor.Lexema}'.",
		TipoCampo.Fecha when !DateTime.TryParseExact(valor.Lexema, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
			=> $"la fecha '{valor.Lexema}' no existe.",
		TipoCampo.Texto when valor.Tipo is not (TipoToken.Texto or TipoToken.Palabra) => $"se esperaba texto y se recibió '{valor.Lexema}'.",
		_ => null
	};

	/// <summary>Función de orden superior: combina dos predicados con Y lógico.</summary>
	private static Func<T, bool> Y<T>(Func<T, bool> f, Func<T, bool> g) => x => f(x) && g(x);

	private static bool Contiene(string texto, string buscado) =>
		CultureInfo.CurrentCulture.CompareInfo.IndexOf(texto ?? "", buscado,
			CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
}
