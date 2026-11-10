namespace SistemaTurnos.Core.Lexico;

/// <summary>Categorías léxicas del lenguaje de filtros del historial.</summary>
public enum TipoToken
{
	Identificador,   // nivel, motivo, medico, desde, hasta
	DosPuntos,       // :
	Numero,          // 2
	Fecha,           // 2026-01-31
	Texto,           // "dolor de cabeza"
	Palabra,         // fiebre
	Fin
}

public readonly record struct Token(TipoToken Tipo, string Lexema, int Posicion)
{
	public override string ToString() => $"<{Tipo}, \"{Lexema}\", pos {Posicion}>";
}

