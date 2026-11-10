using System.Runtime.CompilerServices;
using SistemaTurnos.Core.Datos;
using SistemaTurnos.Core.Lexico;
using SistemaTurnos.Core.Logica;
using SistemaTurnos.Core.Modelo;
using SistemaTurnos.Core.Reportes;
using SistemaTurnos.Core.Servicios;
using SistemaTurnos.Core.Simulacion;

namespace SistemaTurnos.Pruebas;

/// <summary>Reloj controlable para probar los tiempos de gracia sin esperar minutos reales.</summary>
internal sealed class RelojSimulado
{
	public DateTime Ahora { get; set; } = new DateTime(2026, 11, 8, 8, 0, 0);
	public void Avanzar(TimeSpan t) => Ahora += t;
}

public static class PruebasSistema
{
	private static (GestorTurnos Gestor, RelojSimulado Reloj) CrearGestor()
	{
		var reloj = new RelojSimulado();
		var g = new GestorTurnos(new RepositorioPacientesMemoria(), new RepositorioTurnosMemoria(),
								 new RepositorioCatalogosMemoria(), new Bitacora(), () => reloj.Ahora);
		g.Inicializar();
		return (g, reloj);
	}

	[Prueba("Cédula: valida el formato 0-0000-0000 y normaliza 9 dígitos", "RF01")]
	public static void Cedulas()
	{
		Afirmar.Verdadero(Cedula.TryParse("5-0412-0876", out _), "Rechazó una cédula válida.");
		Afirmar.Igual("1-1203-0456", Cedula.Parse("112030456").Valor);
		foreach (var mala in new[] { "", "0-1234-5678", "5-412-0876", "abc", "5-0412-08766" })
			Afirmar.Verdadero(!Cedula.TryParse(mala, out _), $"Aceptó la cédula inválida '{mala}'.");
	}

	[Prueba("Turno: rechaza transiciones de estado inválidas (encapsulamiento)", "RF05")]
	public static void TransicionesInvalidas()
	{
		var (g, _) = CrearGestor();
		Afirmar.Lanza<InvalidOperationException>(() => g.Finalizar(1), "Finalizar sin paciente en atención");
		Afirmar.Lanza<InvalidOperationException>(() => g.MarcarAusente(1), "Marcar ausente sin paciente");
	}

	[Prueba("Reglas lógicas: deducen la atención preferencial (adulto mayor, embarazo, discapacidad)", "RF03")]
	public static string ReglasPreferenciales()
	{
		var r = new ReglasAtencion();
		var mayor = r.Evaluar(68, false, false);
		var embarazo = r.Evaluar(29, true, false);
		var ambos = r.Evaluar(70, false, true);
		var ninguno = r.Evaluar(30, false, false);
		Afirmar.Verdadero(mayor.EsPreferencial && mayor.Razones.Single().Contains("adulto mayor"), "68 años debe ser preferencial.");
		Afirmar.Verdadero(embarazo.EsPreferencial && embarazo.Razones.Single() == "embarazo", "Embarazo debe ser preferencial.");
		Afirmar.Igual(2, ambos.Razones.Count, "70 años con discapacidad:");
		Afirmar.Verdadero(!ninguno.EsPreferencial, "30 años sin condiciones no es preferencial.");
		return $"68 años → {string.Join(", ", mayor.Razones)} · 70 años + discapacidad → {string.Join(", ", ambos.Razones)}";
	}

	[Prueba("Motor lógico: unificación y vuelta atrás con una regla recursiva")]
	public static string MotorLogicoRecursivo()
	{
		var m = new MotorLogico();
		m.Cargar("""
            padre(ana, beto). padre(beto, carla). padre(carla, dani).
            ancestro(X, Y) :- padre(X, Y).
            ancestro(X, Y) :- padre(X, Z), ancestro(Z, Y).
            """);
		var descendientes = m.Consultar("ancestro(ana, D)")
			.Select(s => MotorLogico.Resolver(new Variable("D"), s).ToString()).ToList();
		Afirmar.Igual("beto,carla,dani", string.Join(",", descendientes));
		return "ancestro(ana, D) → D = beto; carla; dani";
	}

	[Prueba("Análisis léxico: reconoce identificadores, números, fechas, textos y palabras", "RF08")]
	public static string Lexico()
	{
		var tokens = new AnalizadorLexico("nivel:2 motivo:\"crisis asmática\" desde:2026-01-01 fiebre").Analizar();
		string tipos = string.Join(" ", tokens.Select(t => t.Tipo));
		Afirmar.Igual("Identificador DosPuntos Numero Identificador DosPuntos Texto Identificador DosPuntos Fecha Palabra Fin", tipos);
		return string.Join(" ", tokens.Take(tokens.Count - 1).Select(t => $"{t.Tipo}({t.Lexema})"));
	}

	[Prueba("Análisis sintáctico y semántico: detecta errores con mensajes claros", "RF08")]
	public static string ErroresConsulta()
	{
		var a = new AnalizadorConsultas();
		var r1 = a.Analizar("doctor:Campos");
		var r2 = a.Analizar("nivel:9");
		var r3 = a.Analizar("desde:2026-02-30");
		var r4 = a.Analizar("motivo:\"sin cerrar");
		var r5 = a.Analizar("nivel:fiebre");
		Afirmar.Verdadero(r1.Errores.Single().StartsWith("Error semántico"), "Campo inexistente no detectado.");
		Afirmar.Verdadero(r2.Errores.Single().Contains("entre 1 y 5"), "Nivel fuera de rango no detectado.");
		Afirmar.Verdadero(r3.Errores.Single().Contains("no existe"), "Fecha inexistente no detectada.");
		Afirmar.Verdadero(r4.Errores.Single().StartsWith("Error léxico"), "Comillas sin cerrar no detectadas.");
		Afirmar.Verdadero(r5.Errores.Single().Contains("se esperaba un número"), "Tipo incorrecto no detectado.");
		return r1.Errores[0];
	}

	[Prueba("Filtro del historial: combina condiciones con composición de funciones", "RF08")]
	public static string FiltroHistorial()
	{
		var datos = new[]
		{
			new AtencionHistorial(new DateTime(2025, 11, 3), "Fiebre y tos", NivelPrioridad.Amarillo, "Dra. Sofía Jiménez", 22),
			new AtencionHistorial(new DateTime(2026, 1, 20), "Consulta general", NivelPrioridad.Blanco, "Dra. Laura Campos", 48),
			new AtencionHistorial(new DateTime(2026, 6, 15), "Crisis asmática", NivelPrioridad.Rojo, "Dr. Andrés Salas", 6),
			new AtencionHistorial(new DateTime(2026, 9, 2), "Control de presión arterial", NivelPrioridad.Verde, "Dra. Laura Campos", 35),
		};
		var r = new AnalizadorConsultas().Analizar("medico:campos desde:2026-02-01");
		Afirmar.Verdadero(r.EsValida, string.Join("; ", r.Errores));
		var resultado = datos.Where(r.Predicado).ToList();
		Afirmar.Igual(1, resultado.Count, "Resultados:");
		Afirmar.Igual("Control de presión arterial", resultado[0].Motivo);
		var r2 = new AnalizadorConsultas().Analizar("asmatica");
		Afirmar.Igual(1, datos.Count(r2.Predicado), "Búsqueda libre sin tilde:");
		return "medico:campos desde:2026-02-01 → «Control de presión arterial»";
	}

	[Prueba("Flujo completo: asignar, llamar por prioridad, ausencia, reingreso y finalización", "RF03–RF07")]
	public static string FlujoCompleto()
	{
		var (g, reloj) = CrearGestor();
		var ana = g.RegistrarPaciente(Cedula.Parse("1-1203-0456"), "Ana Méndez Quesada", new DateTime(1972, 3, 14), "", false, false);
		var jose = g.RegistrarPaciente(Cedula.Parse("2-0567-0912"), "José Solís Brenes", new DateTime(1979, 7, 2), "", false, false);
		var maria = g.RegistrarPaciente(Cedula.Parse("5-0412-0876"), "María Rojas Díaz", new DateTime(1958, 5, 20), "", false, false);

		var tJose = g.AsignarTurno(jose, NivelPrioridad.Verde, "Control");
		reloj.Avanzar(TimeSpan.FromMinutes(2));
		var tMaria = g.AsignarTurno(maria, NivelPrioridad.Amarillo, "Mareo");
		reloj.Avanzar(TimeSpan.FromMinutes(2));
		var tAna = g.AsignarTurno(ana, NivelPrioridad.Azul, "Dificultad respiratoria");
		Afirmar.Verdadero(tMaria.Preferencial, "María (68 años) debía ser preferencial.");
		Afirmar.Igual("T-03,T-02,T-01", string.Join(",", g.FilaOrdenada().Select(t => t.Codigo)), "Fila:");

		var primero = g.LlamarSiguiente(1)!;
		Afirmar.Igual(tAna, primero, "Primer llamado:");
		g.Finalizar(1);

		var segundo = g.LlamarSiguiente(1)!;
		Afirmar.Igual(tMaria, segundo, "Segundo llamado:");
		g.MarcarAusente(1);
		Afirmar.Igual(EstadoTurno.Ausente, tMaria.Estado);
		Afirmar.Igual(0, g.ProcesarReingresos(), "Reingresos antes de la gracia:");
		reloj.Avanzar(g.TiempoGracia + TimeSpan.FromSeconds(1));
		Afirmar.Igual(1, g.ProcesarReingresos(), "Reingresos después de la gracia:");
		Afirmar.Igual(tMaria, g.LlamarSiguiente(2), "María vuelve a ser la primera:");
		Afirmar.Igual(2, tMaria.Llamados);
		g.Finalizar(2);
		Afirmar.Verdadero(g.FilaEsValida(), "La fila perdió la propiedad de montículo.");
		return "Orden de llamado: T-03 (nivel 1) → T-02 (nivel 3, preferencial; ausente y reingresó) → T-01 en espera.";
	}

	[Prueba("Tres llamados sin respuesta cancelan el turno", "RF06")]
	public static void TresAusencias()
	{
		var (g, reloj) = CrearGestor();
		var p = g.RegistrarPaciente(Cedula.Parse("1-1111-1111"), "Prueba Uno", new DateTime(1990, 1, 1), "", false, false);
		var t = g.AsignarTurno(p, NivelPrioridad.Verde, "x");
		for (int i = 1; i <= 3; i++)
		{
			Afirmar.Igual(t, g.LlamarSiguiente(1), $"Llamado {i}:");
			g.MarcarAusente(1);
			reloj.Avanzar(g.TiempoGracia + TimeSpan.FromSeconds(1));
			g.ProcesarReingresos();
		}
		Afirmar.Igual(EstadoTurno.Cancelado, t.Estado);
		Afirmar.Igual(0, g.FilaOrdenada().Count, "La fila debía quedar vacía:");
	}

	[Prueba("Un paciente no puede tener dos turnos activos", "RF03")]
	public static void TurnoDuplicado()
	{
		var (g, _) = CrearGestor();
		var p = g.RegistrarPaciente(Cedula.Parse("1-1111-1111"), "Prueba Uno", new DateTime(1990, 1, 1), "", false, false);
		g.AsignarTurno(p, NivelPrioridad.Verde, "x");
		Afirmar.Lanza<InvalidOperationException>(() => g.AsignarTurno(p, NivelPrioridad.Rojo, "y"), "Segundo turno");
	}

	[Prueba("Sobrecarga: BuscarPaciente(Cedula) usa el árbol AVL y BuscarPaciente(string) busca por nombre", "RF02")]
	public static string Sobrecarga()
	{
		var (g, _) = CrearGestor();
		g.RegistrarPaciente(Cedula.Parse("1-1203-0456"), "Ana Méndez Quesada", new DateTime(1972, 3, 14), "", false, false);
		g.RegistrarPaciente(Cedula.Parse("5-0412-0876"), "María Fernanda Rojas Díaz", new DateTime(1958, 5, 20), "", false, false);
		Afirmar.Igual("Ana Méndez Quesada", g.BuscarPaciente(Cedula.Parse("1-1203-0456"))?.Nombre);
		Afirmar.Igual("María Fernanda Rojas Díaz", g.BuscarPaciente("rojas").Single().Nombre);
		Afirmar.Verdadero(g.BuscarPaciente(Cedula.Parse("9-9999-9999")) is null, "Encontró una cédula inexistente.");
		return "Ambas versiones del método resuelven correctamente según el tipo del argumento.";
	}

	[Prueba("Alias: dos variables referencian el mismo objeto Turno")]
	public static string Alias()
	{
		var (g, _) = CrearGestor();
		var p = g.RegistrarPaciente(Cedula.Parse("1-1111-1111"), "Prueba Uno", new DateTime(1990, 1, 1), "", false, false);
		Turno asignado = g.AsignarTurno(p, NivelPrioridad.Rojo, "x");
		Turno llamado = g.LlamarSiguiente(1)!;
		Afirmar.Verdadero(ReferenceEquals(asignado, llamado), "No son el mismo objeto.");
		Afirmar.Igual(EstadoTurno.EnAtencion, asignado.Estado, "El cambio hecho por medio de 'llamado' se ve desde 'asignado':");
		Cedula c1 = p.Cedula, c2 = c1; // tipo de valor: se copia, no es alias
		return $"ReferenceEquals(asignado, llamado) = true; la cédula ({c2}) es un tipo de valor y se copia.";
	}

	[Prueba("Recolección de basura: un turno finalizado ya no tiene referencias y el GC lo libera")]
	public static string RecoleccionDeBasura()
	{
		// Repositorio que NO guarda los objetos (como la base de datos real, que guarda filas, no objetos).
		// Con RepositorioTurnosMemoria la prueba falla, porque su lista mantiene viva la referencia.
		var g = new GestorTurnos(new RepositorioPacientesMemoria(), new RepositorioTurnosSinRetencion(),
								 new RepositorioCatalogosMemoria(), new Bitacora());
		g.Inicializar();
		var referenciaDebil = CrearYFinalizar(g);
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
		Afirmar.Verdadero(!referenciaDebil.IsAlive, "El turno sigue vivo: alguna estructura conserva una referencia (fuga).");
		return "WeakReference.IsAlive = false después de GC.Collect().";
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static WeakReference CrearYFinalizar(GestorTurnos g)
	{
		var p = g.RegistrarPaciente(Cedula.Parse("1-1111-1111"), "Prueba Uno", new DateTime(1990, 1, 1), "", false, false);
		var t = g.AsignarTurno(p, NivelPrioridad.Rojo, "x");
		var debil = new WeakReference(t);
		g.LlamarSiguiente(1);
		g.Finalizar(1);
		return debil;
	}

	[Prueba("Reportes: calculan la espera promedio por nivel con LINQ sin modificar los datos", "RF10")]
	public static string ReporteEspera()
	{
		var h = new DateTime(2026, 11, 8, 8, 0, 0);
		var datos = new List<RegistroAtencion>
		{
			new("T-01", NivelPrioridad.Rojo, false, h, h.AddMinutes(4), h.AddMinutes(20), EstadoTurno.Finalizado, 1),
			new("T-02", NivelPrioridad.Rojo, false, h, h.AddMinutes(8), h.AddMinutes(30), EstadoTurno.Finalizado, 1),
			new("T-03", NivelPrioridad.Verde, false, h, h.AddMinutes(60), h.AddMinutes(70), EstadoTurno.Finalizado, 2),
			new("T-04", NivelPrioridad.Verde, false, h, null, h.AddMinutes(90), EstadoTurno.Cancelado, 3),
		};
		var filas = new ReporteEsperaPorNivel().Generar(datos);
		Afirmar.Igual(2, filas.Count, "Niveles:");
		Afirmar.Igual(6.0, filas[0].Valor, "Espera nivel 2:");
		Afirmar.Igual(60.0, filas[1].Valor, "Espera nivel 4:");
		Afirmar.Igual(4, datos.Count, "Los datos originales no deben cambiar:");
		var aus = new ReporteAusentismo().Generar(datos);
		Afirmar.Igual(1.0, aus[2].Valor, "Cancelados:");
		return $"{filas[0].Categoria}: {filas[0].Valor} min · {filas[1].Categoria}: {filas[1].Valor} min";
	}

	[Prueba("Simulación paralela de 1 000 turnos: objetivo específico c)", "RF12")]
	public static string Simulacion()
	{
		var r = new SimuladorFila().Ejecutar(new ParametrosSimulacion(Turnos: 1000, Replicaciones: 20));
		Afirmar.Verdadero(r.MicrosegundosInsertar < 1000 && r.MicrosegundosExtraer < 1000, "Las operaciones superan 1 ms.");
		Afirmar.Verdadero(r.ReduccionNiveles1y2 >= 50, $"La reducción fue de {r.ReduccionNiveles1y2} %, menor que 50 %.");
		string niveles = string.Join(" | ", r.PorNivel.Select(n =>
			$"N{(int)n.Nivel}: FIFO {n.EsperaFifoMin} → prioridad {n.EsperaPrioridadMin} min"));
		return $"Reducción niveles 1-2: {r.ReduccionNiveles1y2} %. Inserción {r.MicrosegundosInsertar} µs, extracción " +
			   $"{r.MicrosegundosExtraer} µs. {r.Parametros.Replicaciones} replicaciones en {r.HilosUtilizados} hilos, {r.DuracionMs} ms.\n         → {niveles}";
	}
}

/// <summary>Repositorio de turnos que no conserva referencias a los objetos.</summary>
internal sealed class RepositorioTurnosSinRetencion : IRepositorioTurnos
{
	private int _ultimoId;
	public void Insertar(Turno turno, DateTime fecha) => turno.GetType().GetProperty("Id")!.SetValue(turno, ++_ultimoId);
	public void Actualizar(Turno turno) { }
	public IEnumerable<Turno> ObtenerActivos(Func<int, Paciente?> buscarPaciente) => Array.Empty<Turno>();
	public int CerrarDiasAnteriores(DateTime hoy) => 0;
	public int UltimoNumeroDelDia(DateTime dia) => 0;
	public IEnumerable<AtencionHistorial> ObtenerHistorial(int pacienteId) => Array.Empty<AtencionHistorial>();
	public IEnumerable<AtencionHistorial> ObtenerHistorialGeneral() => Array.Empty<AtencionHistorial>();
	public IEnumerable<RegistroAtencion> ObtenerCerrados(DateTime desde, DateTime hasta) => Array.Empty<RegistroAtencion>();
	public IEnumerable<SistemaTurnos.Core.Reportes.FilaReporte> ResumenPorNivel(DateTime desde, DateTime hasta) => Array.Empty<SistemaTurnos.Core.Reportes.FilaReporte>();
}
