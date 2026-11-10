using System.Globalization;
using SistemaTurnos.Formularios;
using SistemaTurnos.UI;

namespace SistemaTurnos;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		// Fechas y horas en español de Costa Rica, sin importar el idioma de Windows.
		var cultura = new CultureInfo("es-CR");
		CultureInfo.DefaultThreadCurrentCulture = cultura;
		CultureInfo.DefaultThreadCurrentUICulture = cultura;
		Thread.CurrentThread.CurrentCulture = cultura;
		Thread.CurrentThread.CurrentUICulture = cultura;

		ApplicationConfiguration.Initialize();

		// Manejo global de errores: ningún error inesperado cierra la aplicación sin aviso;
		// todos quedan registrados en la bitácora.
		Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
		Application.ThreadException += (_, e) => Tema.MostrarError(null, e.Exception);
		AppDomain.CurrentDomain.UnhandledException += (_, e) =>
		{
			try { Aplicacion.Bitacora?.Error("Error no controlado", e.ExceptionObject as Exception); }
			catch { /* último recurso: no hacer nada */ }
		};

		try
		{
			Aplicacion.Iniciar();
		}
		catch (Exception ex)
		{
			MessageBox.Show("No se pudo conectar con SQL Server.\n\n" + ex.Message +
				"\n\nVerifique que el servidor esté en ejecución y que la cadena de conexión de " +
				"configuracion.json sea correcta (vea el archivo README.md).", "Sistema de turnos",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
			return;
		}

		Application.Run(new FrmInicio());
	}
}
