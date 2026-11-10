using SistemaTurnos.Core.Estructuras;
using SistemaTurnos.Core.Lexico;
using SistemaTurnos.Core.Modelo;
using SistemaTurnos.UI;

namespace SistemaTurnos.Formularios;

/// <summary>
/// Consultorio: llama al siguiente turno, marca ausencias, finaliza la atención y muestra el
/// historial del paciente en una lista doblemente enlazada que se recorre con Anterior/Siguiente.
/// El historial se puede filtrar con un pequeño lenguaje de consultas (análisis léxico y sintáctico).
/// </summary>
public class FrmConsultorio : Form
{
	private readonly Label lblTitulo;
	private readonly ComboBox cboConsultorio = new();
	private readonly Label lblCodigo = new();
	private readonly Panel pnlColor = new();
	private readonly Label lblPrioridad = new();
	private readonly Label lblNombre = new();
	private readonly Label lblDatos = new();
	private readonly Label lblMotivo = new();
	private readonly Label lblTiempos = new();
	private readonly Button btnLlamar;
	private readonly Button btnAusente;
	private readonly Button btnFinalizar;
	private readonly DataGridView dgvAusentes = new();
	private readonly TextBox txtFiltro = new();
	private readonly Button btnBuscarPaciente = Tema.CrearBoton(
		"Buscar paciente", new Rectangle(342, 80, 142, 28), Tema.PrimarioClaro, Tema.Primario);
	private readonly Label lblEstadoFiltro = new();
	private readonly Button btnDetalleFiltro = Tema.CrearBoton(
		"Ayuda", new Rectangle(494, 80, 146, 28), Tema.PrimarioClaro, Tema.Primario);
	private string? _detalleFiltro;
	private readonly DataGridView dgvHistorial = new();
	private readonly Label lblPosicion = new();

	private ListaDobleEnlazada<AtencionHistorial> _historialCompleto = new();
	private ListaDobleEnlazada<AtencionHistorial> _historialVisible = new();
	private NodoDoble<AtencionHistorial>? _nodoActual;

	private int ConsultorioId => (cboConsultorio.SelectedItem as Consultorio)?.Id ?? 0;

	public FrmConsultorio()
	{
		Tema.ConfigurarFormulario(this, "Sistema de turnos — Consultorio", new Size(1180, 700));
		MinimumSize = new Size(1000, 660);

		var encabezado = Tema.CrearEncabezado("Sistema de turnos — Consultorio", out lblTitulo);
		Controls.Add(encabezado);

		// ---- Paciente en atención
		var tarjeta = CrearTarjetaConsultorio("Paciente en atención", new Rectangle(20, 76, 470, 318));
		tarjeta.Controls.Add(Tema.CrearEtiqueta("Consultorio:", 14, 43, Tema.Normal(9f)));
		cboConsultorio.Bounds = new Rectangle(104, 39, 352, 28);
		cboConsultorio.DropDownStyle = ComboBoxStyle.DropDownList;
		foreach (var c in Aplicacion.Gestor.Consultorios) cboConsultorio.Items.Add(c);
		cboConsultorio.SelectedIndexChanged += (_, _) => CambiarConsultorio();

		var filaTurno = new FlowLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false,
			Margin = new Padding(0, 0, 0, 6)
		};
		lblCodigo.AutoSize = true;
		lblCodigo.Font = Tema.Negrita(34f);
		lblCodigo.Margin = new Padding(0, 0, 18, 0);
		pnlColor.Size = new Size(34, 34);
		pnlColor.Margin = new Padding(0, 18, 0, 0);
		filaTurno.Controls.AddRange(new Control[] { lblCodigo, pnlColor });

		var detallesPaciente = new FlowLayoutPanel
		{
			Bounds = new Rectangle(14, 70, 442, 236),
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			AutoScroll = true
		};
		detallesPaciente.Controls.Add(filaTurno);
		lblNombre.Font = Tema.Negrita(12.5f);
		lblDatos.ForeColor = Tema.TextoSecundario;
		lblMotivo.ForeColor = Tema.TextoSecundario;
		lblTiempos.ForeColor = Tema.TextoSecundario;
		foreach (var etiqueta in new[] { lblPrioridad, lblNombre, lblDatos, lblMotivo, lblTiempos })
		{
			etiqueta.AutoSize = true;
			etiqueta.MaximumSize = new Size(418, 0);
			etiqueta.Margin = new Padding(0, 0, 0, 5);
			detallesPaciente.Controls.Add(etiqueta);
		}
		tarjeta.Controls.AddRange(new Control[] { cboConsultorio, detallesPaciente });
		Controls.Add(tarjeta);

		// ---- Acciones del médico
		btnLlamar = Tema.CrearBoton("LLAMAR SIGUIENTE", new Rectangle(20, 406, 226, 48), Tema.Primario);
		btnAusente = Tema.CrearBoton("MARCAR AUSENTE", new Rectangle(264, 406, 226, 48), Tema.Neutro, Color.Black);
		btnFinalizar = Tema.CrearBoton("FINALIZAR ATENCIÓN", new Rectangle(20, 464, 470, 48), Tema.Exito);
		btnLlamar.Click += (_, _) => Ejecutar(() =>
		{
			var t = Aplicacion.Gestor.LlamarSiguiente(ConsultorioId);
			if (t is null) Tema.Informar(this, "No hay pacientes en espera.");
		});
		btnAusente.Click += (_, _) => Ejecutar(() => Aplicacion.Gestor.MarcarAusente(ConsultorioId));
		btnFinalizar.Click += (_, _) => Ejecutar(() =>
		{
			var turno = Aplicacion.Gestor.EnAtencion(ConsultorioId);
			Aplicacion.Gestor.Finalizar(ConsultorioId);
			_pacienteHistorialEnReposo = turno?.Paciente;
			CargarHistorial(_pacienteHistorialEnReposo);
		});
		Controls.AddRange(new Control[] { btnLlamar, btnAusente, btnFinalizar });

		// ---- Ausentes
		var tarjetaAus = CrearTarjetaConsultorio($"Ausentes (reingresan tras {Aplicacion.Gestor.TiempoGracia.TotalMinutes:0} min de gracia)",
										   new Rectangle(20, 524, 470, 156));
		tarjetaAus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;
		dgvAusentes.Bounds = new Rectangle(0, 43, 468, 111);
		dgvAusentes.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
		Tema.EstilizarTabla(dgvAusentes);
		dgvAusentes.Columns.Add("colTurno", "Turno");
		dgvAusentes.Columns.Add("colLlamados", "Llamados");
		dgvAusentes.Columns.Add("colReingreso", "Reingresa a las");
		tarjetaAus.Controls.Add(dgvAusentes);
		Controls.Add(tarjetaAus);

		Controls.Add(CrearPanelHistorial());

		Aplicacion.Gestor.FilaCambiada += AlCambiarFila;
		FormClosed += (_, _) => Aplicacion.Gestor.FilaCambiada -= AlCambiarFila;
		if (cboConsultorio.Items.Count > 0) cboConsultorio.SelectedIndex = 0;
		else MostrarPaciente(null);
	}

	private static Panel CrearTarjetaConsultorio(string titulo, Rectangle limites)
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

	private Panel CrearPanelHistorial()
	{
		var t = CrearTarjetaConsultorio("Historial de atenciones", new Rectangle(506, 76, 654, 604));
		t.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

		txtFiltro.Bounds = new Rectangle(14, 46, 470, 28);
		txtFiltro.PlaceholderText = "Ej.: medico:mario";
		txtFiltro.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		txtFiltro.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AplicarFiltro(); } };
		var btnFiltrar = Tema.CrearBoton("Filtrar", new Rectangle(494, 45, 146, 30), Tema.PrimarioClaro, Tema.Primario);
		btnFiltrar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		btnFiltrar.Click += (_, _) => AplicarFiltro();

		btnBuscarPaciente.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		btnBuscarPaciente.Click += (_, _) => BuscarPacienteHistorial();
		lblEstadoFiltro.Bounds = new Rectangle(14, 80, 318, 28);
		lblEstadoFiltro.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		lblEstadoFiltro.AutoEllipsis = true;
		lblEstadoFiltro.TextAlign = ContentAlignment.MiddleLeft;
		lblEstadoFiltro.Font = Tema.Normal(9f);
		btnDetalleFiltro.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		btnDetalleFiltro.Click += (_, _) => Tema.Informar(this, _detalleFiltro ??
			"El texto sin campo busca en el motivo. Para buscar por médico, usa medico:mario." + Environment.NewLine + Environment.NewLine +
			string.Join(Environment.NewLine, AnalizadorConsultas.CamposDisponibles) +
			Environment.NewLine + Environment.NewLine + "Ejemplo: nivel:2 medico:campos desde:2026-01-01");
		MostrarEstadoFiltro(EstadoHistorial(), Tema.TextoSecundario);

		dgvHistorial.Bounds = new Rectangle(0, 118, 652, 329);
		dgvHistorial.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
		Tema.EstilizarTabla(dgvHistorial);
		dgvHistorial.Columns.Add("colFecha", "Fecha");
		dgvHistorial.Columns.Add("colMotivo", "Motivo");
		dgvHistorial.Columns.Add("colPrioridad", "Prioridad");
		dgvHistorial.Columns.Add("colMedico", "Médico");
		dgvHistorial.Columns.Add("colEspera", "Espera");
		dgvHistorial.Columns.Add("colPaciente", "Paciente");
		dgvHistorial.Columns["colPaciente"]!.Visible = false;
		int[] pesos = { 60, 150, 70, 110, 50 };
		for (int i = 0; i < pesos.Length; i++) dgvHistorial.Columns[i].FillWeight = pesos[i];
		dgvHistorial.CellClick += (_, e) => { if (e.RowIndex >= 0) IrAIndice(e.RowIndex); };

		var btnAnterior = Tema.CrearBoton("◀  Anterior", new Rectangle(14, 462, 150, 40), Tema.Primario);
		var btnSiguiente = Tema.CrearBoton("Siguiente  ▶", new Rectangle(176, 462, 150, 40), Tema.Primario);
		btnAnterior.Anchor = btnSiguiente.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
		// Recorrido de la lista doble: se sigue la referencia Anterior o Siguiente del nodo actual.
		btnAnterior.Click += (_, _) => { if (_nodoActual?.Anterior is not null) MostrarNodo(_nodoActual.Anterior); };
		btnSiguiente.Click += (_, _) => { if (_nodoActual?.Siguiente is not null) MostrarNodo(_nodoActual.Siguiente); };

		lblPosicion.Location = new Point(342, 473);
		lblPosicion.AutoSize = true;
		lblPosicion.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
		lblPosicion.ForeColor = Tema.TextoSecundario;

		t.Controls.AddRange(new Control[] { txtFiltro, btnFiltrar, lblEstadoFiltro, btnBuscarPaciente, btnDetalleFiltro, dgvHistorial, btnAnterior, btnSiguiente, lblPosicion });
		return t;
	}

	// ---------------------------------------------------------------- Acciones
	private void Ejecutar(Action accion)
	{
		if (ConsultorioId == 0) { Tema.Informar(this, "Seleccione un consultorio."); return; }
		try { accion(); }
		catch (Exception ex) { Tema.MostrarError(this, ex); }
		RefrescarTodo();
	}

	private void CambiarConsultorio()
	{
		_pacienteHistorialEnReposo = null;
		if (cboConsultorio.SelectedItem is Consultorio c)
		{
			lblTitulo.Text = $"Sistema de turnos — Consultorio {c.Numero}";
			Text = $"Sistema de turnos — Consultorio {c.Numero}";
		}
		RefrescarTodo();
	}

	private void AlCambiarFila(object? sender, EventArgs e) => Tema.EnHiloUI(this, RefrescarTodo);

	private void RefrescarTodo()
	{
		var turno = ConsultorioId == 0 ? null : Aplicacion.Gestor.EnAtencion(ConsultorioId);
		MostrarPaciente(turno);
		var pacienteHistorial = turno?.Paciente ?? _pacienteHistorialEnReposo;
		if (!ReferenceEquals(_pacienteMostrado, pacienteHistorial))
			CargarHistorial(pacienteHistorial);

		dgvAusentes.Rows.Clear();
		foreach (var t in Aplicacion.Gestor.Ausentes())
			dgvAusentes.Rows.Add(t.Codigo, $"{t.Llamados} de {Turno.MaximoLlamados}", t.ReingresoEn?.ToString("HH:mm:ss") ?? "—");
		dgvAusentes.ClearSelection();
	}

	private Paciente? _pacienteMostrado;
	private Paciente? _pacienteHistorialEnReposo;

	private void MostrarPaciente(Turno? t)
	{
		bool hay = t is not null;
		btnLlamar.Enabled = !hay;
		btnAusente.Enabled = hay;
		btnFinalizar.Enabled = hay;
		btnBuscarPaciente.Enabled = !hay;
		btnAusente.BackColor = hay ? Tema.Neutro : Color.FromArgb(235, 235, 235);
		btnFinalizar.BackColor = hay ? Tema.Exito : Color.FromArgb(170, 200, 180);
		btnLlamar.BackColor = hay ? Color.FromArgb(160, 172, 196) : Tema.Primario;

		if (t is null)
		{
			lblCodigo.Text = "—";
			pnlColor.Visible = false;
			lblPrioridad.Text = "";
			lblPrioridad.Visible = false;
			lblNombre.Text = "Sin paciente en atención";
			lblDatos.Text = $"En espera: {Aplicacion.Gestor.FilaOrdenada().Count} paciente(s).";
			lblMotivo.Text = "Presione «Llamar siguiente» para atender al paciente de mayor prioridad.";
			lblTiempos.Text = "";
			return;
		}

		var ahora = DateTime.Now;
		lblCodigo.Text = t.Codigo;
		pnlColor.Visible = true;
		lblPrioridad.Visible = true;
		pnlColor.BackColor = t.Nivel.ColorFondo();
		lblPrioridad.Text = $"Prioridad {(int)t.Nivel} · {t.Nivel} — {t.Nivel.Descripcion()}{(t.Preferencial ? " · ★ preferencial" : "")}";
		lblNombre.Text = t.Paciente.Nombre;
		lblDatos.Text = $"{t.Paciente.Edad(ahora)} años   ·   Cédula {t.Paciente.Cedula}   ·   Tel. {t.Paciente.Telefono}";
		lblMotivo.Text = $"Motivo: {t.Motivo}";
		lblTiempos.Text = $"Llegada {t.HoraLlegada:HH:mm} · llamado {t.HoraLlamado:HH:mm} · esperó {t.MinutosEspera(ahora)} min " +
						  $"(referencia ≤ {t.Nivel.MinutosReferencia()} min) · llamado {t.Llamados} de {Turno.MaximoLlamados}";
	}

	// ---------------------------------------------------------------- Historial
	private void BuscarPacienteHistorial()
	{
		using var dialogo = new Form();
		Tema.ConfigurarFormulario(dialogo, "Buscar paciente", new Size(420, 126));
		dialogo.FormBorderStyle = FormBorderStyle.FixedDialog;
		dialogo.StartPosition = FormStartPosition.CenterParent;
		dialogo.MaximizeBox = dialogo.MinimizeBox = false;

		var entrada = new TextBox { Bounds = new Rectangle(16, 36, 388, 28) };
		var buscar = Tema.CrearBoton("Buscar", new Rectangle(304, 78, 100, 32), Tema.Primario);
		buscar.DialogResult = DialogResult.OK;
		dialogo.Controls.AddRange(new Control[]
		{
			Tema.CrearEtiqueta("Nombre o cédula del paciente", 16, 12), entrada, buscar
		});
		dialogo.AcceptButton = buscar;
		dialogo.ActiveControl = entrada;
		if (dialogo.ShowDialog(this) != DialogResult.OK) return;

		string consulta = entrada.Text.Trim();
		if (consulta.Length == 0) return;

		try
		{
			Paciente? paciente;
			if (Cedula.TryParse(consulta, out var cedula))
			{
				paciente = Aplicacion.Gestor.BuscarPaciente(cedula);
				if (paciente is null)
				{
					Tema.Informar(this, "No se encontró un paciente con esa cédula.");
					return;
				}
			}
			else
			{
				var coincidencias = Aplicacion.Gestor.BuscarPaciente(consulta);
				if (coincidencias.Count == 0)
				{
					Tema.Informar(this, "No se encontraron pacientes con ese nombre.");
					return;
				}
				paciente = coincidencias.Count == 1 ? coincidencias[0]
					: DialogoSeleccion.Elegir(this, "Selecciona un paciente", coincidencias);
				if (paciente is null) return;
			}

			_pacienteHistorialEnReposo = paciente;
			CargarHistorial(paciente);
		}
		catch (Exception ex)
		{
			Tema.MostrarError(this, ex);
		}
	}

	private void CargarHistorial(Paciente? paciente)
	{
		_pacienteMostrado = paciente;
		_historialCompleto = paciente is null ? new ListaDobleEnlazada<AtencionHistorial>() : Aplicacion.Gestor.ObtenerHistorial(paciente);
		txtFiltro.Clear();
		MostrarEstadoFiltro(EstadoHistorial(), Tema.TextoSecundario);
		MostrarLista(_historialCompleto);
	}

	/// <summary>
	/// Analiza el texto del filtro: análisis léxico (tokens), sintáctico (gramática) y semántico
	/// (tabla de símbolos y tipos). El resultado es una función que se aplica al historial.
	/// </summary>
	private void AplicarFiltro()
	{
		if (string.IsNullOrWhiteSpace(txtFiltro.Text))
		{
			MostrarLista(_historialCompleto);
			MostrarEstadoFiltro(EstadoHistorial(), Tema.TextoSecundario);
			return;
		}
		var resultado = new AnalizadorConsultas().Analizar(txtFiltro.Text);
		if (!resultado.EsValida)
		{
			MostrarEstadoFiltro("Consulta no válida.", Tema.Peligro, string.Join(Environment.NewLine, resultado.Errores));
			Aplicacion.Bitacora?.Advertencia($"Filtro rechazado «{txtFiltro.Text}»: {resultado.Errores[0]}");
			return;
		}
		bool busquedaGeneral = _pacienteMostrado is null;
		try
		{
			IEnumerable<AtencionHistorial> origen = busquedaGeneral
				? Aplicacion.Gestor.ObtenerHistorialGeneral()
				: _historialCompleto;
			var filtrada = new ListaDobleEnlazada<AtencionHistorial>();
			foreach (var a in origen.Where(resultado.Predicado))
				filtrada.AgregarAlFinal(a);

			string alcance = _pacienteMostrado?.NombreCorto ?? "Historial general";
			string coincidencias = filtrada.Count == 1 ? "1 atención encontrada" : $"{filtrada.Count} atenciones encontradas";
			string detalle = $"Filtro aplicado: {txtFiltro.Text.Trim()}{Environment.NewLine}" +
				$"Historial consultado: {alcance}{Environment.NewLine}" +
				$"{coincidencias}.";
			MostrarEstadoFiltro($"{alcance} · {coincidencias}.", Tema.Exito, detalle);
			MostrarLista(filtrada, busquedaGeneral);
		}
		catch (Exception ex)
		{
			Tema.MostrarError(this, ex);
		}
	}

	private string EstadoHistorial()
	{
		if (_pacienteMostrado is null)
			return "Filtra el historial general.";

		return _historialCompleto.Count == 0
			? $"{_pacienteMostrado.NombreCorto} · Sin atenciones registradas."
			: $"{_pacienteMostrado.NombreCorto} · {_historialCompleto.Count} atenciones.";
	}

	private void MostrarEstadoFiltro(string mensaje, Color color, string? detalle = null)
	{
		lblEstadoFiltro.Text = mensaje;
		lblEstadoFiltro.ForeColor = color;
		_detalleFiltro = detalle;
		btnDetalleFiltro.Text = detalle is null ? "Ayuda" : "Ver detalles";
	}

	private void MostrarLista(ListaDobleEnlazada<AtencionHistorial> lista, bool busquedaGeneral = false)
	{
		_historialVisible = lista;
		dgvHistorial.Rows.Clear();
		dgvHistorial.Columns["colPaciente"]!.Visible = busquedaGeneral;
		foreach (var a in lista)
		{
			int i = dgvHistorial.Rows.Add(a.Fecha.ToString("dd/MM/yyyy"), a.Motivo, $"{(int)a.Nivel} · {a.Nivel}", a.Medico, $"{a.MinutosEspera} min", a.NombrePaciente);
			Tema.ColorearCelda(dgvHistorial.Rows[i].Cells["colPrioridad"], a.Nivel);
		}
		// Se inicia en el nodo más reciente (el último de la lista).
		if (lista.Ultimo is not null) MostrarNodo(lista.Ultimo);
		else
		{
			_nodoActual = null;
			lblPosicion.Text = _pacienteMostrado is null ? "" : "Sin atenciones registradas.";
			dgvHistorial.ClearSelection();
		}
	}

	private void IrAIndice(int indice)
	{
		var nodo = _historialVisible.Primero;
		for (int i = 0; i < indice && nodo is not null; i++) nodo = nodo.Siguiente;
		if (nodo is not null) MostrarNodo(nodo);
	}

	private void MostrarNodo(NodoDoble<AtencionHistorial> nodo)
	{
		_nodoActual = nodo;
		int indice = 0;
		for (var n = _historialVisible.Primero; n is not null && !ReferenceEquals(n, nodo); n = n.Siguiente) indice++;
		if (indice < dgvHistorial.Rows.Count)
		{
			dgvHistorial.ClearSelection();
			dgvHistorial.Rows[indice].Selected = true;
			try { dgvHistorial.FirstDisplayedScrollingRowIndex = indice; }
			catch (InvalidOperationException) { /* la tabla aún no se ha dibujado */ }
		}
		lblPosicion.Text = $"Registro {indice + 1} de {_historialVisible.Count}";
	}
}
