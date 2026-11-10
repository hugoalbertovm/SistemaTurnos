using System.Globalization;
using SistemaTurnos.Core.Datos;

namespace SistemaTurnos.Core.Servicios;

public enum TipoBitacora { Info, Advertencia, Error }

public record EntradaBitacora(DateTime FechaHora, TipoBitacora Tipo, string Mensaje)
{
	public override string ToString() =>
		$"{FechaHora.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} [{Tipo.ToString().ToUpperInvariant(),-11}] {Mensaje}";
}

public interface IBitacora
{
	void Info(string mensaje);
	void Advertencia(string mensaje);
	void Error(string mensaje, Exception? ex = null);
	IReadOnlyList<EntradaBitacora> Recientes();
	event EventHandler<EntradaBitacora>? Registrada;
}

/// <summary>
/// Bitácora (log) del sistema: escribe en un archivo de texto diario, en la base de datos
/// y en memoria. Es segura para hilos (lock), porque la simulación paralela y la
/// interfaz pueden registrar eventos al mismo tiempo.
/// </summary>
public class Bitacora : IBitacora
{
	private readonly object _candado = new();
	private readonly LinkedList<EntradaBitacora> _recientes = new();
	private readonly string? _carpeta;
	private readonly IRepositorioBitacora? _repositorio;
	private const int MaximoEnMemoria = 500;

	public event EventHandler<EntradaBitacora>? Registrada;

	public Bitacora(string? carpeta = null, IRepositorioBitacora? repositorio = null)
	{
		_carpeta = carpeta;
		_repositorio = repositorio;
		if (_carpeta is not null) Directory.CreateDirectory(_carpeta);
	}

	public void Info(string mensaje) => Registrar(TipoBitacora.Info, mensaje);
	public void Advertencia(string mensaje) => Registrar(TipoBitacora.Advertencia, mensaje);
	public void Error(string mensaje, Exception? ex = null) =>
		Registrar(TipoBitacora.Error, ex is null ? mensaje : $"{mensaje} — {ex.GetType().Name}: {ex.Message}");

	public IReadOnlyList<EntradaBitacora> Recientes()
	{
		lock (_candado) return _recientes.ToList();
	}

	private void Registrar(TipoBitacora tipo, string mensaje)
	{
		var entrada = new EntradaBitacora(DateTime.Now, tipo, mensaje);
		lock (_candado)
		{
			_recientes.AddLast(entrada);
			if (_recientes.Count > MaximoEnMemoria) _recientes.RemoveFirst();
			try
			{
				if (_carpeta is not null)
					File.AppendAllText(Path.Combine(_carpeta, $"bitacora-{entrada.FechaHora:yyyyMMdd}.log"),
									   entrada + Environment.NewLine);
				_repositorio?.Insertar(entrada.FechaHora, tipo switch
				{
					TipoBitacora.Info => "INFO",
					TipoBitacora.Advertencia => "ADVERTENCIA",
					_ => "ERROR"
				}, mensaje);
			}
			catch (Exception)
			{
				// La bitácora nunca debe detener la aplicación.
			}
		}
		Registrada?.Invoke(this, entrada);
	}
}
