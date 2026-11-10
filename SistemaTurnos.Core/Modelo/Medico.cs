using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SistemaTurnos.Core.Modelo;

public class Medico : Persona
{
	public string Especialidad { get; }

	public Medico(Cedula cedula, string nombre, string especialidad, int id = 0) : base(cedula, nombre)
	{
		Id = id;
		Especialidad = especialidad;
	}

	public override string Describir() => $"{Nombre} — {Especialidad}";
}

/// <summary>Consultorio de la clínica, con el médico asignado.</summary>
public record Consultorio(int Id, int Numero, Medico? Medico)
{
	public override string ToString() =>
		Medico is null ? $"Consultorio {Numero}" : $"Consultorio {Numero} — {Medico.Nombre}";
}
