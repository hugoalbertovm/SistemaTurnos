using SistemaTurnos.Core.Modelo;
using SistemaTurnos.Core.Reportes;

namespace SistemaTurnos.Core.Datos;

/// <summary>Repositorios en memoria: se usan en las pruebas automatizadas.</summary>
public class RepositorioPacientesMemoria : IRepositorioPacientes
{
	private readonly List<Paciente> _datos = new();
	public IEnumerable<Paciente> ObtenerTodos() => _datos.ToList();
	public void Insertar(Paciente p)
	{
		if (_datos.Any(x => x.Cedula == p.Cedula)) throw new InvalidOperationException("Cédula duplicada.");
		p.Id = _datos.Count + 1;
		_datos.Add(p);
	}
	public void Actualizar(Paciente p) { }
}

public class RepositorioTurnosMemoria : IRepositorioTurnos
{
	private readonly List<(Turno Turno, DateTime Fecha)> _datos = new();
	public IReadOnlyList<Turno> Todos => _datos.Select(x => x.Turno).ToList();

	public void Insertar(Turno t, DateTime fecha) { t.Id = _datos.Count + 1; _datos.Add((t, fecha.Date)); }
	public void Actualizar(Turno t) { }

	public IEnumerable<Turno> ObtenerActivos(Func<int, Paciente?> buscarPaciente) =>
		_datos.Select(x => x.Turno).Where(t => t.Estado is EstadoTurno.EnEspera or EstadoTurno.EnAtencion or EstadoTurno.Ausente).ToList();

	public int CerrarDiasAnteriores(DateTime hoy) => 0;

	public int UltimoNumeroDelDia(DateTime dia) =>
		_datos.Where(x => x.Fecha == dia.Date).Select(x => int.Parse(x.Turno.Codigo[2..])).DefaultIfEmpty(0).Max();

	public IEnumerable<AtencionHistorial> ObtenerHistorial(int pacienteId) =>
		_datos.Select(x => x.Turno)
			  .Where(t => t.Paciente.Id == pacienteId && t.Estado == EstadoTurno.Finalizado)
			  .OrderByDescending(t => t.HoraLlegada)
			  .Select(t => new AtencionHistorial(t.HoraLlegada, t.Motivo, t.Nivel, $"Consultorio {t.ConsultorioId}", t.MinutosEspera(t.HoraFin ?? t.HoraLlegada)))
			  .ToList();

	public IEnumerable<AtencionHistorial> ObtenerHistorialGeneral() =>
		_datos.Select(x => x.Turno)
			  .Where(t => t.Estado == EstadoTurno.Finalizado)
			  .Select(t => new AtencionHistorial(t.HoraLlegada, t.Motivo, t.Nivel,
				  $"Consultorio {t.ConsultorioId}", t.MinutosEspera(t.HoraFin ?? t.HoraLlegada))
			  { NombrePaciente = t.Paciente.Nombre })
			  .ToList();

	public IEnumerable<RegistroAtencion> ObtenerCerrados(DateTime desde, DateTime hasta) =>
		_datos.Where(x => x.Fecha >= desde.Date && x.Fecha <= hasta.Date &&
						  x.Turno.Estado is EstadoTurno.Finalizado or EstadoTurno.Cancelado)
			  .Select(x => new RegistroAtencion(x.Turno.Codigo, x.Turno.Nivel, x.Turno.Preferencial, x.Turno.HoraLlegada,
				  x.Turno.HoraLlamado, x.Turno.HoraFin, x.Turno.Estado, x.Turno.Llamados))
			  .ToList();

	public IEnumerable<FilaReporte> ResumenPorNivel(DateTime desde, DateTime hasta) =>
		new ReporteEsperaPorNivel().Generar(ObtenerCerrados(desde, hasta));
}

public class RepositorioCatalogosMemoria : IRepositorioCatalogos
{
	public IEnumerable<Consultorio> ObtenerConsultorios() =>
		Enumerable.Range(1, 4).Select(i => new Consultorio(i, i, null)).ToList();
}
