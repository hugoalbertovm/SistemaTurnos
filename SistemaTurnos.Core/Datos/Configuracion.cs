using System.Text.Json;

namespace SistemaTurnos.Core.Datos;

/// <summary>
/// Lee la cadena de conexión desde configuracion.json, para poder cambiar de servidor
/// (SQL Server Express, Docker o Azure SQL) sin recompilar la aplicación.
/// </summary>
public sealed class Configuracion
{
	public const string CadenaPorDefecto =
		@"Server=localhost\SQLEXPRESS;Database=SistemaTurnos;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=10";

	public string CadenaConexion { get; init; } = CadenaPorDefecto;
	public int MinutosGracia { get; init; } = 1;

	public static Configuracion Cargar(string ruta)
	{
		if (!File.Exists(ruta)) return new Configuracion();
		var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
		return JsonSerializer.Deserialize<Configuracion>(File.ReadAllText(ruta), opciones) ?? new Configuracion();
	}
}
