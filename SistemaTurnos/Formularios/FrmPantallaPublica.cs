using SistemaTurnos.Core.Modelo;
using SistemaTurnos.UI;
using static System.Net.Mime.MediaTypeNames;

namespace SistemaTurnos.Formularios;

/// <summary>
/// Pantalla de la sala de espera. Se actualiza sola mediante los eventos TurnoLlamado y
/// FilaCambiada del gestor. Por privacidad solo muestra números de turno, nunca nombres.
/// </summary>
public class FrmPantallaPublica : Form
{
	private static readonly Color FondoOscuro = Color.FromArgb(24, 34, 56);
	private static readonly Color PanelOscuro = Color.FromArgb(38, 52, 82);
	private static readonly Color Destello = Color.FromArgb(242, 196, 15);

	private readonly Label lblReloj = new();
	private readonly Panel pnlLlamado = new();
	private readonly Label lblCodigo = new();
	private readonly Label lblConsultorio = new();
	private readonly Panel pnlProximos = new();
	private readonly System.Windows.Forms.Timer temporizadorReloj = new() { Interval = 1000 };
	private readonly System.Windows.Forms.Timer temporizadorDestello = new() { Interval = 350 };
	private int _destellosRestantes;

	public FrmPantallaPublica()
	{
		Tema.ConfigurarFormulario(this, "Sistema de turnos — Sala de espera", new Size(1180, 680));
		BackColor = FondoOscuro;
		MinimumSize = new Size(1000, 640);

		Controls.Add(new Label
		{
			Text = "Clínica Médica Integral del Pacífico",
			ForeColor = Color.White,
			Font = Tema.Negrita(18f),
			AutoSize = true,
			Location = new Point(40, 30)
		});
		lblReloj.ForeColor = Color.White;
		lblReloj.Font = Tema.Normal(18f);
		lblReloj.AutoSize = true;
		lblReloj.Location = new Point(1010, 32);
		lblReloj.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		Controls.Add(lblReloj);

		pnlLlamado.Bounds = new Rectangle(40, 100, 700, 460);
		pnlLlamado.BackColor = Tema.Primario;
		pnlLlamado.Controls.Add(new Label
		{
			Text = "TURNO LLAMADO",
			ForeColor = Color.FromArgb(200, 215, 240),
			Font = Tema.Negrita(20f),
			Bounds = new Rectangle(0, 40, 700, 50),
			TextAlign = ContentAlignment.MiddleCenter
		});
		lblCodigo.Font = Tema.Negrita(110f);
		lblCodigo.ForeColor = Color.White;
		lblCodigo.Bounds = new Rectangle(0, 100, 700, 200);
		lblCodigo.TextAlign = ContentAlignment.MiddleCenter;
		lblConsultorio.Font = Tema.Negrita(28f);
		lblConsultorio.ForeColor = Color.White;
		lblConsultorio.Bounds = new Rectangle(0, 320, 700, 70);
		lblConsultorio.TextAlign = ContentAlignment.MiddleCenter;
		pnlLlamado.Controls.Add(lblCodigo);
		pnlLlamado.Controls.Add(lblConsultorio);
		Controls.Add(pnlLlamado);

		pnlProximos.Bounds = new Rectangle(770, 100, 370, 460);
		pnlProximos.BackColor = PanelOscuro;
		Controls.Add(pnlProximos);

		Controls.Add(new Label
		{
			Text = "Por su privacidad, esta pantalla solo muestra el número de turno. " +
				   $"Si no responde al llamado, será llamado nuevamente hasta {Turno.MaximoLlamados} veces.",
			ForeColor = Color.FromArgb(200, 210, 230),
			Font = Tema.Normal(11f),
			Bounds = new Rectangle(40, 590, 1100, 50),
			TextAlign = ContentAlignment.MiddleCenter,
			Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
		});

		// Paradigma orientado a eventos: temporizadores (Tick) y eventos del gestor.
		temporizadorReloj.Tick += (_, _) => lblReloj.Text = DateTime.Now.ToString("HH:mm:ss");
		temporizadorDestello.Tick += (_, _) => Destellar();
		temporizadorReloj.Start();
		lblReloj.Text = DateTime.Now.ToString("HH:mm:ss");

		Aplicacion.Gestor.TurnoLlamado += AlLlamarTurno;
		Aplicacion.Gestor.FilaCambiada += AlCambiarFila;
		FormClosed += (_, _) =>
		{
			Aplicacion.Gestor.TurnoLlamado -= AlLlamarTurno;
			Aplicacion.Gestor.FilaCambiada -= AlCambiarFila;
			temporizadorReloj.Dispose();
			temporizadorDestello.Dispose();
		};

		MostrarLlamado(Aplicacion.Gestor.UltimoLlamado ?? Aplicacion.Gestor.TodosEnAtencion().FirstOrDefault());
		RefrescarProximos();
	}

	private void AlLlamarTurno(object? sender, TurnoEventArgs e) => Tema.EnHiloUI(this, () =>
	{
		MostrarLlamado(e.Turno);
		_destellosRestantes = 8;
		temporizadorDestello.Start();
	});

	private void AlCambiarFila(object? sender, EventArgs e) => Tema.EnHiloUI(this, RefrescarProximos);

	private void MostrarLlamado(Turno? t)
	{
		if (t is null || t.ConsultorioId is null)
		{
			lblCodigo.Text = "—";
			lblConsultorio.Text = "Esperando el próximo llamado";
			return;
		}
		int numero = Aplicacion.Gestor.Consultorios.FirstOrDefault(c => c.Id == t.ConsultorioId)?.Numero ?? t.ConsultorioId.Value;
		lblCodigo.Text = t.Codigo;
		lblConsultorio.Text = $"Diríjase al CONSULTORIO {numero}";
	}

	private void Destellar()
	{
		_destellosRestantes--;
		lblCodigo.ForeColor = _destellosRestantes % 2 == 0 ? Color.White : Destello;
		if (_destellosRestantes <= 0)
		{
			temporizadorDestello.Stop();
			lblCodigo.ForeColor = Color.White;
		}
	}

	private void RefrescarProximos()
	{
		pnlProximos.SuspendLayout();
		// Se liberan los controles anteriores (Dispose) para no acumular objetos que nadie usa.
		foreach (var c in pnlProximos.Controls.Cast<Control>().ToList()) c.Dispose();
		pnlProximos.Controls.Clear();
		pnlProximos.Controls.Add(new Label
		{
			Text = "Próximos turnos",
			ForeColor = Color.White,
			Font = Tema.Negrita(18f),
			AutoSize = true,
			Location = new Point(24, 20)
		});
		// Se muestran los cinco siguientes según el orden de la cola de prioridad.
		var proximos = Aplicacion.Gestor.FilaOrdenada().Take(5).ToList();
		if (proximos.Count == 0)
		{
			pnlProximos.Controls.Add(new Label
			{
				Text = "No hay turnos en espera",
				ForeColor = Color.FromArgb(170, 185, 210),
				Font = Tema.Normal(13f),
				AutoSize = true,
				Location = new Point(24, 90)
			});
		}
		for (int i = 0; i < proximos.Count; i++)
		{
			var fila = new Panel { Bounds = new Rectangle(24, 80 + i * 72, 322, 60), BackColor = FondoOscuro };
			fila.Controls.Add(new Panel { Bounds = new Rectangle(0, 0, 10, 60), BackColor = proximos[i].Nivel.ColorFondo() });
			fila.Controls.Add(new Label
			{
				Text = proximos[i].Codigo,
				ForeColor = Color.White,
				Font = Tema.Negrita(22f),
				AutoSize = true,
				Location = new Point(26, 12)
			});
			fila.Controls.Add(new Label
			{
				Text = "En espera",
				ForeColor = Color.FromArgb(170, 185, 210),
				Font = Tema.Normal(12f),
				AutoSize = true,
				Location = new Point(200, 20)
			});
			pnlProximos.Controls.Add(fila);
		}
		pnlProximos.ResumeLayout();
	}
}
