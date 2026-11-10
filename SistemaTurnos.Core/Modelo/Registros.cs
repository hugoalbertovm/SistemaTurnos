namespace SistemaTurnos.Core.Modelo;

/// <summary>
/// Registros inmutables (paradigma funcional: datos que no cambian después de crearse).
/// </summary>
public record AtencionHistorial(DateTime Fecha, string Motivo, NivelPrioridad Nivel, string Medico, int MinutosEspera)
{
	public string? NombrePaciente { get; init; }
}

/// <summary>Atención finalizada usada por los reportes.</summary>
public record RegistroAtencion(string Codigo, NivelPrioridad Nivel, bool Preferencial, DateTime HoraLlegada,
							   DateTime? HoraLlamado, DateTime? HoraFin, EstadoTurno Estado, int Llamados)
{
	public double? MinutosEspera => HoraLlamado is null ? null : (HoraLlamado.Value - HoraLlegada).TotalMinutes;
}

public class TurnoEventArgs : EventArgs
{
	public Turno Turno { get; }
	public TurnoEventArgs(Turno turno) => Turno = turno;
}

public record EvaluacionPreferencial(bool EsPreferencial, IReadOnlyList<string> Razones);
