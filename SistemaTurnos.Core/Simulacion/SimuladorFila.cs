using System.Collections.Concurrent;
using System.Diagnostics;
using SistemaTurnos.Core.Estructuras;
using SistemaTurnos.Core.Modelo;

namespace SistemaTurnos.Core.Simulacion;

public record ParametrosSimulacion(
	int Turnos = 1000,
	int Consultorios = 4,
	int Replicaciones = 20,
	double UtilizacionObjetivo = 0.92,
	int ServicioMinimoMin = 8,
	int ServicioMaximoMin = 20,
	int SemillaBase = 2026);

public record ResultadoNivel(NivelPrioridad Nivel, double EsperaFifoMin, double EsperaPrioridadMin)
{
	public double ReduccionPorcentaje => EsperaFifoMin <= 0 ? 0 : (EsperaFifoMin - EsperaPrioridadMin) * 100.0 / EsperaFifoMin;
}

public record ResultadoSimulacion(
	ParametrosSimulacion Parametros,
	IReadOnlyList<ResultadoNivel> PorNivel,
	double ReduccionNiveles1y2,
	double MicrosegundosInsertar,
	double MicrosegundosExtraer,
	long DuracionMs,
	int HilosUtilizados);

/// <summary>
/// Simulación de eventos discretos que compara la fila por orden de llegada (FIFO)
/// con la cola de prioridad (montículo). Cada replicación usa una semilla distinta y
/// las replicaciones se ejecutan EN PARALELO con Parallel.For (procesamiento en paralelo).
/// Responde al objetivo específico c) del proyecto.
/// </summary>
public class SimuladorFila
{
	private readonly record struct PacienteSim(int Id, NivelPrioridad Nivel, bool Preferencial, double Llegada, double Servicio);

	private sealed class ComparadorSim : IComparer<PacienteSim>
	{
		public int Compare(PacienteSim a, PacienteSim b)
		{
			int c = ((int)a.Nivel).CompareTo((int)b.Nivel);
			if (c != 0) return c;
			c = b.Preferencial.CompareTo(a.Preferencial);
			if (c != 0) return c;
			c = a.Llegada.CompareTo(b.Llegada);
			return c != 0 ? c : a.Id.CompareTo(b.Id);
		}
	}

	private record Replicacion(Dictionary<NivelPrioridad, double> EsperaFifo, Dictionary<NivelPrioridad, double> EsperaPrioridad,
							   double TicksInsertar, double TicksExtraer, int Operaciones);

	public ResultadoSimulacion Ejecutar(ParametrosSimulacion p, IProgress<int>? progreso = null)
	{
		var reloj = Stopwatch.StartNew();
		var resultados = new ConcurrentBag<Replicacion>();
		var hilos = new ConcurrentDictionary<int, byte>();
		int completadas = 0;

		Parallel.For(0, p.Replicaciones, r =>
		{
			hilos.TryAdd(Environment.CurrentManagedThreadId, 0);
			var pacientes = Generar(p, p.SemillaBase + r);
			var fifo = Simular(pacientes, p.Consultorios, usarPrioridad: false, out _, out _);
			var prio = Simular(pacientes, p.Consultorios, usarPrioridad: true, out double tIns, out double tExt);
			resultados.Add(new Replicacion(fifo, prio, tIns, tExt, pacientes.Count));
			progreso?.Report(Interlocked.Increment(ref completadas) * 100 / p.Replicaciones);
		});

		reloj.Stop();
		var lista = resultados.ToList();
		var porNivel = Enum.GetValues<NivelPrioridad>()
			.Select(n => new ResultadoNivel(n,
				Math.Round(lista.Average(x => x.EsperaFifo.GetValueOrDefault(n)), 1),
				Math.Round(lista.Average(x => x.EsperaPrioridad.GetValueOrDefault(n)), 1)))
			.ToList();

		double fifo12 = porNivel.Where(x => x.Nivel <= NivelPrioridad.Rojo).Average(x => x.EsperaFifoMin);
		double prio12 = porNivel.Where(x => x.Nivel <= NivelPrioridad.Rojo).Average(x => x.EsperaPrioridadMin);
		double ticksPorUs = Stopwatch.Frequency / 1_000_000.0;

		return new ResultadoSimulacion(p, porNivel,
			fifo12 <= 0 ? 0 : Math.Round((fifo12 - prio12) * 100 / fifo12, 1),
			Math.Round(lista.Average(x => x.TicksInsertar) / ticksPorUs, 3),
			Math.Round(lista.Average(x => x.TicksExtraer) / ticksPorUs, 3),
			reloj.ElapsedMilliseconds,
			hilos.Count);
	}

	private static List<PacienteSim> Generar(ParametrosSimulacion p, int semilla)
	{
		var rnd = new Random(semilla);
		double servicioMedio = (p.ServicioMinimoMin + p.ServicioMaximoMin) / 2.0;
		double tasaLlegada = p.UtilizacionObjetivo * p.Consultorios / servicioMedio; // pacientes por minuto
		var lista = new List<PacienteSim>(p.Turnos);
		double t = 0;
		for (int i = 0; i < p.Turnos; i++)
		{
			t += -Math.Log(1 - rnd.NextDouble()) / tasaLlegada; // llegadas de Poisson
			double u = rnd.NextDouble();
			var nivel = u < 0.03 ? NivelPrioridad.Azul : u < 0.15 ? NivelPrioridad.Rojo :
						u < 0.50 ? NivelPrioridad.Amarillo : u < 0.85 ? NivelPrioridad.Verde : NivelPrioridad.Blanco;
			double servicio = p.ServicioMinimoMin + rnd.NextDouble() * (p.ServicioMaximoMin - p.ServicioMinimoMin);
			lista.Add(new PacienteSim(i, nivel, rnd.NextDouble() < 0.15, t, servicio));
		}
		return lista;
	}

	private static Dictionary<NivelPrioridad, double> Simular(List<PacienteSim> llegadas, int consultorios,
		bool usarPrioridad, out double ticksInsertar, out double ticksExtraer)
	{
		var monticulo = new MonticuloMinimo<PacienteSim>(new ComparadorSim());
		var fifo = new Queue<PacienteSim>();
		var libreEn = new double[consultorios];
		var esperas = Enum.GetValues<NivelPrioridad>().ToDictionary(n => n, _ => new List<double>());
		long tIns = 0, tExt = 0, nIns = 0, nExt = 0;
		var sw = new Stopwatch();
		int i = 0;
		double ahora = 0;

		int Cantidad() => usarPrioridad ? monticulo.Count : fifo.Count;

		while (true)
		{
			int k = 0;
			for (int c = 1; c < consultorios; c++) if (libreEn[c] < libreEn[k]) k = c;
			double inicio = Math.Max(libreEn[k], ahora);

			if (Cantidad() > 0 && (i >= llegadas.Count || inicio <= llegadas[i].Llegada))
			{
				PacienteSim x;
				if (usarPrioridad)
				{
					sw.Restart(); x = monticulo.ExtraerMinimo(); sw.Stop();
					tExt += sw.ElapsedTicks; nExt++;
				}
				else x = fifo.Dequeue();
				esperas[x.Nivel].Add(inicio - x.Llegada);
				libreEn[k] = inicio + x.Servicio;
				ahora = inicio;
			}
			else if (i < llegadas.Count)
			{
				var llegada = llegadas[i++];
				ahora = llegada.Llegada;
				if (usarPrioridad)
				{
					sw.Restart(); monticulo.Insertar(llegada); sw.Stop();
					tIns += sw.ElapsedTicks; nIns++;
				}
				else fifo.Enqueue(llegada);
			}
			else break;
		}

		ticksInsertar = nIns == 0 ? 0 : (double)tIns / nIns;
		ticksExtraer = nExt == 0 ? 0 : (double)tExt / nExt;
		return esperas.ToDictionary(kv => kv.Key, kv => kv.Value.Count == 0 ? 0 : kv.Value.Average());
	}
}

