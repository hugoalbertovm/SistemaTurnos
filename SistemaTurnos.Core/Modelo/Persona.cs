namespace SistemaTurnos.Core.Modelo;

/// <summary>
/// Clase abstracta base (POO: abstracción y herencia). Paciente y Médico heredan de ella.
/// </summary>
public abstract class Persona
{
	public int Id { get; internal set; }
	public Cedula Cedula { get; }
	public string Nombre { get; private set; }

	protected Persona(Cedula cedula, string nombre)
	{
		if (string.IsNullOrWhiteSpace(nombre))
			throw new ArgumentException("El nombre es obligatorio.", nameof(nombre));
		Cedula = cedula;
		Nombre = nombre.Trim();
	}

	/// <summary>Nombre corto para mostrar en pantallas (ej.: "A. Méndez").</summary>
	public string NombreCorto
	{
		get
		{
			var partes = Nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			return partes.Length >= 2 ? $"{partes[0][0]}. {partes[1]}" : Nombre;
		}
	}

	protected void CambiarNombre(string nombre)
	{
		if (string.IsNullOrWhiteSpace(nombre))
			throw new ArgumentException("El nombre es obligatorio.", nameof(nombre));
		Nombre = nombre.Trim();
	}

	/// <summary>Método polimórfico: cada subclase describe su rol.</summary>
	public abstract string Describir();

	public override string ToString() => $"{Nombre} ({Cedula})";
}
