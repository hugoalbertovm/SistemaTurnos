#if !SIN_BASE_DATOS
using System.Data.Common;
using Microsoft.Data.SqlClient;
using SistemaTurnos.Core.Datos;
using SistemaTurnos.Core.Modelo;
using SistemaTurnos.Core.Servicios;

namespace SistemaTurnos.Pruebas;

/// <summary>
/// Pruebas de integración con SQL Server. Cada prueba crea una base de datos temporal,
/// la usa y la elimina al terminar. Si no hay servidor disponible, las pruebas se marcan como omitidas.
/// </summary>
public static class PruebasBaseDatos
{
	private static readonly string CadenaBase =
		Configuracion.Cargar(Path.Combine(AppContext.BaseDirectory, "configuracion.json")).CadenaConexion;

	private static void ConBaseTemporal(Action<Func<DbConnection>> prueba)
	{
		var builder = new SqlConnectionStringBuilder(CadenaBase) { InitialCatalog = "master", ConnectTimeout = 5 };
		string master = builder.ConnectionString;
		Func<DbConnection> fabricaMaster = () => new SqlConnection(master);
		string nombre = $"SistemaTurnos_Prueba_{Guid.NewGuid():N}"[..36];
		try
		{
			BaseDatos.CrearSiNoExiste(fabricaMaster, nombre);
		}
		catch (SqlException ex)
		{
			throw new PruebaOmitida($"No se pudo conectar a SQL Server ({ex.Message.Split('\n')[0]}). Revise configuracion.json.");
		}
		builder.InitialCatalog = nombre;
		string cadena = builder.ConnectionString;
		try
		{
			prueba(() => new SqlConnection(cadena));
		}
		finally
		{
			SqlConnection.ClearAllPools();
			BaseDatos.Eliminar(fabricaMaster, nombre);
		}
	}

	private static int Escalar(Func<DbConnection> fabrica, string sql)
	{
		using var con = fabrica();
		con.Open();
		using var cmd = con.CreateCommand();
		cmd.CommandText = sql;
		return Convert.ToInt32(cmd.ExecuteScalar());
	}

	[Prueba("Base de datos: crea el esquema, los catálogos y los datos de demostración", "RF11")]
	public static string CrearEsquema()
	{
		string resultado = "";
		ConBaseTemporal(fabrica =>
		{
			BaseDatos.Inicializar(fabrica);
			BaseDatos.Inicializar(fabrica); // segunda ejecución: no debe duplicar nada
			Afirmar.Igual(5, Escalar(fabrica, "SELECT COUNT(*) FROM dbo.NivelPrioridad;"), "Niveles:");
			Afirmar.Igual(4, Escalar(fabrica, "SELECT COUNT(*) FROM dbo.Consultorio;"), "Consultorios:");
			Afirmar.Igual(10, Escalar(fabrica, "SELECT COUNT(*) FROM dbo.Paciente;"), "Pacientes:");
			int turnos = Escalar(fabrica, "SELECT COUNT(*) FROM dbo.Turno;");
			int tablas = Escalar(fabrica, "SELECT COUNT(*) FROM sys.tables;");
			int indices = Escalar(fabrica, "SELECT COUNT(*) FROM sys.indexes WHERE name LIKE 'IX[_]%';");
			int procs = Escalar(fabrica, "SELECT COUNT(*) FROM sys.procedures;");
			int restricciones = Escalar(fabrica, "SELECT COUNT(*) FROM sys.objects WHERE type IN ('C', 'F', 'UQ', 'PK', 'D');");
			Afirmar.Igual(4, Escalar(fabrica, "SELECT COUNT(*) FROM vw_Historial WHERE PacienteId = 1;"), "Historial de Ana Méndez:");
			resultado = $"{tablas} tablas, 1 vista, {procs} procedimientos, {indices} índices, {restricciones} restricciones; " +
						$"10 pacientes y {turnos} turnos de demostración.";
		});
		return resultado;
	}

	[Prueba("Persistencia: al reiniciar la aplicación, la fila se reconstruye desde la base de datos", "RF11")]
	public static string Persistencia()
	{
		string resultado = "";
		ConBaseTemporal(fabrica =>
		{
			BaseDatos.Inicializar(fabrica, cargarDatosDemo: false);
			GestorTurnos NuevoGestor()
			{
				var g = new GestorTurnos(new RepositorioPacientesSql(fabrica), new RepositorioTurnosSql(fabrica),
										 new RepositorioCatalogosSql(fabrica), new Bitacora());
				g.Inicializar();
				return g;
			}
			var g1 = NuevoGestor();
			var p1 = g1.RegistrarPaciente(Cedula.Parse("1-1203-0456"), "Ana Méndez Quesada", new DateTime(1972, 3, 14), "", false, false);
			var p2 = g1.RegistrarPaciente(Cedula.Parse("5-0412-0876"), "María Rojas Díaz", new DateTime(1958, 5, 20), "", false, false);
			g1.AsignarTurno(p1, NivelPrioridad.Verde, "Control");
			g1.AsignarTurno(p2, NivelPrioridad.Rojo, "Dolor torácico");

			var g2 = NuevoGestor(); // simula cerrar y volver a abrir la aplicación
			string fila = string.Join(",", g2.FilaOrdenada().Select(t => t.Codigo));
			Afirmar.Igual("T-02,T-01", fila, "Fila reconstruida:");
			Afirmar.Verdadero(g2.BuscarPaciente(Cedula.Parse("5-0412-0876")) is not null, "El índice AVL no se reconstruyó.");
			var jose = g2.RegistrarPaciente(Cedula.Parse("2-0567-0912"), "José Solís Brenes", new DateTime(1979, 7, 2), "", false, false);
			var nuevo = g2.AsignarTurno(jose, NivelPrioridad.Amarillo, "Mareo");
			Afirmar.Igual("T-03", nuevo.Codigo, "La numeración continúa:");
			resultado = $"Fila después de reiniciar: {fila}; el siguiente código asignado fue {nuevo.Codigo}.";
		});
		return resultado;
	}

	[Prueba("Integridad: la base de datos rechaza cédulas duplicadas, llaves foráneas y estados inválidos", "RNF03")]
	public static string Integridad()
	{
		string resultado = "";
		ConBaseTemporal(fabrica =>
		{
			BaseDatos.Inicializar(fabrica, cargarDatosDemo: false);
			var repo = new RepositorioPacientesSql(fabrica);
			repo.Insertar(new Paciente(Cedula.Parse("1-1111-1111"), "Prueba Uno", new DateTime(1990, 1, 1)));
			var e1 = Afirmar.Lanza<SqlException>(
				() => repo.Insertar(new Paciente(Cedula.Parse("1-1111-1111"), "Prueba Dos", new DateTime(1990, 1, 1))), "Cédula duplicada");
			var fantasma = new Paciente(Cedula.Parse("9-9999-9999"), "No Existe", new DateTime(1990, 1, 1), id: 999);
			var e2 = Afirmar.Lanza<SqlException>(
				() => new RepositorioTurnosSql(fabrica).Insertar(
					new Turno("T-01", fantasma, NivelPrioridad.Rojo, "x", false, DateTime.Now), DateTime.Now), "Llave foránea");
			var e3 = Afirmar.Lanza<SqlException>(
				() => Escalar(fabrica, "UPDATE dbo.Paciente SET Cedula = '123' WHERE Id = 1; SELECT 1;"), "Formato de cédula");
			resultado = $"UQ_Paciente_Cedula: {(e1.Message.Contains("UQ_Paciente_Cedula") ? "rechazada" : e1.Number.ToString())} · " +
						$"FK_Turno_Paciente: {(e2.Message.Contains("FK_Turno_Paciente") ? "rechazada" : e2.Number.ToString())} · " +
						$"CK_Paciente_Cedula: {(e3.Message.Contains("CK_Paciente_Cedula") ? "rechazada" : e3.Number.ToString())}";
		});
		return resultado;
	}

	[Prueba("Seguridad: las consultas parametrizadas impiden la inyección de SQL", "RNF03")]
	public static string InyeccionSql()
	{
		string resultado = "";
		ConBaseTemporal(fabrica =>
		{
			BaseDatos.Inicializar(fabrica, cargarDatosDemo: false);
			const string ataque = "Robert'); DROP TABLE dbo.Paciente; --";
			var repo = new RepositorioPacientesSql(fabrica);
			repo.Insertar(new Paciente(Cedula.Parse("1-2222-2222"), ataque, new DateTime(1990, 1, 1)));
			var guardado = repo.ObtenerTodos().Single();
			Afirmar.Igual(ataque, guardado.Nombre, "El texto debe guardarse literalmente:");
			Afirmar.Igual(1, Escalar(fabrica, "SELECT COUNT(*) FROM dbo.Paciente;"), "La tabla Paciente sigue existiendo:");
			resultado = "El texto malicioso se guardó como un nombre común y la tabla no se eliminó.";
		});
		return resultado;
	}

	[Prueba("Procedimientos almacenados: historial y resumen por nivel calculados en el servidor", "RF10")]
	public static string Procedimientos()
	{
		string resultado = "";
		ConBaseTemporal(fabrica =>
		{
			BaseDatos.Inicializar(fabrica);
			var repo = new RepositorioTurnosSql(fabrica);
			var historial = repo.ObtenerHistorial(1).ToList();
			Afirmar.Igual(4, historial.Count, "usp_HistorialPaciente:");
			Afirmar.Verdadero(historial[0].Fecha >= historial[^1].Fecha, "El historial no viene en orden descendente.");
			var resumen = repo.ResumenPorNivel(DateTime.Today.AddDays(-30), DateTime.Today).ToList();
			Afirmar.Igual(5, resumen.Count, "usp_ResumenPorNivel (una fila por nivel):");
			resultado = string.Join(" · ", resumen.Select(f => $"{f.Categoria}: {f.Valor} min"));
		});
		return resultado;
	}
}
#endif
