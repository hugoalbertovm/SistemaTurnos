namespace SistemaTurnos.Core.Datos;

/// <summary>
/// Sentencias T-SQL del sistema. Todas son PARAMETRIZADAS (@Nombre): los valores nunca se
/// concatenan al texto SQL, lo que evita la inyección de SQL (seguridad de los datos).
/// </summary>
public static class Sql
{
	public const string PacientesTodos =
		"SELECT Id, Cedula, Nombre, FechaNacimiento, Telefono, EsEmbarazada, TieneDiscapacidad FROM dbo.Paciente ORDER BY Cedula;";

	public const string PacienteInsertar =
		"INSERT INTO dbo.Paciente (Cedula, Nombre, FechaNacimiento, Telefono, EsEmbarazada, TieneDiscapacidad) " +
		"OUTPUT INSERTED.Id VALUES (@Cedula, @Nombre, @FechaNacimiento, @Telefono, @EsEmbarazada, @TieneDiscapacidad);";

	public const string PacienteActualizar =
		"UPDATE dbo.Paciente SET Nombre = @Nombre, FechaNacimiento = @FechaNacimiento, Telefono = @Telefono, " +
		"EsEmbarazada = @EsEmbarazada, TieneDiscapacidad = @TieneDiscapacidad WHERE Id = @Id;";

	public const string TurnoInsertar =
		"INSERT INTO dbo.Turno (Fecha, Codigo, PacienteId, NivelId, Motivo, Preferencial, HoraLlegada, Estado, Llamados) " +
		"OUTPUT INSERTED.Id VALUES (@Fecha, @Codigo, @PacienteId, @NivelId, @Motivo, @Preferencial, @HoraLlegada, @Estado, @Llamados);";

	public const string TurnoActualizar =
		"UPDATE dbo.Turno SET Estado = @Estado, Llamados = @Llamados, ConsultorioId = @ConsultorioId, " +
		"HoraLlamado = @HoraLlamado, HoraFin = @HoraFin, ReingresoEn = @ReingresoEn, " +
		// Al finalizar se guarda el médico que atendió, para que el historial no cambie si el consultorio cambia de médico.
		"MedicoId = CASE WHEN @Estado = 'FINALIZADO' " +
		"THEN (SELECT c.MedicoId FROM dbo.Consultorio AS c WHERE c.Id = @ConsultorioId) ELSE MedicoId END " +
		"WHERE Id = @Id;";

	public const string TurnosActivos =
		"SELECT Id, Codigo, PacienteId, NivelId, Motivo, Preferencial, HoraLlegada, Estado, Llamados, " +
		"ConsultorioId, HoraLlamado, HoraFin, ReingresoEn FROM dbo.Turno " +
		"WHERE Estado IN ('EN_ESPERA', 'EN_ATENCION', 'AUSENTE') ORDER BY NivelId, Preferencial DESC, HoraLlegada;";

	public const string UltimoNumeroDelDia =
		"SELECT COALESCE(MAX(CAST(SUBSTRING(Codigo, 3, 6) AS INT)), 0) FROM dbo.Turno WHERE Fecha = @Fecha;";

	public const string HistorialGeneral =
		"SELECT h.HoraLlegada, h.Motivo, h.NivelId, h.Medico, COALESCE(h.MinutosEspera, 0), p.Nombre " +
		"FROM dbo.vw_Historial AS h INNER JOIN dbo.Paciente AS p ON p.Id = h.PacienteId;";

	public const string Cerrados =
		"SELECT Codigo, NivelId, Preferencial, HoraLlegada, HoraLlamado, HoraFin, Estado, Llamados FROM dbo.Turno " +
		"WHERE Estado IN ('FINALIZADO', 'CANCELADO') AND Fecha BETWEEN @Desde AND @Hasta;";

	public const string Consultorios =
		"SELECT c.Id, c.Numero, m.Id, m.Cedula, m.Nombre, m.Especialidad FROM dbo.Consultorio AS c " +
		"LEFT JOIN dbo.Medico AS m ON m.Id = c.MedicoId ORDER BY c.Numero;";

	public const string BitacoraInsertar =
		"INSERT INTO dbo.Bitacora (FechaHora, Tipo, Mensaje) VALUES (@FechaHora, @Tipo, @Mensaje);";

	// Procedimientos almacenados
	public const string ProcHistorialPaciente = "dbo.usp_HistorialPaciente";
	public const string ProcCerrarDiasAnteriores = "dbo.usp_CerrarDiasAnteriores";
	public const string ProcResumenPorNivel = "dbo.usp_ResumenPorNivel";
}
