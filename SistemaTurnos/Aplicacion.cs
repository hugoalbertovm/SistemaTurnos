using System.Data.Common;
using Microsoft.Data.SqlClient;
using SistemaTurnos.Core.Datos;
using SistemaTurnos.Core.Servicios;

namespace SistemaTurnos;

/// <summary>
/// Punto único de configuración: lee configuracion.json, crea la base de datos en SQL Server
/// si no existe, aplica el esquema y crea la bitácora y el gestor de turnos.
/// Todas las ventanas comparten el mismo gestor, por eso los cambios se ven en todas al instante.
/// </summary>
internal static class Aplicacion
{
	public static GestorTurnos Gestor { get; private set; } = null!;
	public static Bitacora? Bitacora { get; private set; }
	public static string DescripcionBaseDatos { get; private set; } = "";
	public static string CarpetaBitacoras { get; private set; } = "";

	public static void Iniciar()
	{
		var configuracion = Configuracion.Cargar(Path.Combine(AppContext.BaseDirectory, "configuracion.json"));
		var builder = new SqlConnectionStringBuilder(configuracion.CadenaConexion);
		string nombreBase = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "SistemaTurnos" : builder.InitialCatalog;
		builder.InitialCatalog = nombreBase;
		string cadena = builder.ConnectionString;
		builder.InitialCatalog = "master";
		string cadenaMaster = builder.ConnectionString;
		DescripcionBaseDatos = $"SQL Server {builder.DataSource} · base de datos {nombreBase}";

		Func<DbConnection> fabricaMaster = () => new SqlConnection(cadenaMaster);
		Func<DbConnection> fabrica = () => new SqlConnection(cadena);

		bool creada = BaseDatos.CrearSiNoExiste(fabricaMaster, nombreBase);
		BaseDatos.Inicializar(fabrica);

		CarpetaBitacoras = Path.Combine(AppContext.BaseDirectory, "bitacoras");
		Bitacora = new Bitacora(CarpetaBitacoras, new RepositorioBitacoraSql(fabrica));
		Bitacora.Info(creada ? $"Base de datos {nombreBase} creada en {builder.DataSource}." : $"Conectado a {DescripcionBaseDatos}.");

		Gestor = new GestorTurnos(new RepositorioPacientesSql(fabrica), new RepositorioTurnosSql(fabrica),
								  new RepositorioCatalogosSql(fabrica), Bitacora)
		{
			TiempoGracia = TimeSpan.FromMinutes(Math.Max(1, configuracion.MinutosGracia))
		};
		Gestor.Inicializar();
	}
}
