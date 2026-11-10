using SistemaTurnos.Core.Modelo;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SistemaTurnos.Core.Logica;

/// <summary>
/// Reglas de atención preferencial expresadas como cláusulas de Horn.
/// El sistema NO asigna el nivel clínico (eso lo decide el personal de salud);
/// solo deduce si el paciente tiene derecho a atención preferencial dentro de su nivel.
/// </summary>
public class ReglasAtencion
{
	public const string Programa = """
        % Reglas de atención preferencial (paradigma lógico)
        adulto_mayor(P) :- edad(P, E), E >= 65.
        preferencial(P, adulto_mayor) :- adulto_mayor(P).
        preferencial(P, embarazo) :- condicion(P, embarazo).
        preferencial(P, discapacidad) :- condicion(P, discapacidad).
        """;

	/// <summary>Evalúa a un paciente: crea hechos a partir de sus datos y consulta preferencial(p, Razon).</summary>
	public EvaluacionPreferencial Evaluar(Paciente paciente, DateTime hoy) =>
		Evaluar(paciente.Edad(hoy), paciente.EsEmbarazada, paciente.TieneDiscapacidad);

	public EvaluacionPreferencial Evaluar(int edad, bool embarazo, bool discapacidad)
	{
		var motor = new MotorLogico();
		motor.Cargar(Programa);
		var p = new Atomo("paciente");
		motor.AgregarHecho("edad", p, new Numero(edad));
		if (embarazo) motor.AgregarHecho("condicion", p, new Atomo("embarazo"));
		if (discapacidad) motor.AgregarHecho("condicion", p, new Atomo("discapacidad"));

		var razones = motor.Consultar("preferencial(paciente, Razon)")
			.Select(s => MotorLogico.Resolver(new Variable("Razon"), s).ToString())
			.Distinct()
			.Select(r => r switch
			{
				"adulto_mayor" => "adulto mayor (65 años o más)",
				"embarazo" => "embarazo",
				"discapacidad" => "discapacidad",
				_ => r
			})
			.ToList();
		return new EvaluacionPreferencial(razones.Count > 0, razones);
	}
}
