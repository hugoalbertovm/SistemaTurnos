using SistemaTurnos.Core.Modelo;
using SistemaTurnos.UI;

namespace SistemaTurnos.Formularios;

/// <summary>
/// Recepción: busca o registra al paciente, evalúa las reglas de atención preferencial,
/// asigna el turno y muestra la fila ordenada por la cola de prioridad.
/// </summary>
public class FrmRecepcion : Form
{
	private readonly TextBox txtBuscar = new();
	private readonly TextBox txtMensajeBusqueda = new();
	private readonly TextBox txtCedula = new();
	private readonly TextBox txtNombre = new();
	private readonly DateTimePicker dtpNacimiento = new();
	private readonly Label lblEdad = new();
	private readonly TextBox txtTelefono = new();
	private readonly CheckBox chkEmbarazo = new();
	private readonly CheckBox chkDiscapacidad = new();
	private readonly Label lblPreferencial = new();
	private readonly TextBox txtMotivo = new();
	private readonly Label lblNivelSeleccionado = new();
	private readonly Label lblResultado = new();
	private readonly DataGridView dgvFila = new();
	private readonly Label lblResumen = new();
	private readonly List<RadioButton> botonesNivel = new();
	private readonly System.Windows.Forms.Timer temporizador = new() { Interval = 30_000 };

	public FrmRecepcion()
	{
		Tema.ConfigurarFormulario(this, "Sistema de turnos — Recepción", new Size(1180, 770));
		MinimumSize = new Size(1000, 700);

		Controls.Add(Tema.CrearEncabezado("Sistema de turnos — Recepción", out _));
		Controls.Add(CrearPanelRegistro());
		Controls.Add(CrearPanelFila());

		// Paradigma orientado a eventos: la pantalla se suscribe al evento del gestor y se
		// actualiza sola cuando cualquier otra ventana cambia la fila.
		Aplicacion.Gestor.FilaCambiada += AlCambiarFila;
		temporizador.Tick += (_, _) => RefrescarFila();
		temporizador.Start();
		FormClosed += (_, _) =>
		{
			// Si no se cancela la suscripción, el gestor mantendría una referencia a esta
			// ventana cerrada y el recolector de basura no podría liberarla (fuga de memoria).
			Aplicacion.Gestor.FilaCambiada -= AlCambiarFila;
			temporizador.Dispose();
		};
		Shown += (_, _) => { RefrescarFila(); txtBuscar.Focus(); };
		LimpiarFormulario();
	}

	private static Panel CrearTarjetaRecepcion(string titulo, Rectangle limites)
	{
		var panel = new Panel
		{
			Bounds = limites,
			BackColor = Color.White,
			BorderStyle = BorderStyle.FixedSingle
		};
		panel.Controls.Add(Tema.CrearEtiqueta(titulo, 14, 12, Tema.Negrita(11f), Tema.Primario));
		return panel;
	}

	// ---------------------------------------------------------------- Formulario de registro
	private Panel CrearPanelRegistro()
	{
		var t = CrearTarjetaRecepcion("Paciente y turno", new Rectangle(20, 76, 388, 674));
		t.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;
		t.AutoScroll = true;

		t.Controls.Add(Tema.CrearEtiqueta("Buscar por cédula o por nombre", 14, 44));
		txtBuscar.Bounds = new Rectangle(14, 66, 240, 28);
		txtBuscar.PlaceholderText = "5-0412-0876  o  Rojas";
		txtBuscar.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Buscar(); } };
		var btnBuscar = Tema.CrearBoton("Buscar", new Rectangle(262, 65, 104, 30), Tema.Primario);
		btnBuscar.Click += (_, _) => Buscar();
		txtMensajeBusqueda.Multiline = true;
		txtMensajeBusqueda.ReadOnly = true;
		txtMensajeBusqueda.ScrollBars = ScrollBars.Vertical;
		txtMensajeBusqueda.BorderStyle = BorderStyle.None;
		txtMensajeBusqueda.BackColor = Color.White;
		txtMensajeBusqueda.TabStop = false;
		txtMensajeBusqueda.Bounds = new Rectangle(14, 98, 352, 42);
		txtMensajeBusqueda.Font = Tema.Normal(8.5f);

		t.Controls.Add(Tema.CrearEtiqueta("Cédula", 14, 148));
		txtCedula.Bounds = new Rectangle(14, 170, 130, 28);
		txtCedula.PlaceholderText = "0-0000-0000";
		t.Controls.Add(Tema.CrearEtiqueta("Nombre completo", 154, 148));
		txtNombre.Bounds = new Rectangle(154, 170, 212, 28);

		t.Controls.Add(Tema.CrearEtiqueta("Fecha de nacimiento", 14, 206));
		dtpNacimiento.Bounds = new Rectangle(14, 228, 170, 28);
		dtpNacimiento.Format = DateTimePickerFormat.Short;
		dtpNacimiento.MinDate = new DateTime(1900, 1, 1);
		dtpNacimiento.Value = new DateTime(1990, 1, 1);
		dtpNacimiento.MaxDate = DateTime.Today;
		dtpNacimiento.ValueChanged += (_, _) => EvaluarReglas();
		t.Controls.Add(Tema.CrearEtiqueta("Teléfono", 196, 206));
		txtTelefono.Bounds = new Rectangle(196, 228, 170, 28);
		txtTelefono.PlaceholderText = "0000-0000";
		lblEdad.Location = new Point(14, 260);
		lblEdad.AutoSize = true;
		lblEdad.Font = Tema.Normal(8.5f);
		lblEdad.ForeColor = Tema.TextoSecundario;

		chkEmbarazo.Text = "Embarazo";
		chkEmbarazo.Location = new Point(14, 282);
		chkEmbarazo.AutoSize = true;
		chkEmbarazo.CheckedChanged += (_, _) => EvaluarReglas();
		chkDiscapacidad.Text = "Discapacidad";
		chkDiscapacidad.Location = new Point(150, 282);
		chkDiscapacidad.AutoSize = true;
		chkDiscapacidad.CheckedChanged += (_, _) => EvaluarReglas();

		lblPreferencial.Location = new Point(14, 312);
		lblPreferencial.AutoSize = true;
		lblPreferencial.MaximumSize = new Size(352, 0);
		lblPreferencial.Font = Tema.Negrita(9f);

		t.Controls.Add(Tema.CrearEtiqueta("Motivo de consulta", 14, 350));
		txtMotivo.Bounds = new Rectangle(14, 372, 352, 50);
		txtMotivo.Multiline = true;

		t.Controls.Add(Tema.CrearEtiqueta("Nivel de prioridad (lo asigna el personal de salud)", 14, 434));
		foreach (var nivel in Enum.GetValues<NivelPrioridad>())
		{
			int i = (int)nivel - 1;
			var rb = new RadioButton
			{
				Appearance = Appearance.Button,
				Text = ((int)nivel).ToString(),
				Tag = nivel,
				Bounds = new Rectangle(14 + i * 71, 458, 63, 40),
				TextAlign = ContentAlignment.MiddleCenter,
				FlatStyle = FlatStyle.Flat,
				BackColor = nivel.ColorFondo(),
				ForeColor = nivel.ColorTexto(),
				Font = Tema.Negrita(12f),
				Cursor = Cursors.Hand
			};
			rb.FlatAppearance.CheckedBackColor = nivel.ColorFondo();
			rb.FlatAppearance.MouseOverBackColor = nivel.ColorFondo();
			rb.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 120);
			rb.FlatAppearance.BorderSize = 1;
			rb.CheckedChanged += (_, _) => ActualizarNivelSeleccionado(rb);
			botonesNivel.Add(rb);
			t.Controls.Add(rb);
		}
		lblNivelSeleccionado.Location = new Point(14, 504);
		lblNivelSeleccionado.AutoSize = true;
		lblNivelSeleccionado.MaximumSize = new Size(352, 0);
		lblNivelSeleccionado.Font = Tema.Normal(9f);
		lblNivelSeleccionado.ForeColor = Tema.TextoSecundario;

		var btnAsignar = Tema.CrearBoton("ASIGNAR TURNO", new Rectangle(14, 546, 352, 46), Tema.Primario);
		btnAsignar.Font = Tema.Negrita(11f);
		btnAsignar.Click += (_, _) => AsignarTurno();
		var btnLimpiar = Tema.CrearBoton("Limpiar", new Rectangle(14, 600, 352, 32), Tema.PrimarioClaro, Tema.Primario);
		btnLimpiar.Click += (_, _) => LimpiarFormulario();

		lblResultado.Location = new Point(14, 644);
		lblResultado.AutoSize = true;
		lblResultado.MaximumSize = new Size(352, 0);
		lblResultado.Font = Tema.Negrita(9.5f);
		lblResultado.ForeColor = Tema.Exito;

		t.Controls.AddRange(new Control[]
		{
			txtBuscar, btnBuscar, txtMensajeBusqueda, txtCedula, txtNombre, dtpNacimiento, txtTelefono, lblEdad,
			chkEmbarazo, chkDiscapacidad, lblPreferencial, txtMotivo, lblNivelSeleccionado, btnAsignar, btnLimpiar, lblResultado
		});
		return t;
	}

	// ---------------------------------------------------------------- Fila de espera
	private Panel CrearPanelFila()
	{
		var t = CrearTarjetaRecepcion("Fila actual (ordenada por la cola de prioridad)", new Rectangle(416, 76, 744, 674));
		t.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

		dgvFila.Bounds = new Rectangle(0, 47, 742, 578);
		dgvFila.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
		Tema.EstilizarTabla(dgvFila);
		dgvFila.Columns.Add("colPos", "#");
		dgvFila.Columns.Add("colTurno", "Turno");
		dgvFila.Columns.Add("colPaciente", "Paciente");
		dgvFila.Columns.Add("colPrioridad", "Prioridad");
		dgvFila.Columns.Add("colPref", "Pref.");
		dgvFila.Columns.Add("colMotivo", "Motivo");
		dgvFila.Columns.Add("colLlegada", "Llegada");
		dgvFila.Columns.Add("colEspera", "Espera");
		int[] pesos = { 25, 50, 110, 75, 40, 140, 55, 55 };
		for (int i = 0; i < pesos.Length; i++) dgvFila.Columns[i].FillWeight = pesos[i];
		dgvFila.Columns["colTurno"]!.DefaultCellStyle.Font = Tema.Negrita(9.5f);

		lblResumen.Location = new Point(14, 640);
		lblResumen.AutoSize = true;
		lblResumen.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
		lblResumen.ForeColor = Tema.TextoSecundario;

		t.Controls.Add(dgvFila);
		t.Controls.Add(lblResumen);
		return t;
	}

	// ---------------------------------------------------------------- Acciones
	private void Buscar()
	{
		string texto = txtBuscar.Text.Trim();
		if (texto.Length == 0) return;
		try
		{
			if (Cedula.TryParse(texto, out var cedula))
			{
				// Sobrecarga 1: búsqueda por cédula en el árbol AVL, O(log n).
				var p = Aplicacion.Gestor.BuscarPaciente(cedula);
				if (p is null)
				{
					LimpiarDatosPaciente();
					txtCedula.Text = cedula.Valor;
					MostrarBusqueda($"Cédula {cedula} no registrada: complete los datos del paciente nuevo.", Tema.TextoSecundario);
					txtNombre.Focus();
				}
				else
				{
					CargarPaciente(p);
					MostrarBusqueda($"Paciente encontrado en el árbol AVL ({Aplicacion.Gestor.PacientesIndexados} nodos, altura {Aplicacion.Gestor.AlturaIndice}).", Tema.Exito);
				}
				return;
			}
			if (texto.Any(char.IsDigit))
			{
				MostrarBusqueda("La cédula no es válida. Formato: 0-0000-0000 (o 9 dígitos).", Tema.Peligro);
				return;
			}
			// Sobrecarga 2: búsqueda por nombre con LINQ.
			var resultados = Aplicacion.Gestor.BuscarPaciente(texto);
			if (resultados.Count == 0)
				MostrarBusqueda($"No hay pacientes cuyo nombre contenga «{texto}».", Tema.Peligro);
			else if (resultados.Count == 1)
			{
				CargarPaciente(resultados[0]);
				MostrarBusqueda("Paciente encontrado por nombre.", Tema.Exito);
			}
			else
			{
				var elegido = DialogoSeleccion.Elegir(this, "Seleccione el paciente", resultados);
				if (elegido is not null)
				{
					CargarPaciente(elegido);
					MostrarBusqueda($"{resultados.Count} coincidencias; se cargó el paciente seleccionado.", Tema.Exito);
				}
			}
		}
		catch (Exception ex) { Tema.MostrarError(this, ex); }
	}

	private void AsignarTurno()
	{
		try
		{
			if (!Cedula.TryParse(txtCedula.Text, out var cedula))
				throw new FormatException("Ingrese una cédula válida con el formato 0-0000-0000.");
			if (string.IsNullOrWhiteSpace(txtNombre.Text))
				throw new ArgumentException("Ingrese el nombre completo del paciente.");
			var seleccionado = botonesNivel.FirstOrDefault(b => b.Checked);
			if (seleccionado?.Tag is not NivelPrioridad nivel)
				throw new InvalidOperationException("Seleccione el nivel de prioridad asignado por el personal de salud.");
			if (string.IsNullOrWhiteSpace(txtMotivo.Text))
				throw new ArgumentException("Ingrese el motivo de consulta.");

			var paciente = Aplicacion.Gestor.RegistrarPaciente(cedula, txtNombre.Text, dtpNacimiento.Value.Date,
				txtTelefono.Text, chkEmbarazo.Checked, chkDiscapacidad.Checked);
			var turno = Aplicacion.Gestor.AsignarTurno(paciente, nivel, txtMotivo.Text);
			int posicion = Aplicacion.Gestor.FilaOrdenada().ToList().IndexOf(turno) + 1;

			LimpiarFormulario();
			lblResultado.ForeColor = Tema.Exito;
			lblResultado.Text = $"Turno {turno.Codigo} asignado a {paciente.NombreCorto} · prioridad {(int)nivel} ({nivel})" +
								$"{(turno.Preferencial ? " · preferencial" : "")} · posición {posicion} en la fila.";
			SeleccionarEnFila(turno.Codigo);
		}
		catch (Exception ex) { Tema.MostrarError(this, ex); }
	}

	// ---------------------------------------------------------------- Apoyo
	private void CargarPaciente(Paciente p)
	{
		txtCedula.Text = p.Cedula.Valor;
		txtNombre.Text = p.Nombre;
		dtpNacimiento.Value = p.FechaNacimiento;
		txtTelefono.Text = p.Telefono;
		chkEmbarazo.Checked = p.EsEmbarazada;
		chkDiscapacidad.Checked = p.TieneDiscapacidad;
		EvaluarReglas();
		txtMotivo.Focus();
	}

	/// <summary>Consulta las reglas lógicas cada vez que cambian la edad o las condiciones.</summary>
	private void EvaluarReglas()
	{
		int edad = Edad(dtpNacimiento.Value.Date, DateTime.Today);
		lblEdad.Text = $"Edad: {edad} años";
		var r = Aplicacion.Gestor.EvaluarPreferencial(edad, chkEmbarazo.Checked, chkDiscapacidad.Checked);
		lblPreferencial.ForeColor = r.EsPreferencial ? Tema.Exito : Tema.TextoSecundario;
		lblPreferencial.Text = r.EsPreferencial
			? $"★ Atención preferencial (regla lógica): {string.Join(", ", r.Razones)}"
			: "Sin atención preferencial según las reglas.";
	}

	private static int Edad(DateTime nacimiento, DateTime hoy)
	{
		int edad = hoy.Year - nacimiento.Year;
		if (nacimiento > hoy.AddYears(-edad)) edad--;
		return Math.Max(0, edad);
	}

	private void ActualizarNivelSeleccionado(RadioButton rb)
	{
		rb.FlatAppearance.BorderSize = rb.Checked ? 3 : 1;
		rb.FlatAppearance.BorderColor = rb.Checked ? Color.Black : Color.FromArgb(120, 120, 120);
		if (rb.Checked && rb.Tag is NivelPrioridad nivel)
			lblNivelSeleccionado.Text = $"Seleccionado: {(int)nivel} · {nivel} — {nivel.Descripcion()} (referencia ≤ {nivel.MinutosReferencia()} min)";
	}

	private void LimpiarDatosPaciente()
	{
		txtCedula.Clear();
		txtNombre.Clear();
		txtTelefono.Clear();
		dtpNacimiento.Value = new DateTime(1990, 1, 1);
		chkEmbarazo.Checked = false;
		chkDiscapacidad.Checked = false;
		EvaluarReglas();
	}

	private void LimpiarFormulario()
	{
		txtBuscar.Clear();
		txtMensajeBusqueda.Text = "";
		LimpiarDatosPaciente();
		txtMotivo.Clear();
		foreach (var b in botonesNivel) b.Checked = false;
		lblNivelSeleccionado.Text = "Ningún nivel seleccionado.";
		lblResultado.Text = "";
	}

	private void MostrarBusqueda(string texto, Color color)
	{
		txtMensajeBusqueda.ForeColor = color;
		txtMensajeBusqueda.Text = texto;
		txtMensajeBusqueda.Select(0, 0);
		txtMensajeBusqueda.ScrollToCaret();
	}

	private void AlCambiarFila(object? sender, EventArgs e) => Tema.EnHiloUI(this, RefrescarFila);

	private void RefrescarFila()
	{
		var ahora = DateTime.Now;
		var fila = Aplicacion.Gestor.FilaOrdenada();
		dgvFila.Rows.Clear();
		int posicion = 0;
		foreach (var t in fila)
		{
			int idx = dgvFila.Rows.Add(++posicion, t.Codigo, t.Paciente.NombreCorto, $"{(int)t.Nivel} · {t.Nivel}",
				t.Preferencial ? "★" : "", t.Motivo, t.HoraLlegada.ToString("HH:mm"), $"{t.MinutosEspera(ahora)} min");
			Tema.ColorearCelda(dgvFila.Rows[idx].Cells["colPrioridad"], t.Nivel);
		}
		dgvFila.ClearSelection();

		// Paradigma funcional: resumen calculado con LINQ sin modificar la colección.
		if (fila.Count == 0)
		{
			lblResumen.Text = "No hay pacientes en espera.";
			return;
		}
		double promedio = fila.Average(t => t.MinutosEspera(ahora));
		int criticos = fila.Count(t => t.Nivel <= NivelPrioridad.Rojo);
		int fueraDeTiempo = fila.Count(t => t.MinutosEspera(ahora) > t.Nivel.MinutosReferencia());
		lblResumen.Text = $"En espera: {fila.Count}   ·   Espera promedio: {promedio:0} min   ·   Niveles 1 y 2: {criticos}   ·   " +
						  $"Fuera del tiempo de referencia: {fueraDeTiempo}   ·   Ausentes: {Aplicacion.Gestor.Ausentes().Count}";
	}

	private void SeleccionarEnFila(string codigo)
	{
		foreach (DataGridViewRow fila in dgvFila.Rows)
			if ((string?)fila.Cells["colTurno"].Value == codigo) { fila.Selected = true; break; }
	}
}

/// <summary>Diálogo sencillo para elegir un paciente cuando la búsqueda por nombre tiene varios resultados.</summary>
internal static class DialogoSeleccion
{
	public static Paciente? Elegir(IWin32Window dueno, string titulo, IReadOnlyList<Paciente> opciones)
	{
		using var f = new Form();
		Tema.ConfigurarFormulario(f, titulo, new Size(420, 320));
		f.FormBorderStyle = FormBorderStyle.FixedDialog;
		f.MaximizeBox = f.MinimizeBox = false;
		f.StartPosition = FormStartPosition.CenterParent;
		var lista = new ListBox { Bounds = new Rectangle(16, 16, 388, 230), Font = Tema.Normal(10f) };
		foreach (var p in opciones) lista.Items.Add(p);
		lista.SelectedIndex = 0;
		var aceptar = Tema.CrearBoton("Seleccionar", new Rectangle(16, 260, 388, 40), Tema.Primario);
		aceptar.DialogResult = DialogResult.OK;
		lista.DoubleClick += (_, _) => f.DialogResult = DialogResult.OK;
		f.Controls.Add(lista);
		f.Controls.Add(aceptar);
		f.AcceptButton = aceptar;
		return f.ShowDialog(dueno) == DialogResult.OK ? lista.SelectedItem as Paciente : null;
	}
}
