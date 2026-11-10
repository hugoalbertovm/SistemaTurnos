using SistemaTurnos.Core.Modelo;

namespace SistemaTurnos.UI;

/// <summary>
/// Colores, fuentes y fábricas de controles compartidos por todas las pantallas.
/// </summary>
public static class Tema
{
	public static readonly Color Primario = Color.FromArgb(47, 75, 124);
	public static readonly Color PrimarioClaro = Color.FromArgb(232, 237, 245);
	public static readonly Color Fondo = Color.FromArgb(247, 248, 250);
	public static readonly Color Borde = Color.FromArgb(208, 214, 226);
	public static readonly Color Exito = Color.FromArgb(27, 120, 55);
	public static readonly Color Peligro = Color.FromArgb(185, 28, 28);
	public static readonly Color Neutro = Color.FromArgb(217, 217, 217);
	public static readonly Color TextoSecundario = Color.FromArgb(90, 90, 90);

	public const string Fuente = "Segoe UI";
	public static Font Normal(float tam = 10f) => new(Fuente, tam, FontStyle.Regular);
	public static Font Negrita(float tam = 10f) => new(Fuente, tam, FontStyle.Bold);

	/// <summary>
	/// Configuración común de las ventanas. AutoScaleMode.Dpi hace que las posiciones
	/// diseñadas a 96 DPI se escalen bien en pantallas Retina (por ejemplo, en Parallels).
	/// </summary>
	public static void ConfigurarFormulario(Form form, string titulo, Size tamano)
	{
		form.AutoScaleDimensions = new SizeF(96F, 96F);
		form.AutoScaleMode = AutoScaleMode.Dpi;
		form.Text = titulo;
		form.ClientSize = tamano;
		form.StartPosition = FormStartPosition.CenterScreen;
		form.BackColor = Fondo;
		form.Font = Normal();
	}

	/// <summary>Barra superior con el título de la pantalla y un reloj que se actualiza solo.</summary>
	public static Panel CrearEncabezado(string titulo, out Label lblTitulo)
	{
		var panel = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Primario };
		lblTitulo = new Label
		{
			Text = titulo,
			ForeColor = Color.White,
			Font = Negrita(15f),
			AutoSize = true,
			Location = new Point(20, 14)
		};
		var reloj = new Label
		{
			Text = DateTime.Now.ToString("dddd d 'de' MMMM, HH:mm"),
			ForeColor = Color.White,
			Font = Normal(10f),
			AutoSize = true,
			Anchor = AnchorStyles.Top | AnchorStyles.Right
		};
		panel.Controls.Add(lblTitulo);
		panel.Controls.Add(reloj);
		panel.Layout += (_, _) => reloj.Location = new Point(panel.Width - reloj.Width - 20, 19);

		var temporizador = new System.Windows.Forms.Timer { Interval = 15_000 };
		temporizador.Tick += (_, _) => reloj.Text = DateTime.Now.ToString("dddd d 'de' MMMM, HH:mm");
		temporizador.Start();
		panel.Disposed += (_, _) => temporizador.Dispose();
		return panel;
	}

	/// <summary>Panel blanco con borde y título, usado como "tarjeta".</summary>
	public static Panel CrearTarjeta(string titulo, Rectangle limites)
	{
		var panel = new Panel { Bounds = limites, BackColor = Color.White };
		panel.Paint += (_, e) =>
		{
			using var pen = new Pen(Borde);
			e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
		};
		panel.Controls.Add(new Label
		{
			Text = titulo,
			Font = Negrita(11f),
			ForeColor = Primario,
			AutoSize = true,
			Location = new Point(14, 12)
		});
		return panel;
	}

	public static Label CrearEtiqueta(string texto, int x, int y, Font? fuente = null, Color? color = null) => new()
	{
		Text = texto,
		Location = new Point(x, y),
		AutoSize = true,
		Font = fuente ?? Normal(9.5f),
		ForeColor = color ?? TextoSecundario
	};

	public static Button CrearBoton(string texto, Rectangle limites, Color fondo, Color? textoColor = null)
	{
		var boton = new Button
		{
			Text = texto,
			Bounds = limites,
			BackColor = fondo,
			ForeColor = textoColor ?? Color.White,
			Font = Negrita(10f),
			FlatStyle = FlatStyle.Flat,
			Cursor = Cursors.Hand
		};
		boton.FlatAppearance.BorderSize = 0;
		return boton;
	}

	/// <summary>Cuadro de color que representa un nivel de prioridad.</summary>
	public static Panel CrearMuestraColor(NivelPrioridad nivel, Rectangle limites)
	{
		var muestra = new Panel { Bounds = limites, BackColor = nivel.ColorFondo() };
		muestra.Paint += (_, e) =>
		{
			using var pen = new Pen(Color.FromArgb(110, 110, 110));
			e.Graphics.DrawRectangle(pen, 0, 0, muestra.Width - 1, muestra.Height - 1);
		};
		return muestra;
	}

	/// <summary>Aplica el estilo común a una tabla de solo lectura.</summary>
	public static void EstilizarTabla(DataGridView tabla)
	{
		tabla.BackgroundColor = Color.White;
		tabla.BorderStyle = BorderStyle.None;
		tabla.RowHeadersVisible = false;
		tabla.AllowUserToAddRows = false;
		tabla.AllowUserToDeleteRows = false;
		tabla.AllowUserToResizeRows = false;
		tabla.ReadOnly = true;
		tabla.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
		tabla.MultiSelect = false;
		tabla.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
		tabla.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
		tabla.GridColor = Color.FromArgb(235, 235, 235);
		tabla.EnableHeadersVisualStyles = false;
		tabla.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
		tabla.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
		tabla.ColumnHeadersHeight = 36;
		tabla.ColumnHeadersDefaultCellStyle.BackColor = PrimarioClaro;
		tabla.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
		tabla.ColumnHeadersDefaultCellStyle.Font = Negrita(9.5f);
		tabla.ColumnHeadersDefaultCellStyle.SelectionBackColor = PrimarioClaro;
		tabla.DefaultCellStyle.Font = Normal(9.5f);
		tabla.DefaultCellStyle.SelectionBackColor = Color.FromArgb(214, 224, 240);
		tabla.DefaultCellStyle.SelectionForeColor = Color.Black;
		tabla.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 250);
		tabla.RowTemplate.Height = 32;
	}

	/// <summary>Pinta la celda de prioridad con el color del nivel.</summary>
	public static void ColorearCelda(DataGridViewCell celda, NivelPrioridad nivel)
	{
		celda.Style.BackColor = nivel.ColorFondo();
		celda.Style.ForeColor = nivel.ColorTexto();
		celda.Style.SelectionBackColor = nivel.ColorFondo();
		celda.Style.SelectionForeColor = nivel.ColorTexto();
		celda.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
	}

	/// <summary>
	/// Ejecuta una acción en el hilo de la interfaz. Los eventos del gestor o de la bitácora
	/// pueden llegar desde otro hilo, y Windows Forms solo permite tocar controles desde el suyo.
	/// </summary>
	public static void EnHiloUI(Control control, Action accion)
	{
		if (control.IsDisposed || !control.IsHandleCreated) return;
		if (control.InvokeRequired) control.BeginInvoke(accion);
		else accion();
	}

	/// <summary>
	/// Muestra un error de forma amigable y lo registra en la bitácora. Los errores de
	/// validación (reglas del negocio) se muestran como advertencia.
	/// </summary>
	public static void MostrarError(IWin32Window? dueno, Exception ex)
	{
		bool esValidacion = ex is InvalidOperationException or ArgumentException or FormatException;
		try
		{
			if (esValidacion) Aplicacion.Bitacora?.Advertencia($"Validación: {ex.Message}");
			else Aplicacion.Bitacora?.Error("Error inesperado", ex);
		}
		catch { /* la bitácora nunca debe provocar otro error */ }

		string texto = esValidacion ? ex.Message : $"Ocurrió un error inesperado:\n\n{ex.Message}\n\nQuedó registrado en la bitácora.";
		MessageBox.Show(dueno, texto, "Sistema de turnos", MessageBoxButtons.OK,
			esValidacion ? MessageBoxIcon.Warning : MessageBoxIcon.Error);
	}

	public static void Informar(IWin32Window dueno, string texto) =>
		MessageBox.Show(dueno, texto, "Sistema de turnos", MessageBoxButtons.OK, MessageBoxIcon.Information);
}

/// <summary>Colores de los niveles de prioridad (iguales a la Tabla III del documento).</summary>
public static class ColoresNivel
{
	public static Color ColorFondo(this NivelPrioridad nivel) => nivel switch
	{
		NivelPrioridad.Azul => Color.FromArgb(31, 95, 191),
		NivelPrioridad.Rojo => Color.FromArgb(214, 39, 40),
		NivelPrioridad.Amarillo => Color.FromArgb(242, 196, 15),
		NivelPrioridad.Verde => Color.FromArgb(44, 160, 44),
		_ => Color.White
	};

	public static Color ColorTexto(this NivelPrioridad nivel) =>
		nivel is NivelPrioridad.Amarillo or NivelPrioridad.Blanco ? Color.Black : Color.White;
}
