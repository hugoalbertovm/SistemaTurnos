using System.Data;
using System.Data.Common;
using System.Globalization;
using SistemaTurnos.Core.Modelo;
using SistemaTurnos.Core.Reportes;

namespace SistemaTurnos.Core.Datos;

/// <summary>Utilidades comunes de acceso a datos con ADO.NET (independientes del proveedor).</summary>
internal static class Ado
{
	/// <summary>
	/// Abre una conexión. Quien la recibe debe liberarla con 'using' (IDisposable): así la
	/// conexión tiene un tiempo de vida acotado y vuelve al grupo de conexiones (pooling).
	/// </summary>
	public static DbConnection Abrir(Func<DbConnection> fabrica)
	{
		var con = fabrica();
		con.Open();
		return con;
	}

	public static DbCommand Comando(DbConnection con, string sql, params (string Nombre, object? Valor)[] parametros) =>
		Crear(con, sql, CommandType.Text, parametros);

	public static DbCommand Procedimiento(DbConnection con, string nombre, params (string Nombre, object? Valor)[] parametros) =>
		Crear(con, nombre, CommandType.StoredProcedure, parametros);

	private static DbCommand Crear(DbConnection con, string texto, CommandType tipo, (string Nombre, object? Valor)[] parametros)
	{
		var cmd = con.CreateCommand();
		cmd.CommandText = texto;
		cmd.CommandType = tipo;
		foreach (var (nombre, valor) in parametros)
		{
			var p = cmd.CreateParameter();
			p.ParameterName = nombre;
			p.Value = valor ?? DBNull.Value;
			if (valor is DateTime) p.DbType = DbType.DateTime2;  // evita el rango limitado del tipo DATETIME
			cmd.Parameters.Add(p);
		}
		return cmd;
	}

	public static DateTime LeerFecha(DbDataReader r, int i) => r.GetDateTime(i);
	public static DateTime? LeerFechaNula(DbDataReader r, int i) => r.IsDBNull(i) ? null : r.GetDateTime(i);
	public static bool LeerBool(DbDataReader r, int i) => Convert.ToBoolean(r.GetValue(i), CultureInfo.InvariantCulture);
	public static int LeerEntero(DbDataReader r, int i) => Convert.ToInt32(r.GetValue(i), CultureInfo.InvariantCulture);

	public static string EstadoATexto(EstadoTurno e) => e switch
	{
		EstadoTurno.EnEspera => "EN_ESPERA",
		EstadoTurno.EnAtencion => "EN_ATENCION",
		EstadoTurno.Ausente => "AUSENTE",
		EstadoTurno.Finalizado => "FINALIZADO",
		_ => "CANCELADO"
	};

	public static EstadoTurno TextoAEstado(string s) => s switch
	{
		"EN_ESPERA" => EstadoTurno.EnEspera,
		"EN_ATENCION" => EstadoTurno.EnAtencion,
		"AUSENTE" => EstadoTurno.Ausente,
		"FINALIZADO" => EstadoTurno.Finalizado,
		_ => EstadoTurno.Cancelado
	};
}

public class RepositorioPacientesSql : IRepositorioPacientes
{
	private readonly Func<DbConnection> _fabrica;
	public RepositorioPacientesSql(Func<DbConnection> fabrica) => _fabrica = fabrica;

	public IEnumerable<Paciente> ObtenerTodos()
	{
		using var con = Ado.Abrir(_fabrica);
		using var cmd = Ado.Comando(con, Sql.PacientesTodos);
		using var r = cmd.ExecuteReader();
		var lista = new List<Paciente>();
		while (r.Read())
		{
			lista.Add(new Paciente(Cedula.Parse(r.GetString(1)), r.GetString(2), Ado.LeerFecha(r, 3),
				r.IsDBNull(4) ? "" : r.GetString(4), Ado.LeerBool(r, 5), Ado.LeerBool(r, 6), Ado.LeerEntero(r, 0)));
		}
		return lista;
	}

	public void Insertar(Paciente p)
	{
		using var con = Ado.Abrir(_fabrica);
		using var cmd = Ado.Comando(con, Sql.PacienteInsertar, Parametros(p));
		p.Id = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
	}

	public void Actualizar(Paciente p)
	{
		using var con = Ado.Abrir(_fabrica);
		using var cmd = Ado.Comando(con, Sql.PacienteActualizar, Parametros(p).Append(("@Id", p.Id)).ToArray());
		cmd.ExecuteNonQuery();
	}

	private static (string, object?)[] Parametros(Paciente p) => new (string, object?)[]
	{
		("@Cedula", p.Cedula.Valor), ("@Nombre", p.Nombre), ("@FechaNacimiento", p.FechaNacimiento.Date),
		("@Telefono", string.IsNullOrWhiteSpace(p.Telefono) ? null : p.Telefono),
		("@EsEmbarazada", p.EsEmbarazada), ("@TieneDiscapacidad", p.TieneDiscapacidad)
	};
}

public partial class RepositorioTurnosSql : IRepositorioTurnos
{
	private readonly Func<DbConnection> _fabrica;
	public RepositorioTurnosSql(Func<DbConnection> fabrica) => _fabrica = fabrica;

	public void Insertar(Turno t, DateTime fecha)
	{
		using var con = Ado.Abrir(_fabrica);
		using var cmd = Ado.Comando(con, Sql.TurnoInsertar,
			("@Fecha", fecha.Date), ("@Codigo", t.Codigo), ("@PacienteId", t.Paciente.Id), ("@NivelId", (byte)t.Nivel),
			("@Motivo", t.Motivo), ("@Preferencial", t.Preferencial), ("@HoraLlegada", t.HoraLlegada),
			("@Estado", Ado.EstadoATexto(t.Estado)), ("@Llamados", (byte)t.Llamados));
		t.Id = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
	}

	public void Actualizar(Turno t)
	{
		using var con = Ado.Abrir(_fabrica);
		using var cmd = Ado.Comando(con, Sql.TurnoActualizar,
			("@Estado", Ado.EstadoATexto(t.Estado)), ("@Llamados", (byte)t.Llamados), ("@ConsultorioId", t.ConsultorioId),
			("@HoraLlamado", t.HoraLlamado), ("@HoraFin", t.HoraFin), ("@ReingresoEn", t.ReingresoEn), ("@Id", t.Id));
		cmd.ExecuteNonQuery();
	}

	public IEnumerable<Turno> ObtenerActivos(Func<int, Paciente?> buscarPaciente)
	{
		using var con = Ado.Abrir(_fabrica);
		using var cmd = Ado.Comando(con, Sql.TurnosActivos);
		using var r = cmd.ExecuteReader();
		var lista = new List<Turno>();
		while (r.Read())
		{
			var paciente = buscarPaciente(Ado.LeerEntero(r, 2));
			if (paciente is null) continue;
			lista.Add(Turno.Restaurar(Ado.LeerEntero(r, 0), r.GetString(1), paciente, (NivelPrioridad)Ado.LeerEntero(r, 3),
				r.GetString(4), Ado.LeerBool(r, 5), Ado.LeerFecha(r, 6), Ado.TextoAEstado(r.GetString(7)), Ado.LeerEntero(r, 8),
				r.IsDBNull(9) ? null : Ado.LeerEntero(r, 9), Ado.LeerFechaNula(r, 10), Ado.LeerFechaNula(r, 11), Ado.LeerFechaNula(r, 12)));
		}
		return lista;
	}

	public int CerrarDiasAnteriores(DateTime hoy)
	{
		using var con = Ado.Abrir(_fabrica);
		using var cmd = Ado.Procedimiento(con, Sql.ProcCerrarDiasAnteriores, ("@Hoy", hoy.Date), ("@Ahora", hoy));
		return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
	}

	public int UltimoNumeroDelDia(DateTime dia)
	{
		using var con = Ado.Abrir(_fabrica);
		using var cmd = Ado.Comando(con, Sql.UltimoNumeroDelDia, ("@Fecha", dia.Date));
		return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
	}

	public IEnumerable<AtencionHistorial> ObtenerHistorial(int pacienteId)
	{
		using var con = Ado.Abrir(_fabrica);
		using var cmd = Ado.Procedimiento(con, Sql.ProcHistorialPaciente, ("@PacienteId", pacienteId));
		using var r = cmd.ExecuteReader();
		var lista = new List<AtencionHistorial>();
		while (r.Read())
			lista.Add(new AtencionHistorial(Ado.LeerFecha(r, 0), r.GetString(1), (NivelPrioridad)Ado.LeerEntero(r, 2),
				r.GetString(3), Ado.LeerEntero(r, 4)));
		return lista;
	}

	public IEnumerable<AtencionHistorial> ObtenerHistorialGeneral()
	{
		using var con = Ado.Abrir(_fabrica);
		using var cmd = Ado.Comando(con, Sql.HistorialGeneral);
		using var r = cmd.ExecuteReader();
		var lista = new List<AtencionHistorial>();
		while (r.Read())
			lista.Add(new AtencionHistorial(Ado.LeerFecha(r, 0), r.GetString(1), (NivelPrioridad)Ado.LeerEntero(r, 2),
				r.GetString(3), Ado.LeerEntero(r, 4))
			{ NombrePaciente = r.GetString(5) });
		return lista;
	}

	public IEnumerable<RegistroAtencion> ObtenerCerrados(DateTime desde, DateTime hasta)
	{
		using var con = Ado.Abrir(_fabrica);
		using var cmd = Ado.Comando(con, Sql.Cerrados, ("@Desde", desde.Date), ("@Hasta", hasta.Date));
		using var r = cmd.ExecuteReader();
		var lista = new List<RegistroAtencion>();
		while (r.Read())
			lista.Add(new RegistroAtencion(r.GetString(0), (NivelPrioridad)Ado.LeerEntero(r, 1), Ado.LeerBool(r, 2),
				Ado.LeerFecha(r, 3), Ado.LeerFechaNula(r, 4), Ado.LeerFechaNula(r, 5), Ado.TextoAEstado(r.GetString(6)), Ado.LeerEntero(r, 7)));
		return lista;
	}
}

public partial class RepositorioTurnosSql
{
	/// <summary>Resumen calculado en el servidor mediante el procedimiento almacenado dbo.usp_ResumenPorNivel.</summary>
	public IEnumerable<FilaReporte> ResumenPorNivel(DateTime desde, DateTime hasta)
	{
		using var con = Ado.Abrir(_fabrica);
		using var cmd = Ado.Procedimiento(con, Sql.ProcResumenPorNivel, ("@Desde", desde.Date), ("@Hasta", hasta.Date));
		using var r = cmd.ExecuteReader();
		var lista = new List<FilaReporte>();
		while (r.Read())
		{
			int atenciones = Ado.LeerEntero(r, 2);
			double espera = r.IsDBNull(3) ? 0 : Convert.ToDouble(r.GetValue(3), CultureInfo.InvariantCulture);
			lista.Add(new FilaReporte($"Nivel {Ado.LeerEntero(r, 0)} ({r.GetString(1)})", Math.Round(espera, 1),
				$"{atenciones} atenciones · referencia ≤ {Ado.LeerEntero(r, 4)} min"));
		}
		return lista;
	}
}

public class RepositorioCatalogosSql : IRepositorioCatalogos
{
	private readonly Func<DbConnection> _fabrica;
	public RepositorioCatalogosSql(Func<DbConnection> fabrica) => _fabrica = fabrica;

	public IEnumerable<Consultorio> ObtenerConsultorios()
	{
		using var con = Ado.Abrir(_fabrica);
		using var cmd = Ado.Comando(con, Sql.Consultorios);
		using var r = cmd.ExecuteReader();
		var lista = new List<Consultorio>();
		while (r.Read())
		{
			Medico? medico = r.IsDBNull(2) ? null
				: new Medico(Cedula.Parse(r.GetString(3)), r.GetString(4), r.GetString(5), Ado.LeerEntero(r, 2));
			lista.Add(new Consultorio(Ado.LeerEntero(r, 0), Ado.LeerEntero(r, 1), medico));
		}
		return lista;
	}
}

public class RepositorioBitacoraSql : IRepositorioBitacora
{
	private readonly Func<DbConnection> _fabrica;
	public RepositorioBitacoraSql(Func<DbConnection> fabrica) => _fabrica = fabrica;

	public void Insertar(DateTime fechaHora, string tipo, string mensaje)
	{
		using var con = Ado.Abrir(_fabrica);
		using var cmd = Ado.Comando(con, Sql.BitacoraInsertar,
			("@FechaHora", fechaHora), ("@Tipo", tipo), ("@Mensaje", mensaje.Length > 1000 ? mensaje[..1000] : mensaje));
		cmd.ExecuteNonQuery();
	}
}
