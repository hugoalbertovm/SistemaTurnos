using SistemaTurnos.Core.Modelo;

namespace SistemaTurnos.Core.Datos;

/// <summary>
/// Datos ficticios para demostrar el sistema: pacientes, el historial de Ana Méndez
/// (el de la Fig. 2c del primer avance) y tres semanas de atenciones para los reportes.
/// </summary>
public static class DatosDemo
{
	public static void Cargar(IRepositorioPacientes pacientes, IRepositorioTurnos turnos,
							  IReadOnlyList<Consultorio> consultorios, DateTime hoy)
	{
		var lista = new List<Paciente>
		{
			new(Cedula.Parse("1-1203-0456"), "Ana Méndez Quesada", new DateTime(1972, 3, 14), "8812-4501"),
			new(Cedula.Parse("2-0567-0912"), "José Solís Brenes", new DateTime(1979, 7, 2), "8890-1123"),
			new(Cedula.Parse("5-0412-0876"), "María Fernanda Rojas Díaz", new DateTime(1958, 5, 20), "8845-2210"),
			new(Cedula.Parse("5-0298-0331"), "Carlos Mora Vega", new DateTime(1991, 1, 9), "8701-5566"),
			new(Cedula.Parse("1-1544-0720"), "Rosa Chaves Ulate", new DateTime(1997, 11, 30), "8366-7788"),
			new(Cedula.Parse("5-0377-0149"), "Luis Vargas Castro", new DateTime(1965, 2, 17), "8455-9031"),
			new(Cedula.Parse("6-0211-0508"), "Daniela Araya Soto", new DateTime(2004, 6, 5), "8620-3344"),
			new(Cedula.Parse("5-0450-0663"), "Sergio Pérez León", new DateTime(1986, 9, 12), "8311-0099"),
			new(Cedula.Parse("5-0433-0912"), "Elena Quirós Badilla", new DateTime(1995, 4, 22), "8722-6610", esEmbarazada: true),
			new(Cedula.Parse("2-0612-0388"), "Jorge Alvarado Núñez", new DateTime(1981, 8, 8), "8599-1470", tieneDiscapacidad: true),
		};
		foreach (var p in lista) pacientes.Insertar(p);

		var codigos = new Dictionary<DateTime, int>();
		string Codigo(DateTime dia)
		{
			codigos[dia.Date] = codigos.GetValueOrDefault(dia.Date) + 1;
			return $"T-{codigos[dia.Date]:00}";
		}

		Consultorio C(int numero) => consultorios.First(c => c.Numero == numero);

		void Registrar(Paciente p, NivelPrioridad nivel, string motivo, DateTime llegada, int esperaMin, int duracionMin,
					   int consultorio, EstadoTurno estado = EstadoTurno.Finalizado, int llamados = 1)
		{
			var t = Turno.Restaurar(0, Codigo(llegada), p, nivel, motivo, p.Edad(llegada) >= 65 || p.EsEmbarazada || p.TieneDiscapacidad,
				llegada, estado, llamados, estado == EstadoTurno.Finalizado ? C(consultorio).Id : null,
				estado == EstadoTurno.Finalizado ? llegada.AddMinutes(esperaMin) : null,
				llegada.AddMinutes(esperaMin + duracionMin), null);
			turnos.Insertar(t, llegada.Date);
			turnos.Actualizar(t);
		}

		// Historial de Ana Méndez (coincide con la pantalla del consultorio del primer avance).
		var ana = lista[0];
		Registrar(ana, NivelPrioridad.Amarillo, "Fiebre y tos", new DateTime(2025, 11, 3, 9, 15, 0), 22, 15, 3);
		Registrar(ana, NivelPrioridad.Blanco, "Consulta general", new DateTime(2026, 1, 20, 10, 5, 0), 48, 12, 1);
		Registrar(ana, NivelPrioridad.Rojo, "Crisis asmática", new DateTime(2026, 6, 15, 8, 40, 0), 6, 30, 2);
		Registrar(ana, NivelPrioridad.Verde, "Control de presión arterial", new DateTime(2026, 9, 2, 14, 10, 0), 35, 10, 1);

		// Tres semanas de atenciones sintéticas para los reportes.
		var rnd = new Random(7);
		string[] motivos = { "Fiebre", "Dolor abdominal", "Control de presión", "Dolor de garganta", "Esguince",
							 "Mareo", "Tos persistente", "Renovación de receta", "Dolor de cabeza", "Control prenatal" };
		for (int d = 21; d >= 1; d--)
		{
			DateTime dia = hoy.Date.AddDays(-d);
			if (dia.DayOfWeek == DayOfWeek.Sunday) continue;
			int cantidad = rnd.Next(6, 11);
			var horas = Enumerable.Range(0, cantidad).Select(_ => rnd.Next(7 * 60, 17 * 60)).OrderBy(m => m).ToList();
			foreach (int minuto in horas)
			{
				double u = rnd.NextDouble();
				var nivel = u < 0.04 ? NivelPrioridad.Azul : u < 0.16 ? NivelPrioridad.Rojo :
							u < 0.50 ? NivelPrioridad.Amarillo : u < 0.85 ? NivelPrioridad.Verde : NivelPrioridad.Blanco;
				int espera = nivel switch
				{
					NivelPrioridad.Azul => rnd.Next(0, 4),
					NivelPrioridad.Rojo => rnd.Next(2, 12),
					NivelPrioridad.Amarillo => rnd.Next(10, 45),
					NivelPrioridad.Verde => rnd.Next(25, 120),
					_ => rnd.Next(40, 180)
				};
				bool cancelado = rnd.NextDouble() < 0.07;
				Registrar(lista[rnd.Next(1, lista.Count)], nivel, motivos[rnd.Next(motivos.Length)], dia.AddMinutes(minuto),
					espera, rnd.Next(8, 25), rnd.Next(1, 5),
					cancelado ? EstadoTurno.Cancelado : EstadoTurno.Finalizado,
					cancelado ? 3 : (rnd.NextDouble() < 0.1 ? 2 : 1));
			}
		}
	}
}
