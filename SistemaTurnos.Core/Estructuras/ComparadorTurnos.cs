using SistemaTurnos.Core.Modelo;

namespace SistemaTurnos.Core.Estructuras;

/// <summary>
/// Criterio de orden de la fila: 1) nivel de prioridad (1 primero), 2) atención
/// preferencial, 3) hora de llegada, 4) secuencia de creación (desempate total).
/// Así se garantiza que pacientes con la misma gravedad conserven el orden de llegada.
/// </summary>
public sealed class ComparadorTurnos : IComparer<Turno>
{
	public static readonly ComparadorTurnos Instancia = new();

	public int Compare(Turno? a, Turno? b)
	{
		if (ReferenceEquals(a, b)) return 0;
		if (a is null) return 1;
		if (b is null) return -1;
		int c = ((int)a.Nivel).CompareTo((int)b.Nivel);
		if (c != 0) return c;
		c = b.Preferencial.CompareTo(a.Preferencial); // true antes que false
		if (c != 0) return c;
		c = a.HoraLlegada.CompareTo(b.HoraLlegada);
		if (c != 0) return c;
		return a.Secuencia.CompareTo(b.Secuencia);
	}
}
