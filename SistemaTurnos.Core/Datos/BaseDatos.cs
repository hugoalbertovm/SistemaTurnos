using System.Data.Common;
using SistemaTurnos.Core.Modelo;

namespace SistemaTurnos.Core.Datos;

/// <summary>Crea la base de datos, el esquema, los catálogos y los datos de demostración.</summary>
public static class BaseDatos
{
	/// <summary>Crea la base de datos en el servidor si todavía no existe (conectándose a master).</summary>
	public static bool CrearSiNoExiste(Func<DbConnection> fabricaMaster, string nombreBase)
	{
		using var con = Ado.Abrir(fabricaMaster);
		using var existe = Ado.Comando(con, "SELECT CASE WHEN DB_ID(@Nombre) IS NULL THEN 0 ELSE 1 END;", ("@Nombre", nombreBase));
		if (Convert.ToInt32(existe.ExecuteScalar()) == 1) return false;
		// QUOTENAME protege el nombre de la base de datos; no se concatena texto del usuario.
		using var crear = Ado.Comando(con,
			"DECLARE @sql NVARCHAR(300) = N'CREATE DATABASE ' + QUOTENAME(@Nombre) + N';'; EXEC sys.sp_executesql @sql;",
			("@Nombre", nombreBase));
		crear.CommandTimeout = 120;
		crear.ExecuteNonQuery();
		return true;
	}

	/// <summary>Elimina una base de datos (se usa en las pruebas de integración).</summary>
	public static void Eliminar(Func<DbConnection> fabricaMaster, string nombreBase)
	{
		using var con = Ado.Abrir(fabricaMaster);
		using var cmd = Ado.Comando(con,
			"IF DB_ID(@Nombre) IS NOT NULL BEGIN " +
			"DECLARE @sql NVARCHAR(400) = N'ALTER DATABASE ' + QUOTENAME(@Nombre) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
			"DROP DATABASE ' + QUOTENAME(@Nombre) + N';'; EXEC sys.sp_executesql @sql; END;",
			("@Nombre", nombreBase));
		cmd.ExecuteNonQuery();
	}

	public static void Inicializar(Func<DbConnection> fabrica, bool cargarDatosDemo = true)
	{
		using (var con = Ado.Abrir(fabrica))
		{
			foreach (string lote in Esquema.Lotes)
			{
				using var cmd = Ado.Comando(con, lote);
				cmd.ExecuteNonQuery();
			}
			if (Contar(con, "dbo.NivelPrioridad") == 0) CargarCatalogos(con);
		}

		if (cargarDatosDemo)
		{
			bool vacia;
			using (var con = Ado.Abrir(fabrica)) vacia = Contar(con, "dbo.Paciente") == 0;
			if (vacia) DatosDemo.Cargar(new RepositorioPacientesSql(fabrica), new RepositorioTurnosSql(fabrica),
										new RepositorioCatalogosSql(fabrica).ObtenerConsultorios().ToList(), DateTime.Now);
		}
	}

	private static int Contar(DbConnection con, string tabla)
	{
		// El nombre de la tabla viene de una constante del código, nunca del usuario.
		using var cmd = Ado.Comando(con, $"SELECT COUNT(*) FROM {tabla};");
		return Convert.ToInt32(cmd.ExecuteScalar());
	}

	/// <summary>Carga los niveles, los médicos y los consultorios dentro de una TRANSACCIÓN: todo o nada.</summary>
	private static void CargarCatalogos(DbConnection con)
	{
		using var tx = con.BeginTransaction();
		foreach (var n in Enum.GetValues<NivelPrioridad>())
		{
			using var cmd = Ado.Comando(con,
				"INSERT INTO dbo.NivelPrioridad (Id, Color, Descripcion, MinutosReferencia) VALUES (@Id, @Color, @Descripcion, @Minutos);",
				("@Id", (byte)n), ("@Color", n.ToString()), ("@Descripcion", n.Descripcion()), ("@Minutos", n.MinutosReferencia()));
			cmd.Transaction = tx;
			cmd.ExecuteNonQuery();
		}
		var medicos = new[]
		{
			("1-0987-0654", "Dra. Laura Campos Rojas", "Medicina general"),
			("2-0456-0789", "Dr. Andrés Salas Mora", "Medicina general"),
			("5-0321-0147", "Dra. Sofía Jiménez Arce", "Pediatría"),
			("1-1122-0334", "Dr. Mario Rodríguez Vega", "Ginecología"),
		};
		for (int i = 0; i < medicos.Length; i++)
		{
			var (cedula, nombre, especialidad) = medicos[i];
			using var cm = Ado.Comando(con,
				"INSERT INTO dbo.Medico (Cedula, Nombre, Especialidad) OUTPUT INSERTED.Id VALUES (@Cedula, @Nombre, @Especialidad);",
				("@Cedula", cedula), ("@Nombre", nombre), ("@Especialidad", especialidad));
			cm.Transaction = tx;
			int idMedico = Convert.ToInt32(cm.ExecuteScalar());
			using var cc = Ado.Comando(con, "INSERT INTO dbo.Consultorio (Numero, MedicoId) VALUES (@Numero, @MedicoId);",
				("@Numero", (byte)(i + 1)), ("@MedicoId", idMedico));
			cc.Transaction = tx;
			cc.ExecuteNonQuery();
		}
		tx.Commit();
	}
}
