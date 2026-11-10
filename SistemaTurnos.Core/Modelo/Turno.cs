namespace SistemaTurnos.Core.Modelo;

public enum EstadoTurno
{
	EnEspera,
	EnAtencion,
	Ausente,
	Finalizado,
	Cancelado
}

/// <summary>
/// Turno de atención. El estado solo cambia mediante métodos que validan las
/// transiciones permitidas (encapsulamiento): EnEspera → EnAtencion → Finalizado,
/// EnAtencion → Ausente → EnEspera (máximo de llamados).
/// </summary>
public class Turno
{
	public const int MaximoLlamados = 3;

	public int Id { get; internal set; }
	public string Codigo { get; }
	public Paciente Paciente { get; }
	public NivelPrioridad Nivel { get; }
	public string Motivo { get; }
	public bool Preferencial { get; }
	public DateTime HoraLlegada { get; }

	/// <summary>Número de orden de creación; desempata en el comparador.</summary>
	public long Secuencia { get; internal set; }

	public EstadoTurno Estado { get; private set; } = EstadoTurno.EnEspera;
	public int Llamados { get; private set; }
	public int? ConsultorioId { get; private set; }
	public DateTime? HoraLlamado { get; private set; }
	public DateTime? HoraFin { get; private set; }
	public DateTime? ReingresoEn { get; private set; }

	public Turno(string codigo, Paciente paciente, NivelPrioridad nivel, string motivo,
				 bool preferencial, DateTime horaLlegada)
	{
		if (!NivelPrioridadExtensiones.EsValido((int)nivel))
			throw new ArgumentOutOfRangeException(nameof(nivel), "El nivel debe estar entre 1 y 5.");
		Codigo = codigo;
		Paciente = paciente ?? throw new ArgumentNullException(nameof(paciente));
		Nivel = nivel;
		Motivo = string.IsNullOrWhiteSpace(motivo) ? "Sin especificar" : motivo.Trim();
		Preferencial = preferencial;
		HoraLlegada = horaLlegada;
	}

	/// <summary>Reconstruye un turno guardado en la base de datos.</summary>
	public static Turno Restaurar(int id, string codigo, Paciente paciente, NivelPrioridad nivel, string motivo,
		bool preferencial, DateTime horaLlegada, EstadoTurno estado, int llamados, int? consultorioId,
		DateTime? horaLlamado, DateTime? horaFin, DateTime? reingresoEn)
	{
		return new Turno(codigo, paciente, nivel, motivo, preferencial, horaLlegada)
		{
			Id = id,
			Estado = estado,
			Llamados = llamados,
			ConsultorioId = consultorioId,
			HoraLlamado = horaLlamado,
			HoraFin = horaFin,
			ReingresoEn = reingresoEn
		};
	}

	public int MinutosEspera(DateTime ahora)
	{
		DateTime fin = HoraLlamado ?? ahora;
		return (int)Math.Max(0, (fin - HoraLlegada).TotalMinutes);
	}

	internal void Llamar(int consultorioId, DateTime ahora)
	{
		if (Estado != EstadoTurno.EnEspera)
			throw new InvalidOperationException($"El turno {Codigo} no está en espera.");
		Estado = EstadoTurno.EnAtencion;
		ConsultorioId = consultorioId;
		HoraLlamado = ahora;
		Llamados++;
		ReingresoEn = null;
	}

	/// <summary>Marca la ausencia. Devuelve true si el turno podrá reingresar a la fila.</summary>
	internal bool MarcarAusente(DateTime ahora, TimeSpan gracia)
	{
		if (Estado != EstadoTurno.EnAtencion)
			throw new InvalidOperationException($"El turno {Codigo} no está siendo atendido.");
		if (Llamados >= MaximoLlamados)
		{
			Estado = EstadoTurno.Cancelado;
			HoraFin = ahora;
			ReingresoEn = null;
			return false;
		}
		Estado = EstadoTurno.Ausente;
		HoraLlamado = null;
		ConsultorioId = null;
		ReingresoEn = ahora + gracia;
		return true;
	}

	internal void Reingresar()
	{
		if (Estado != EstadoTurno.Ausente)
			throw new InvalidOperationException($"El turno {Codigo} no está ausente.");
		Estado = EstadoTurno.EnEspera;
		ReingresoEn = null;
	}

	internal void Finalizar(DateTime ahora)
	{
		if (Estado != EstadoTurno.EnAtencion)
			throw new InvalidOperationException($"El turno {Codigo} no está siendo atendido.");
		Estado = EstadoTurno.Finalizado;
		HoraFin = ahora;
	}

	public override string ToString() => $"{Codigo} (nivel {(int)Nivel}, {Paciente.NombreCorto})";
}
