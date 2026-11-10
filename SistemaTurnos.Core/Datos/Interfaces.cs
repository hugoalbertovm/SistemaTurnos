using SistemaTurnos.Core.Modelo;
using SistemaTurnos.Core.Reportes;

namespace SistemaTurnos.Core.Datos;

/// <summary>
/// Contratos de acceso a datos (patrón repositorio). La lógica del sistema depende de
/// estas interfaces y no de una base de datos concreta: así puede probarse con
/// repositorios en memoria y ejecutarse con SQLite.
/// </summary>
public interface IRepositorioPacientes
{
	IEnumerable<Paciente> ObtenerTodos();
	void Insertar(Paciente paciente);
	void Actualizar(Paciente paciente);
}

public interface IRepositorioTurnos
{
	void Insertar(Turno turno, DateTime fecha);
	void Actualizar(Turno turno);
	IEnumerable<Turno> ObtenerActivos(Func<int, Paciente?> buscarPaciente);
	int CerrarDiasAnteriores(DateTime hoy);
	int UltimoNumeroDelDia(DateTime dia);
	IEnumerable<AtencionHistorial> ObtenerHistorial(int pacienteId);
	IEnumerable<AtencionHistorial> ObtenerHistorialGeneral();
	IEnumerable<RegistroAtencion> ObtenerCerrados(DateTime desde, DateTime hasta);
	IEnumerable<FilaReporte> ResumenPorNivel(DateTime desde, DateTime hasta);
}

public interface IRepositorioCatalogos
{
	IEnumerable<Consultorio> ObtenerConsultorios();
}

public interface IRepositorioBitacora
{
	void Insertar(DateTime fechaHora, string tipo, string mensaje);
}
