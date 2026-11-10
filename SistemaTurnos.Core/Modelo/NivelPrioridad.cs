namespace SistemaTurnos.Core.Modelo;

/// <summary>
/// Niveles de la escala CTAS que usa la CCSS. Menor número = mayor prioridad.
/// </summary>
public enum NivelPrioridad
{
	Azul = 1,
	Rojo = 2,
	Amarillo = 3,
	Verde = 4,
	Blanco = 5
}

public static class NivelPrioridadExtensiones
{
	public static string Descripcion(this NivelPrioridad nivel) => nivel switch
	{
		NivelPrioridad.Azul => "Riesgo vital inminente",
		NivelPrioridad.Rojo => "Emergencia",
		NivelPrioridad.Amarillo => "Urgencia con condición estable",
		NivelPrioridad.Verde => "Menos urgente",
		_ => "No urgente"
	};

	/// <summary>Tiempo máximo de referencia, en minutos (Tabla III del documento).</summary>
	public static int MinutosReferencia(this NivelPrioridad nivel) => nivel switch
	{
		NivelPrioridad.Azul => 3,
		NivelPrioridad.Rojo => 10,
		NivelPrioridad.Amarillo => 40,
		NivelPrioridad.Verde => 180,
		_ => 1440
	};

	public static bool EsValido(int valor) => valor is >= 1 and <= 5;
}
