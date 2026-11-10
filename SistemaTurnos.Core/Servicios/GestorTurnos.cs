using System.Diagnostics;
using SistemaTurnos.Core.Datos;
using SistemaTurnos.Core.Estructuras;
using SistemaTurnos.Core.Logica;
using SistemaTurnos.Core.Modelo;
using SistemaTurnos.Core.Reportes;

namespace SistemaTurnos.Core.Servicios;

/// <summary>
/// Servicio central del sistema. Combina las estructuras de datos dinámicas:
///  · MonticuloMinimo&lt;Turno&gt;  → fila de espera ordenada por prioridad.
///  · ArbolAVL&lt;string, Paciente&gt; → índice de pacientes por cédula.
///  · ListaDobleEnlazada&lt;AtencionHistorial&gt; → historial navegable.
/// Publica eventos (paradigma orientado a eventos) para que las pantallas se actualicen solas.
/// Todas las operaciones sobre la fila se protegen con lock porque la pantalla pública,
/// los temporizadores y los consultorios pueden acceder a ella al mismo tiempo.
/// </summary>
public class GestorTurnos
{
	private readonly IRepositorioPacientes _repoPacientes;
	private readonly IRepositorioTurnos _repoTurnos;
	private readonly IRepositorioCatalogos _repoCatalogos;
	private readonly IBitacora _bitacora;
	private readonly Func<DateTime> _reloj;
	private readonly ReglasAtencion _reglas = new();

	private readonly object _candado = new();
	private readonly MonticuloMinimo<Turno> _fila = new(ComparadorTurnos.Instancia);
	private readonly ArbolAVL<string, Paciente> _indicePacientes = new();
	private readonly Dictionary<int, Turno> _enAtencion = new();   // consultorioId → turno
	private readonly List<Turno> _ausentes = new();
	private List<Consultorio> _consultorios = new();
	private long _secuencia;
	private int _ultimoNumero;
	private DateTime _diaActual;

	public TimeSpan TiempoGracia { get; set; } = TimeSpan.FromMinutes(1);
	public Turno? UltimoLlamado { get; private set; }

	/// <summary>Se dispara cada vez que cambia la fila (asignación, llamado, ausencia, reingreso).</summary>
	public event EventHandler? FilaCambiada;
	/// <summary>Se dispara cuando un consultorio llama a un paciente.</summary>
	public event EventHandler<TurnoEventArgs>? TurnoLlamado;

	public GestorTurnos(IRepositorioPacientes repoPacientes, IRepositorioTurnos repoTurnos,
						IRepositorioCatalogos repoCatalogos, IBitacora bitacora, Func<DateTime>? reloj = null)
	{
		_repoPacientes = repoPacientes;
		_repoTurnos = repoTurnos;
		_repoCatalogos = repoCatalogos;
		_bitacora = bitacora;
		_reloj = reloj ?? (() => DateTime.Now);
	}

	public IReadOnlyList<Consultorio> Consultorios => _consultorios;
	public int PacientesIndexados { get { lock (_candado) return _indicePacientes.Count; } }
	public int AlturaIndice { get { lock (_candado) return _indicePacientes.Altura; } }

	/// <summary>Carga los pacientes en el árbol AVL y reconstruye la fila desde la base de datos.</summary>
	public void Inicializar()
	{
		var sw = Stopwatch.StartNew();
		DateTime ahora = _reloj();
		lock (_candado)
		{
			_consultorios = _repoCatalogos.ObtenerConsultorios().ToList();
			foreach (var p in _repoPacientes.ObtenerTodos())
				_indicePacientes.Insertar(p.Cedula.Valor, p);

			int cerrados = _repoTurnos.CerrarDiasAnteriores(ahora);
			if (cerrados > 0) _bitacora.Advertencia($"Cierre de día: {cerrados} turno(s) de días anteriores quedaron cancelados.");

			var porId = _indicePacientes.EnOrden().ToDictionary(p => p.Id);
			foreach (var t in _repoTurnos.ObtenerActivos(id => porId.GetValueOrDefault(id)))
			{
				t.Secuencia = ++_secuencia;
				switch (t.Estado)
				{
					case EstadoTurno.EnEspera: _fila.Insertar(t); break;
					case EstadoTurno.EnAtencion when t.ConsultorioId is int c: _enAtencion[c] = t; break;
					case EstadoTurno.Ausente: _ausentes.Add(t); break;
				}
			}
			_diaActual = ahora.Date;
			_ultimoNumero = _repoTurnos.UltimoNumeroDelDia(ahora);
		}
		sw.Stop();
		_bitacora.Info($"Sistema iniciado: {_indicePacientes.Count} pacientes en el árbol AVL (altura {_indicePacientes.Altura}), " +
					   $"{_fila.Count} turnos en espera, {_ausentes.Count} ausentes. Carga: {sw.ElapsedMilliseconds} ms.");
	}

	// ------------------------------------------------------------------ Pacientes

	/// <summary>Búsqueda por cédula en el árbol AVL: O(log n). (Sobrecarga 1)</summary>
	public Paciente? BuscarPaciente(Cedula cedula)
	{
		lock (_candado)
			return _indicePacientes.TryBuscar(cedula.Valor, out var p) ? p : null;
	}

	/// <summary>Búsqueda por nombre con LINQ: O(n). (Sobrecarga 2: mismo nombre, otro tipo de parámetro)</summary>
	public IReadOnlyList<Paciente> BuscarPaciente(string textoNombre)
	{
		lock (_candado)
			return _indicePacientes.EnOrden()
				.Where(p => p.Nombre.Contains(textoNombre.Trim(), StringComparison.CurrentCultureIgnoreCase))
				.OrderBy(p => p.Nombre)
				.ToList();
	}

	public Paciente RegistrarPaciente(Cedula cedula, string nombre, DateTime fechaNacimiento, string telefono,
									  bool esEmbarazada, bool tieneDiscapacidad)
	{
		var existente = BuscarPaciente(cedula);
		if (existente is not null)
		{
			existente.Actualizar(nombre, fechaNacimiento, telefono, esEmbarazada, tieneDiscapacidad);
			_repoPacientes.Actualizar(existente);
			_bitacora.Info($"Paciente {cedula} actualizado.");
			return existente;
		}
		var nuevo = new Paciente(cedula, nombre, fechaNacimiento, telefono, esEmbarazada, tieneDiscapacidad);
		_repoPacientes.Insertar(nuevo);
		lock (_candado) _indicePacientes.Insertar(cedula.Valor, nuevo);
		_bitacora.Info($"Paciente {cedula} registrado (árbol AVL: {_indicePacientes.Count} nodos, altura {_indicePacientes.Altura}).");
		return nuevo;
	}

	/// <summary>Consulta las reglas lógicas de atención preferencial.</summary>
	public EvaluacionPreferencial EvaluarPreferencial(Paciente paciente) => _reglas.Evaluar(paciente, _reloj());
	public EvaluacionPreferencial EvaluarPreferencial(int edad, bool embarazo, bool discapacidad) =>
		_reglas.Evaluar(edad, embarazo, discapacidad);

	// ------------------------------------------------------------------ Turnos

	public Turno AsignarTurno(Paciente paciente, NivelPrioridad nivel, string motivo)
	{
		DateTime ahora = _reloj();
		Turno turno;
		long ticks;
		lock (_candado)
		{
			if (_fila.ArregloInterno.Any(t => t.Paciente.Id == paciente.Id) ||
				_enAtencion.Values.Any(t => t.Paciente.Id == paciente.Id) ||
				_ausentes.Any(t => t.Paciente.Id == paciente.Id))
				throw new InvalidOperationException($"{paciente.Nombre} ya tiene un turno activo.");

			if (ahora.Date != _diaActual) { _diaActual = ahora.Date; _ultimoNumero = 0; }
			var evaluacion = _reglas.Evaluar(paciente, ahora);
			turno = new Turno($"T-{++_ultimoNumero:00}", paciente, nivel, motivo, evaluacion.EsPreferencial, ahora)
			{
				Secuencia = ++_secuencia
			};
			_repoTurnos.Insertar(turno, ahora);
			var sw = Stopwatch.StartNew();
			_fila.Insertar(turno);
			sw.Stop();
			ticks = sw.ElapsedTicks;
		}
		_bitacora.Info($"Turno {turno.Codigo} asignado a {paciente.NombreCorto} (nivel {(int)nivel}" +
					   $"{(turno.Preferencial ? ", preferencial" : "")}). Inserción en montículo: {Microsegundos(ticks)} µs; en fila: {_fila.Count}.");
		FilaCambiada?.Invoke(this, EventArgs.Empty);
		return turno;
	}

	/// <summary>Extrae el turno de mayor prioridad y lo asigna al consultorio. O(log n).</summary>
	public Turno? LlamarSiguiente(int consultorioId)
	{
		DateTime ahora = _reloj();
		Turno? turno;
		long ticks;
		lock (_candado)
		{
			if (_enAtencion.ContainsKey(consultorioId))
				throw new InvalidOperationException("El consultorio ya está atendiendo a un paciente. Finalice o marque la ausencia primero.");
			var sw = Stopwatch.StartNew();
			bool hay = _fila.TryExtraerMinimo(out var siguiente);
			sw.Stop();
			ticks = sw.ElapsedTicks;
			if (!hay) return null;
			turno = siguiente;
			turno.Llamar(consultorioId, ahora);
			_enAtencion[consultorioId] = turno;
			_repoTurnos.Actualizar(turno);
			UltimoLlamado = turno;
		}
		_bitacora.Info($"Consultorio {NumeroConsultorio(consultorioId)} llamó a {turno.Codigo} (nivel {(int)turno.Nivel}, " +
					   $"llamado {turno.Llamados} de {Turno.MaximoLlamados}, esperó {turno.MinutosEspera(ahora)} min). " +
					   $"Extracción del montículo: {Microsegundos(ticks)} µs.");
		TurnoLlamado?.Invoke(this, new TurnoEventArgs(turno));
		FilaCambiada?.Invoke(this, EventArgs.Empty);
		return turno;
	}

	public void MarcarAusente(int consultorioId)
	{
		DateTime ahora = _reloj();
		Turno turno;
		bool reingresara;
		lock (_candado)
		{
			turno = TurnoEnAtencionOError(consultorioId);
			reingresara = turno.MarcarAusente(ahora, TiempoGracia);
			_enAtencion.Remove(consultorioId);
			if (reingresara) _ausentes.Add(turno);
			_repoTurnos.Actualizar(turno);
		}
		if (reingresara)
			_bitacora.Advertencia($"{turno.Codigo} no respondió al llamado {turno.Llamados}. Reingresa a las {turno.ReingresoEn:HH:mm:ss}.");
		else
			_bitacora.Advertencia($"{turno.Codigo} cancelado: no respondió a {Turno.MaximoLlamados} llamados.");
		FilaCambiada?.Invoke(this, EventArgs.Empty);
	}

	public void Finalizar(int consultorioId)
	{
		DateTime ahora = _reloj();
		Turno turno;
		lock (_candado)
		{
			turno = TurnoEnAtencionOError(consultorioId);
			turno.Finalizar(ahora);
			_repoTurnos.Actualizar(turno);
			// Se elimina la última referencia en memoria: el objeto queda disponible para el
			// recolector de basura. Sus datos permanecen en la base de datos.
			_enAtencion.Remove(consultorioId);
			if (ReferenceEquals(UltimoLlamado, turno)) UltimoLlamado = null;
		}
		_bitacora.Info($"{turno.Codigo} finalizado en el consultorio {NumeroConsultorio(consultorioId)}. " +
					   "El turno se guardó en la base de datos y se liberó de la memoria.");
		FilaCambiada?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Devuelve a la fila los ausentes cuyo tiempo de gracia terminó. Lo llama un temporizador.</summary>
	public int ProcesarReingresos()
	{
		DateTime ahora = _reloj();
		List<Turno> listos;
		lock (_candado)
		{
			listos = _ausentes.Where(t => t.ReingresoEn <= ahora).ToList();
			foreach (var t in listos)
			{
				t.Reingresar();
				_ausentes.Remove(t);
				_fila.Insertar(t);
				_repoTurnos.Actualizar(t);
			}
		}
		foreach (var t in listos) _bitacora.Info($"{t.Codigo} reingresó a la fila tras su tiempo de gracia.");
		if (listos.Count > 0) FilaCambiada?.Invoke(this, EventArgs.Empty);
		return listos.Count;
	}

	// ------------------------------------------------------------------ Consultas

	public IReadOnlyList<Turno> FilaOrdenada() { lock (_candado) return _fila.ComoListaOrdenada(); }
	public IReadOnlyList<Turno> Ausentes() { lock (_candado) return _ausentes.OrderBy(t => t.ReingresoEn).ToList(); }
	public Turno? EnAtencion(int consultorioId) { lock (_candado) return _enAtencion.GetValueOrDefault(consultorioId); }
	public IReadOnlyList<Turno> TodosEnAtencion() { lock (_candado) return _enAtencion.Values.OrderByDescending(t => t.HoraLlamado).ToList(); }
	public bool FilaEsValida() { lock (_candado) return _fila.EsValido(); }

	/// <summary>Contenido del arreglo interno del montículo, posición por posición (para diagnóstico).</summary>
	public IReadOnlyList<string> VistaMonticulo()
	{
		lock (_candado)
			return _fila.ArregloInterno
				.Select((t, i) => $"[{i}] nivel {(int)t.Nivel}{(t.Preferencial ? "★" : "")} {t.Codigo}" +
								  (i == 0 ? "   ← raíz (siguiente en ser llamado)" : $"   padre: [{(i - 1) / 2}]"))
				.ToList();
	}

	/// <summary>Carga el historial del paciente en una lista doblemente enlazada (orden cronológico).</summary>
	public ListaDobleEnlazada<AtencionHistorial> ObtenerHistorial(Paciente paciente)
	{
		var lista = new ListaDobleEnlazada<AtencionHistorial>();
		foreach (var a in _repoTurnos.ObtenerHistorial(paciente.Id).OrderBy(a => a.Fecha))
			lista.AgregarAlFinal(a);
		return lista;
	}

	public IEnumerable<AtencionHistorial> ObtenerHistorialGeneral() =>
		_repoTurnos.ObtenerHistorialGeneral().OrderBy(a => a.Fecha);

	public IReadOnlyList<FilaReporte> GenerarReporte(IReporte reporte, DateTime desde, DateTime hasta) =>
		reporte.Generar(_repoTurnos.ObtenerCerrados(desde, hasta));

	/// <summary>Mismo resumen, pero calculado en el servidor por un procedimiento almacenado.</summary>
	public IReadOnlyList<FilaReporte> ResumenPorNivelEnServidor(DateTime desde, DateTime hasta) =>
		_repoTurnos.ResumenPorNivel(desde, hasta).ToList();

	private Turno TurnoEnAtencionOError(int consultorioId) =>
		_enAtencion.GetValueOrDefault(consultorioId)
		?? throw new InvalidOperationException("No hay ningún paciente en atención en este consultorio.");

	private int NumeroConsultorio(int id) => _consultorios.FirstOrDefault(c => c.Id == id)?.Numero ?? id;

	private static string Microsegundos(long ticks) =>
		(ticks * 1_000_000.0 / Stopwatch.Frequency).ToString("0.0", System.Globalization.CultureInfo.CurrentCulture);
}
