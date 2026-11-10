using System.Text.RegularExpressions;

namespace SistemaTurnos.Core.Modelo;

/// <summary>
/// Cédula de identidad costarricense (formato 0-0000-0000).
/// Es un TIPO DE VALOR (record struct): se copia al asignarse y vive en la pila
/// cuando es una variable local, a diferencia de las clases (tipos de referencia).
/// </summary>
public readonly partial record struct Cedula
{
	public string Valor { get; }

	private Cedula(string valor) => Valor = valor;

	[GeneratedRegex(@"^[1-9]-\d{4}-\d{4}$")]
	private static partial Regex Formato();

	/// <summary>Intenta crear una cédula válida a partir de texto (acepta 9 dígitos sin guiones).</summary>
	public static bool TryParse(string? texto, out Cedula cedula)
	{
		cedula = default;
		if (string.IsNullOrWhiteSpace(texto)) return false;
		string t = texto.Trim();
		if (t.Length == 9 && t.All(char.IsDigit))
			t = $"{t[0]}-{t.Substring(1, 4)}-{t.Substring(5, 4)}";
		if (!Formato().IsMatch(t)) return false;
		cedula = new Cedula(t);
		return true;
	}

	public static Cedula Parse(string texto) =>
		TryParse(texto, out var c) ? c : throw new FormatException($"Cédula inválida: '{texto}'. Formato esperado: 0-0000-0000.");

	public override string ToString() => Valor ?? string.Empty;
}
