using System.Text;

namespace SistemaTurnos.Core.Lexico;

/// <summary>
/// Analizador léxico (scanner) escrito a mano como un autómata finito: recorre el texto
/// carácter por carácter y agrupa los caracteres en tokens. Ejemplo de entrada:
///   nivel:2 motivo:"crisis asmática" desde:2026-01-01 fiebre
/// </summary>
public class AnalizadorLexico
{
	private readonly string _texto;
	private int _pos;

	public AnalizadorLexico(string texto) => _texto = texto ?? string.Empty;

	public List<Token> Analizar()
	{
		var tokens = new List<Token>();
		while (true)
		{
			var t = Siguiente();
			tokens.Add(t);
			if (t.Tipo == TipoToken.Fin) return tokens;
		}
	}

	private Token Siguiente()
	{
		while (_pos < _texto.Length && char.IsWhiteSpace(_texto[_pos])) _pos++;
		if (_pos >= _texto.Length) return new Token(TipoToken.Fin, "", _pos);

		int inicio = _pos;
		char c = _texto[_pos];

		if (c == ':') { _pos++; return new Token(TipoToken.DosPuntos, ":", inicio); }

		if (c == '"')
		{
			_pos++;
			var sb = new StringBuilder();
			while (_pos < _texto.Length && _texto[_pos] != '"') sb.Append(_texto[_pos++]);
			if (_pos >= _texto.Length)
				throw new ErrorLexico($"Falta cerrar las comillas que abren en la posición {inicio + 1}.", inicio);
			_pos++; // comilla de cierre
			return new Token(TipoToken.Texto, sb.ToString(), inicio);
		}

		if (char.IsDigit(c))
		{
			while (_pos < _texto.Length && (char.IsDigit(_texto[_pos]) || _texto[_pos] == '-')) _pos++;
			string lexema = _texto[inicio.._pos];
			if (lexema.All(char.IsDigit)) return new Token(TipoToken.Numero, lexema, inicio);
			if (System.Text.RegularExpressions.Regex.IsMatch(lexema, @"^\d{4}-\d{2}-\d{2}$"))
				return new Token(TipoToken.Fecha, lexema, inicio);
			throw new ErrorLexico($"'{lexema}' no es un número ni una fecha válida (use AAAA-MM-DD).", inicio);
		}

		if (char.IsLetter(c))
		{
			while (_pos < _texto.Length && (char.IsLetterOrDigit(_texto[_pos]) || _texto[_pos] == '_')) _pos++;
			string lexema = _texto[inicio.._pos];
			// Es identificador si lo sigue ':' (se mira el siguiente carácter sin consumirlo).
			int j = _pos;
			while (j < _texto.Length && char.IsWhiteSpace(_texto[j])) j++;
			var tipo = j < _texto.Length && _texto[j] == ':' ? TipoToken.Identificador : TipoToken.Palabra;
			return new Token(tipo, lexema, inicio);
		}

		throw new ErrorLexico($"Carácter no reconocido '{c}' en la posición {inicio + 1}.", inicio);
	}
}

public class ErrorLexico : Exception
{
	public int Posicion { get; }
	public ErrorLexico(string mensaje, int posicion) : base(mensaje) => Posicion = posicion;
}
