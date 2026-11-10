using System.Diagnostics;
using SistemaTurnos.Core.Lexico;
using SistemaTurnos.Core.Logica;
using SistemaTurnos.Core.Modelo;
using SistemaTurnos.Core.Reportes;
using SistemaTurnos.Core.Servicios;
using SistemaTurnos.Core.Simulacion;
using SistemaTurnos.UI;

namespace SistemaTurnos.Formularios;

/// <summary>
/// Reportes (polimorfismo + LINQ), simulación en paralelo, bitácora en vivo y una vista
/// del estado interno de las estructuras de datos y de las reglas lógicas.
/// </summary>
public class FrmReportes : Form
{
	// Reportes
	private readonly ComboBox cboReporte = new();
	private readonly DateTimePicker dtpDesde = new();
	private readonly DateTimePicker dtpHasta = new();
	private readonly DataGridView dgvReporte = new();
	private readonly Panel pnlGrafico = new();
	private IReadOnlyList<FilaReporte> _filas = Array.Empty<FilaReporte>();

	// Simulación
	private readonly NumericUpDown numTurnos = new();
	private readonly NumericUpDown numReplicaciones = new();
	private readonly Button btnSimular;
	private readonly ProgressBar barra = new();
	private readonly DataGridView dgvSimulacion = new();
	private readonly Label lblSimulacion = new();

	// Bitácora
	private readonly ListBox lstBitacora = new();

	// Estructuras
	private readonly TextBox txtEstructuras = new();

	public FrmReportes() : this(false) { }

	// El diagnóstico se habilita explícitamente para una revisión técnica.
	internal FrmReportes(bool mostrarDiagnostico)
	{
		string titulo = mostrarDiagnostico ? "Sistema de turnos — Reportes y diagnóstico" : "Sistema de turnos — Reportes";
		Tema.ConfigurarFormulario(this, titulo, new Size(1100, 700));
		MinimumSize = new Size(960, 640);

		btnSimular = Tema.CrearBoton("EJECUTAR SIMULACIÓN", new Rectangle(560, 18, 220, 36), Tema.Primario);
		var reportes = CrearPanelReportes(mostrarDiagnostico);
		if (mostrarDiagnostico)
		{
			var pestanas = new TabControl { Dock = DockStyle.Fill, Font = Tema.Normal(10f) };
			var pestanaReportes = new TabPage("Reportes");
			pestanaReportes.Controls.Add(reportes);
			pestanas.TabPages.Add(pestanaReportes);
			pestanas.TabPages.Add(CrearPestanaSimulacion());
			pestanas.TabPages.Add(CrearPestanaBitacora());
			var estructuras = CrearPestanaEstructuras();
			pestanas.TabPages.Add(estructuras);
			pestanas.SelectedIndexChanged += (_, _) =>
			{
				if (pestanas.SelectedTab == estructuras) ActualizarEstructuras();
			};
			Controls.Add(pestanas);

			var bitacora = Aplicacion.Bitacora;
			if (bitacora is not null)
			{
				bitacora.Registrada += AlRegistrarBitacora;
				FormClosed += (_, _) => bitacora.Registrada -= AlRegistrarBitacora;
			}
		}
		else
		{
			Controls.Add(reportes);
			FormClosed += (_, _) => btnSimular.Dispose();
		}

		Controls.Add(Tema.CrearEncabezado(titulo, out _));
		Shown += (_, _) => GenerarReporte();
	}

	// ---------------------------------------------------------------- Reportes
	private TableLayoutPanel CrearPanelReportes(bool mostrarDiagnostico)
	{
		var contenido = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			BackColor = Color.White,
			Padding = new Padding(16),
			ColumnCount = 1,
			RowCount = 3
		};
		contenido.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
		contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		contenido.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
		contenido.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

		var filtros = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 4,
			RowCount = 2,
			Margin = new Padding(0, 0, 0, 12)
		};
		filtros.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
		filtros.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19));
		filtros.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19));
		filtros.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16));
		filtros.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		filtros.RowStyles.Add(new RowStyle(SizeType.AutoSize));

		filtros.Controls.Add(Tema.CrearEtiqueta("Reporte", 0, 0), 0, 0);
		filtros.Controls.Add(Tema.CrearEtiqueta("Desde", 0, 0), 1, 0);
		filtros.Controls.Add(Tema.CrearEtiqueta("Hasta", 0, 0), 2, 0);

		cboReporte.DropDownStyle = ComboBoxStyle.DropDownList;
		foreach (var r in CatalogoReportes.Todos) cboReporte.Items.Add(r.Titulo);
		if (mostrarDiagnostico)
			cboReporte.Items.Add("Resumen por nivel (procedimiento almacenado)");
		cboReporte.SelectedIndex = 0;

		dtpDesde.Format = DateTimePickerFormat.Short;
		dtpDesde.Value = DateTime.Today.AddDays(-30);
		dtpHasta.Format = DateTimePickerFormat.Short;
		dtpHasta.Value = DateTime.Today;
		var btn = Tema.CrearBoton("Generar", new Rectangle(0, 0, 120, 30), Tema.Primario);
		btn.Click += (_, _) => GenerarReporte();
		cboReporte.SelectedIndexChanged += (_, _) => GenerarReporte();

		var controlesFiltro = new Control[] { cboReporte, dtpDesde, dtpHasta, btn };
		for (int i = 0; i < controlesFiltro.Length; i++)
		{
			controlesFiltro[i].Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
			controlesFiltro[i].Margin = new Padding(3, 3, 12, 3);
			filtros.Controls.Add(controlesFiltro[i], i, 1);
		}

		dgvReporte.Dock = DockStyle.Fill;
		dgvReporte.Margin = new Padding(0);
		Tema.EstilizarTabla(dgvReporte);
		dgvReporte.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
		dgvReporte.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
		dgvReporte.Columns.Add("colCategoria", "Categoría");
		dgvReporte.Columns.Add("colValor", "Valor");
		dgvReporte.Columns.Add("colDetalle", "Detalle");
		dgvReporte.Columns[0].FillWeight = 90;
		dgvReporte.Columns[1].FillWeight = 45;
		dgvReporte.Columns[2].FillWeight = 165;

		pnlGrafico.Dock = DockStyle.Fill;
		pnlGrafico.Margin = new Padding(0, 10, 0, 0);
		pnlGrafico.BackColor = Color.White;
		pnlGrafico.Paint += DibujarGrafico;
		pnlGrafico.Resize += (_, _) => pnlGrafico.Invalidate();

		contenido.Controls.Add(filtros, 0, 0);
		contenido.Controls.Add(dgvReporte, 0, 1);
		contenido.Controls.Add(pnlGrafico, 0, 2);
		return contenido;
	}

	private void GenerarReporte()
	{
		try
		{
			if (dtpDesde.Value.Date > dtpHasta.Value.Date)
				throw new ArgumentException("La fecha inicial no puede ser posterior a la final.");
			string titulo;
			if (cboReporte.SelectedIndex < CatalogoReportes.Todos.Count)
			{
				// Polimorfismo: se llama a Generar sin saber qué reporte concreto es.
				IReporte reporte = CatalogoReportes.Todos[cboReporte.SelectedIndex];
				_filas = Aplicacion.Gestor.GenerarReporte(reporte, dtpDesde.Value.Date, dtpHasta.Value.Date);
				dgvReporte.Columns["colValor"]!.HeaderText = $"Valor ({reporte.Unidad})";
				titulo = reporte.Titulo;
			}
			else
			{
				_filas = Aplicacion.Gestor.ResumenPorNivelEnServidor(dtpDesde.Value.Date, dtpHasta.Value.Date);
				dgvReporte.Columns["colValor"]!.HeaderText = "Espera promedio (minutos)";
				titulo = "Resumen por nivel (dbo.usp_ResumenPorNivel)";
			}
			dgvReporte.Rows.Clear();
			foreach (var f in _filas) dgvReporte.Rows.Add(f.Categoria, f.Valor, f.Detalle);
			dgvReporte.ClearSelection();
			pnlGrafico.Invalidate();
			Aplicacion.Bitacora?.Info($"Reporte generado: «{titulo}» ({_filas.Count} filas).");
		}
		catch (Exception ex) { Tema.MostrarError(this, ex); }
	}

	/// <summary>Gráfico de barras dibujado con GDI+.</summary>
	private void DibujarGrafico(object? sender, PaintEventArgs e)
	{
		var g = e.Graphics;
		g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
		if (_filas.Count == 0)
		{
			g.DrawString("Sin datos para el periodo seleccionado.", Tema.Normal(10f), Brushes.Gray, 10, 10);
			return;
		}
		double maximo = Math.Max(1, _filas.Max(f => f.Valor));
		int alto = pnlGrafico.Height - 60, ancho = pnlGrafico.Width - 40;
		if (alto <= 20 || ancho <= 0) return;
		float paso = (float)ancho / _filas.Count;
		float anchoBarra = Math.Clamp(paso - 12, 1, 110);
		int intervaloRotulos = Math.Max(1, (int)Math.Ceiling(70 / paso));
		using var pincel = new SolidBrush(Tema.Primario);
		using var fuente = Tema.Normal(8.5f);
		using var fuenteValor = Tema.Negrita(9f);
		using var centrado = new StringFormat
		{
			Alignment = StringAlignment.Center,
			Trimming = StringTrimming.EllipsisCharacter
		};
		float altoValor = fuenteValor.GetHeight(g) + 3;
		for (int i = 0; i < _filas.Count; i++)
		{
			float centro = 20 + (i + 0.5f) * paso;
			float alturaBarra = (float)(_filas[i].Valor / maximo * (alto - 20));
			float y = 10 + alto - alturaBarra;
			g.FillRectangle(pincel, centro - anchoBarra / 2, y, anchoBarra, alturaBarra);

			string valor = _filas[i].Valor.ToString("0.#");
			if (g.MeasureString(valor, fuenteValor).Width <= paso)
			{
				var espacioValor = new RectangleF(centro - paso / 2, y - altoValor, paso, altoValor);
				g.DrawString(valor, fuenteValor, Brushes.Black, espacioValor, centrado);
			}

			if (i % intervaloRotulos == 0)
			{
				float mitadRotulo = Math.Min(paso * intervaloRotulos / 2,
					Math.Min(centro, pnlGrafico.Width - centro));
				var espacioRotulo = new RectangleF(centro - mitadRotulo, alto + 14, mitadRotulo * 2, 40);
				g.DrawString(_filas[i].Categoria, fuente, Brushes.DimGray, espacioRotulo, centrado);
			}
		}
	}

	// ---------------------------------------------------------------- Simulación
	private TabPage CrearPestanaSimulacion()
	{
		var p = new TabPage("Simulación") { BackColor = Color.White };
		p.Controls.Add(Tema.CrearEtiqueta("Turnos por replicación", 16, 24));
		numTurnos.Bounds = new Rectangle(170, 20, 100, 28);
		numTurnos.Minimum = 100;
		numTurnos.Maximum = 100_000;
		numTurnos.Increment = 100;
		numTurnos.Value = 1000;
		p.Controls.Add(Tema.CrearEtiqueta("Replicaciones", 290, 24));
		numReplicaciones.Bounds = new Rectangle(390, 20, 80, 28);
		numReplicaciones.Minimum = 1;
		numReplicaciones.Maximum = 500;
		numReplicaciones.Value = 20;
		btnSimular.Click += async (_, _) => await SimularAsync();
		barra.Bounds = new Rectangle(800, 24, 230, 24);

		lblSimulacion.Location = new Point(16, 66);
		lblSimulacion.AutoSize = true;
		lblSimulacion.MaximumSize = new Size(1010, 0);
		lblSimulacion.Text = "Compara la fila por orden de llegada (FIFO) con la cola de prioridad (montículo) usando los mismos " +
							 "pacientes sintéticos. Las replicaciones se ejecutan en paralelo (Parallel.For) en un hilo de fondo " +
							 "para que la ventana siga respondiendo.";

		dgvSimulacion.Bounds = new Rectangle(16, 150, 1016, 250);
		dgvSimulacion.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		Tema.EstilizarTabla(dgvSimulacion);
		dgvSimulacion.Columns.Add("colNivel", "Nivel");
		dgvSimulacion.Columns.Add("colFifo", "Espera promedio FIFO (min)");
		dgvSimulacion.Columns.Add("colPrio", "Espera promedio con prioridad (min)");
		dgvSimulacion.Columns.Add("colRed", "Reducción");

		p.Controls.AddRange(new Control[] { numTurnos, numReplicaciones, btnSimular, barra, lblSimulacion, dgvSimulacion });
		return p;
	}

	/// <summary>
	/// async/await: la simulación corre en un hilo del grupo de hilos (Task.Run) y dentro de ella
	/// las replicaciones se reparten entre varios núcleos con Parallel.For. La interfaz no se congela.
	/// </summary>
	private async Task SimularAsync()
	{
		btnSimular.Enabled = false;
		barra.Value = 0;
		var parametros = new ParametrosSimulacion(Turnos: (int)numTurnos.Value, Replicaciones: (int)numReplicaciones.Value);
		var progreso = new Progress<int>(v => barra.Value = Math.Min(100, v));
		try
		{
			Aplicacion.Bitacora?.Info($"Simulación iniciada: {parametros.Turnos} turnos × {parametros.Replicaciones} replicaciones.");
			var r = await Task.Run(() => new SimuladorFila().Ejecutar(parametros, progreso));

			dgvSimulacion.Rows.Clear();
			foreach (var n in r.PorNivel)
			{
				int i = dgvSimulacion.Rows.Add($"{(int)n.Nivel} · {n.Nivel}", n.EsperaFifoMin, n.EsperaPrioridadMin,
					$"{n.ReduccionPorcentaje:0.#} %");
				Tema.ColorearCelda(dgvSimulacion.Rows[i].Cells["colNivel"], n.Nivel);
			}
			dgvSimulacion.ClearSelection();
			bool cumple = r.ReduccionNiveles1y2 >= 50 && r.MicrosegundosInsertar < 1000 && r.MicrosegundosExtraer < 1000;
			lblSimulacion.ForeColor = cumple ? Tema.Exito : Tema.Peligro;
			lblSimulacion.Text =
				$"Reducción de la espera en los niveles 1 y 2: {r.ReduccionNiveles1y2} % (meta ≥ 50 %).\n" +
				$"Tiempo promedio por operación del montículo: inserción {r.MicrosegundosInsertar} µs, extracción {r.MicrosegundosExtraer} µs (meta < 1 ms = 1000 µs).\n" +
				$"{r.Parametros.Replicaciones} replicaciones de {r.Parametros.Turnos} turnos en {r.HilosUtilizados} hilos · {r.DuracionMs} ms · " +
				$"{Environment.ProcessorCount} núcleos lógicos. Objetivo específico c): {(cumple ? "CUMPLIDO" : "NO CUMPLIDO")}.";
			Aplicacion.Bitacora?.Info($"Simulación terminada: reducción niveles 1-2 = {r.ReduccionNiveles1y2} %, inserción {r.MicrosegundosInsertar} µs, " +
									  $"extracción {r.MicrosegundosExtraer} µs, {r.HilosUtilizados} hilos, {r.DuracionMs} ms.");
		}
		catch (Exception ex) { Tema.MostrarError(this, ex); }
		finally { btnSimular.Enabled = true; }
	}

	// ---------------------------------------------------------------- Bitácora
	private TabPage CrearPestanaBitacora()
	{
		var p = new TabPage("Bitácora") { BackColor = Color.White };
		lstBitacora.Bounds = new Rectangle(16, 16, 1016, 500);
		lstBitacora.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
		lstBitacora.Font = new Font("Consolas", 9f);
		lstBitacora.HorizontalScrollbar = true;
		foreach (var e in Aplicacion.Bitacora?.Recientes() ?? Array.Empty<EntradaBitacora>()) lstBitacora.Items.Add(e.ToString());
		var btnCarpeta = Tema.CrearBoton("Abrir carpeta de bitácoras", new Rectangle(16, 528, 260, 36), Tema.PrimarioClaro, Tema.Primario);
		btnCarpeta.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
		btnCarpeta.Click += (_, _) =>
		{
			try { Process.Start(new ProcessStartInfo("explorer.exe", Aplicacion.CarpetaBitacoras) { UseShellExecute = true }); }
			catch (Exception ex) { Tema.MostrarError(this, ex); }
		};
		p.Controls.AddRange(new Control[] { lstBitacora, btnCarpeta });
		return p;
	}

	private void AlRegistrarBitacora(object? sender, EntradaBitacora e) => Tema.EnHiloUI(this, () =>
	{
		lstBitacora.Items.Add(e.ToString());
		lstBitacora.TopIndex = Math.Max(0, lstBitacora.Items.Count - 1);
	});

	// ---------------------------------------------------------------- Estructuras
	private TabPage CrearPestanaEstructuras()
	{
		var p = new TabPage("Estructuras y reglas") { BackColor = Color.White };
		txtEstructuras.Bounds = new Rectangle(16, 16, 1016, 548);
		txtEstructuras.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
		txtEstructuras.Multiline = true;
		txtEstructuras.ReadOnly = true;
		txtEstructuras.ScrollBars = ScrollBars.Both;
		txtEstructuras.WordWrap = false;
		txtEstructuras.Font = new Font("Consolas", 9.5f);
		txtEstructuras.BackColor = Color.White;
		p.Controls.Add(txtEstructuras);
		return p;
	}

	private void ActualizarEstructuras()
	{
		var g = Aplicacion.Gestor;
		var sb = new System.Text.StringBuilder();
		sb.AppendLine("MONTÍCULO BINARIO DE MÍNIMOS (fila de espera) — arreglo interno");
		sb.AppendLine("Hijos del nodo i en 2i+1 y 2i+2. Válido: " + (g.FilaEsValida() ? "sí" : "NO"));
		var vista = g.VistaMonticulo();
		if (vista.Count == 0) sb.AppendLine("  (vacío)");
		foreach (var linea in vista) sb.AppendLine("  " + linea);
		sb.AppendLine();
		sb.AppendLine($"ÁRBOL AVL (índice de pacientes por cédula): {g.PacientesIndexados} nodos, altura {g.AlturaIndice} " +
					  $"(máximo teórico ≈ {1.44 * Math.Log2(Math.Max(2, g.PacientesIndexados) + 2):0.0}).");
		sb.AppendLine();
		sb.AppendLine("REGLAS LÓGICAS DE ATENCIÓN PREFERENCIAL (cláusulas de Horn)");
		foreach (var linea in ReglasAtencion.Programa.Split('\n')) sb.AppendLine("  " + linea.TrimEnd());
		sb.AppendLine();
		sb.AppendLine("TABLA DE SÍMBOLOS DEL LENGUAJE DE FILTROS DEL HISTORIAL");
		foreach (var campo in AnalizadorConsultas.CamposDisponibles) sb.AppendLine("  " + campo);
		sb.AppendLine();
		sb.AppendLine($"MEMORIA: {GC.GetTotalMemory(false) / 1024.0 / 1024.0:0.0} MB en el montículo administrado · " +
					  $"recolecciones gen0 = {GC.CollectionCount(0)}, gen1 = {GC.CollectionCount(1)}, gen2 = {GC.CollectionCount(2)}");
		txtEstructuras.Text = sb.ToString();
	}
}
