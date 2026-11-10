using SistemaTurnos.UI;
using static System.Net.Mime.MediaTypeNames;

namespace SistemaTurnos.Formularios;

/// <summary>
/// Menú principal. Además mantiene el temporizador global que devuelve a la fila a los
/// pacientes ausentes cuando termina su tiempo de gracia.
/// </summary>
public class FrmInicio : Form
{
	private readonly Label lblEstado = new();
	private readonly System.Windows.Forms.Timer temporizadorReingresos = new() { Interval = 5000 };

	public FrmInicio()
	{
		Tema.ConfigurarFormulario(this, "Sistema de turnos", new Size(560, 560));
		FormBorderStyle = FormBorderStyle.FixedSingle;
		MaximizeBox = false;

		Controls.Add(Tema.CrearEncabezado("Sistema de turnos", out _));
		Controls.Add(Tema.CrearEtiqueta("Clínica Médica Integral del Pacífico", 40, 82, Tema.Negrita(13f), Tema.Primario));
		Controls.Add(Tema.CrearEtiqueta("Sistema de gestión priorizada de turnos. Seleccione una pantalla:", 40, 116));

		var opciones = new (string Texto, Func<Form> Crear)[]
		{
			("Recepción\nRegistro de pacientes y asignación de turnos", () => new FrmRecepcion()),
			("Consultorio\nLlamado, atención e historial", () => new FrmConsultorio()),
			("Pantalla pública\nSala de espera", () => new FrmPantallaPublica()),
			("Reportes\nIndicadores de atención y tiempos de espera", () => new FrmReportes()),
		};
		for (int i = 0; i < opciones.Length; i++)
		{
			var (texto, crear) = opciones[i];
			var boton = Tema.CrearBoton(texto, new Rectangle(40, 156 + i * 82, 480, 70), Tema.Primario);
			// Paradigma orientado a eventos: cada botón registra un manejador para su evento Click.
			boton.Click += (_, _) => Abrir(crear);
			Controls.Add(boton);
		}

		lblEstado.Location = new Point(40, 490);
		lblEstado.AutoSize = true;
		lblEstado.MaximumSize = new Size(480, 0);
		lblEstado.Font = Tema.Normal(8.5f);
		lblEstado.ForeColor = Tema.TextoSecundario;
		Controls.Add(lblEstado);

		temporizadorReingresos.Tick += (_, _) => ProcesarReingresos();
		temporizadorReingresos.Start();
		Aplicacion.Gestor.FilaCambiada += AlCambiarFila;
		FormClosed += (_, _) =>
		{
			temporizadorReingresos.Dispose();
			Aplicacion.Gestor.FilaCambiada -= AlCambiarFila;
		};
		ActualizarEstado();
	}

	private void Abrir(Func<Form> crear)
	{
		try { crear().Show(); }
		catch (Exception ex) { Tema.MostrarError(this, ex); }
	}

	private void ProcesarReingresos()
	{
		try { Aplicacion.Gestor.ProcesarReingresos(); }
		catch (Exception ex) { Aplicacion.Bitacora?.Error("Error al procesar reingresos", ex); }
	}

	private void AlCambiarFila(object? sender, EventArgs e) => Tema.EnHiloUI(this, ActualizarEstado);

	private void ActualizarEstado()
	{
		var g = Aplicacion.Gestor;
		lblEstado.Text = $"Turnos · En espera: {g.FilaOrdenada().Count} · Ausentes: {g.Ausentes().Count} · " +
			$"En atención: {g.TodosEnAtencion().Count}";
	}
}
