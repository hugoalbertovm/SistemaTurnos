using SistemaTurnos.Core.Modelo;

namespace SistemaTurnos.Core.Reportes;

/// <summary>Fila genérica de un reporte (inmutable).</summary>
public record FilaReporte(string Categoria, double Valor, string Detalle);

/// <summary>
/// Interfaz común de los reportes (POO: polimorfismo). Cada implementación es una
/// función pura escrita con LINQ (paradigma funcional): recibe los datos y devuelve
/// un resultado nuevo sin modificar la colección original.
/// </summary>
public interface IReporte
{
	string Titulo { get; }
	string Unidad { get; }
	IReadOnlyList<FilaReporte> Generar(IEnumerable<RegistroAtencion> registros);
}

public sealed class ReporteEsperaPorNivel : IReporte
{
	public string Titulo => "Espera promedio por nivel de prioridad";
	public string Unidad => "minutos";

	public IReadOnlyList<FilaReporte> Generar(IEnumerable<RegistroAtencion> registros) =>
		registros
			.Where(r => r.MinutosEspera.HasValue)
			.GroupBy(r => r.Nivel)
			.OrderBy(g => g.Key)
			.Select(g => new FilaReporte(
				$"Nivel {(int)g.Key} ({g.Key})",
				Math.Round(g.Average(r => r.MinutosEspera!.Value), 1),
				$"{g.Count()} atenciones · referencia ≤ {g.Key.MinutosReferencia()} min · " +
				$"{g.Count(r => r.MinutosEspera <= g.Key.MinutosReferencia()) * 100 / g.Count()} % dentro del tiempo"))
			.ToList();
}

public sealed class ReporteAusentismo : IReporte
{
	public string Titulo => "Ausencias y cancelaciones";
	public string Unidad => "turnos";

	public IReadOnlyList<FilaReporte> Generar(IEnumerable<RegistroAtencion> registros)
	{
		var lista = registros.ToList();
		int total = Math.Max(1, lista.Count);
		return new[]
		{
			Fila("Atendidos", lista.Count(r => r.Estado == EstadoTurno.Finalizado)),
			Fila("Atendidos tras al menos una ausencia", lista.Count(r => r.Estado == EstadoTurno.Finalizado && r.Llamados > 1)),
			Fila("Cancelados (3 llamados sin respuesta)", lista.Count(r => r.Estado == EstadoTurno.Cancelado)),
		};

		FilaReporte Fila(string nombre, int cantidad) =>
			new(nombre, cantidad, $"{cantidad * 100.0 / total:0.#} % del total");
	}
}

public sealed class ReporteDemandaPorHora : IReporte
{
	public string Titulo => "Demanda por franja horaria";
	public string Unidad => "llegadas";

	public IReadOnlyList<FilaReporte> Generar(IEnumerable<RegistroAtencion> registros) =>
		registros
			.GroupBy(r => r.HoraLlegada.Hour)
			.OrderBy(g => g.Key)
			.Select(g => new FilaReporte($"{g.Key:00}:00–{g.Key:00}:59", g.Count(),
				$"{g.Count(r => r.Nivel <= NivelPrioridad.Rojo)} de nivel 1–2"))
			.ToList();
}

public static class CatalogoReportes
{
	public static IReadOnlyList<IReporte> Todos { get; } = new IReporte[]
	{
		new ReporteEsperaPorNivel(), new ReporteAusentismo(), new ReporteDemandaPorHora()
	};
}
