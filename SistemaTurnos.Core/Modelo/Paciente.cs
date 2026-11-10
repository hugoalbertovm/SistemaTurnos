namespace SistemaTurnos.Core.Modelo;

public class Paciente : Persona
{
	public DateTime FechaNacimiento { get; private set; }
	public string Telefono { get; private set; } = "";
	public bool EsEmbarazada { get; private set; }
	public bool TieneDiscapacidad { get; private set; }

	public Paciente(Cedula cedula, string nombre, DateTime fechaNacimiento, string telefono = "",
					bool esEmbarazada = false, bool tieneDiscapacidad = false, int id = 0)
		: base(cedula, nombre)
	{
		Id = id;
		Actualizar(nombre, fechaNacimiento, telefono, esEmbarazada, tieneDiscapacidad);
	}

	/// <summary>Encapsulamiento: los datos solo cambian a través de este método, que valida.</summary>
	public void Actualizar(string nombre, DateTime fechaNacimiento, string telefono,
						   bool esEmbarazada, bool tieneDiscapacidad)
	{
		if (fechaNacimiento.Date > DateTime.Today)
			throw new ArgumentException("La fecha de nacimiento no puede ser futura.");
		if (fechaNacimiento.Year < 1900)
			throw new ArgumentException("La fecha de nacimiento no es válida.");
		CambiarNombre(nombre);
		FechaNacimiento = fechaNacimiento.Date;
		Telefono = telefono?.Trim() ?? string.Empty;
		EsEmbarazada = esEmbarazada;
		TieneDiscapacidad = tieneDiscapacidad;
	}

	public int Edad(DateTime hoy)
	{
		int edad = hoy.Year - FechaNacimiento.Year;
		if (FechaNacimiento.Date > hoy.AddYears(-edad).Date) edad--;
		return Math.Max(0, edad);
	}

	public override string Describir() => $"Paciente {NombreCorto}";
}
