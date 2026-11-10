namespace SistemaTurnos.Core.Datos;

/// <summary>
/// Esquema relacional de SQL Server en tercera forma normal (3FN).
/// Cada elemento es un lote independiente (equivale a los bloques separados por GO en SSMS).
/// Las sentencias verifican si el objeto existe, por lo que es seguro ejecutarlas varias veces.
/// El mismo script está en docs/BaseDatos/01_esquema.sql.
/// </summary>
public static class Esquema
{
	public static readonly string[] Lotes =
	{
        // ---------------------------------------------------------------- Catálogo de niveles CTAS
        """
        IF OBJECT_ID(N'dbo.NivelPrioridad', N'U') IS NULL
        CREATE TABLE dbo.NivelPrioridad (
            Id                 TINYINT       NOT NULL,
            Color              NVARCHAR(20)  NOT NULL,
            Descripcion        NVARCHAR(80)  NOT NULL,
            MinutosReferencia  INT           NOT NULL,
            CONSTRAINT PK_NivelPrioridad PRIMARY KEY (Id),
            CONSTRAINT UQ_NivelPrioridad_Color UNIQUE (Color),
            CONSTRAINT CK_NivelPrioridad_Id CHECK (Id BETWEEN 1 AND 5),
            CONSTRAINT CK_NivelPrioridad_Minutos CHECK (MinutosReferencia > 0)
        );
        """,

        // ---------------------------------------------------------------- Pacientes
        """
        IF OBJECT_ID(N'dbo.Paciente', N'U') IS NULL
        CREATE TABLE dbo.Paciente (
            Id                 INT IDENTITY(1, 1) NOT NULL,
            Cedula             CHAR(11)      NOT NULL,
            Nombre             NVARCHAR(120) NOT NULL,
            FechaNacimiento    DATE          NOT NULL,
            Telefono           VARCHAR(15)   NULL,
            EsEmbarazada       BIT           NOT NULL CONSTRAINT DF_Paciente_EsEmbarazada DEFAULT (0),
            TieneDiscapacidad  BIT           NOT NULL CONSTRAINT DF_Paciente_TieneDiscapacidad DEFAULT (0),
            CreadoEn           DATETIME2(0)  NOT NULL CONSTRAINT DF_Paciente_CreadoEn DEFAULT (SYSDATETIME()),
            CONSTRAINT PK_Paciente PRIMARY KEY (Id),
            CONSTRAINT UQ_Paciente_Cedula UNIQUE (Cedula),
            CONSTRAINT CK_Paciente_Cedula CHECK (Cedula LIKE '[1-9]-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'),
            CONSTRAINT CK_Paciente_Nombre CHECK (LEN(LTRIM(RTRIM(Nombre))) > 0),
            CONSTRAINT CK_Paciente_FechaNacimiento CHECK (FechaNacimiento >= '19000101')
        );
        """,

        // ---------------------------------------------------------------- Médicos y consultorios
        """
        IF OBJECT_ID(N'dbo.Medico', N'U') IS NULL
        CREATE TABLE dbo.Medico (
            Id            INT IDENTITY(1, 1) NOT NULL,
            Cedula        CHAR(11)      NOT NULL,
            Nombre        NVARCHAR(120) NOT NULL,
            Especialidad  NVARCHAR(60)  NOT NULL,
            CONSTRAINT PK_Medico PRIMARY KEY (Id),
            CONSTRAINT UQ_Medico_Cedula UNIQUE (Cedula)
        );
        """,
		"""
        IF OBJECT_ID(N'dbo.Consultorio', N'U') IS NULL
        CREATE TABLE dbo.Consultorio (
            Id        INT IDENTITY(1, 1) NOT NULL,
            Numero    TINYINT NOT NULL,
            MedicoId  INT     NULL,
            CONSTRAINT PK_Consultorio PRIMARY KEY (Id),
            CONSTRAINT UQ_Consultorio_Numero UNIQUE (Numero),
            CONSTRAINT FK_Consultorio_Medico FOREIGN KEY (MedicoId) REFERENCES dbo.Medico (Id) ON DELETE SET NULL
        );
        """,

        // ---------------------------------------------------------------- Turnos
        """
        IF OBJECT_ID(N'dbo.Turno', N'U') IS NULL
        CREATE TABLE dbo.Turno (
            Id             INT IDENTITY(1, 1) NOT NULL,
            Fecha          DATE          NOT NULL,
            Codigo         VARCHAR(8)    NOT NULL,
            PacienteId     INT           NOT NULL,
            NivelId        TINYINT       NOT NULL,
            Motivo         NVARCHAR(200) NOT NULL,
            Preferencial   BIT           NOT NULL CONSTRAINT DF_Turno_Preferencial DEFAULT (0),
            HoraLlegada    DATETIME2(0)  NOT NULL,
            Estado         VARCHAR(12)   NOT NULL,
            Llamados       TINYINT       NOT NULL CONSTRAINT DF_Turno_Llamados DEFAULT (0),
            ConsultorioId  INT           NULL,
            MedicoId       INT           NULL,
            HoraLlamado    DATETIME2(0)  NULL,
            HoraFin        DATETIME2(0)  NULL,
            ReingresoEn    DATETIME2(0)  NULL,
            CONSTRAINT PK_Turno PRIMARY KEY (Id),
            CONSTRAINT UQ_Turno_Fecha_Codigo UNIQUE (Fecha, Codigo),
            CONSTRAINT FK_Turno_Paciente FOREIGN KEY (PacienteId) REFERENCES dbo.Paciente (Id),
            CONSTRAINT FK_Turno_Nivel FOREIGN KEY (NivelId) REFERENCES dbo.NivelPrioridad (Id),
            CONSTRAINT FK_Turno_Consultorio FOREIGN KEY (ConsultorioId) REFERENCES dbo.Consultorio (Id),
            CONSTRAINT FK_Turno_Medico FOREIGN KEY (MedicoId) REFERENCES dbo.Medico (Id),
            CONSTRAINT CK_Turno_Estado CHECK (Estado IN ('EN_ESPERA', 'EN_ATENCION', 'AUSENTE', 'FINALIZADO', 'CANCELADO')),
            CONSTRAINT CK_Turno_Llamados CHECK (Llamados BETWEEN 0 AND 3),
            CONSTRAINT CK_Turno_HoraLlamado CHECK (HoraLlamado IS NULL OR HoraLlamado >= HoraLlegada),
            CONSTRAINT CK_Turno_HoraFin CHECK (HoraFin IS NULL OR HoraFin >= HoraLlegada)
        );
        """,

        // Índice FILTRADO: solo contiene los turnos activos, que son los que se cargan al iniciar.
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Turno_Activos' AND object_id = OBJECT_ID(N'dbo.Turno'))
        CREATE NONCLUSTERED INDEX IX_Turno_Activos
            ON dbo.Turno (Estado, NivelId, Preferencial DESC, HoraLlegada)
            INCLUDE (Codigo, PacienteId, Motivo, Llamados, ConsultorioId, HoraLlamado, ReingresoEn)
            WHERE Estado IN ('EN_ESPERA', 'EN_ATENCION', 'AUSENTE');
        """,
        // Índice de COBERTURA para el historial de un paciente (la consulta no necesita leer la tabla).
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Turno_Paciente' AND object_id = OBJECT_ID(N'dbo.Turno'))
        CREATE NONCLUSTERED INDEX IX_Turno_Paciente
            ON dbo.Turno (PacienteId, HoraLlegada DESC)
            INCLUDE (Motivo, NivelId, MedicoId, HoraLlamado, Estado);
        """,
        // Índice para los reportes por rango de fechas.
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Turno_Fecha' AND object_id = OBJECT_ID(N'dbo.Turno'))
        CREATE NONCLUSTERED INDEX IX_Turno_Fecha
            ON dbo.Turno (Fecha, Estado)
            INCLUDE (Codigo, NivelId, Preferencial, HoraLlegada, HoraLlamado, HoraFin, Llamados);
        """,
        // Índice para la llave foránea Consultorio.MedicoId.
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Consultorio_Medico' AND object_id = OBJECT_ID(N'dbo.Consultorio'))
        CREATE NONCLUSTERED INDEX IX_Consultorio_Medico ON dbo.Consultorio (MedicoId);
        """,

        // ---------------------------------------------------------------- Bitácora
        """
        IF OBJECT_ID(N'dbo.Bitacora', N'U') IS NULL
        CREATE TABLE dbo.Bitacora (
            Id         BIGINT IDENTITY(1, 1) NOT NULL,
            FechaHora  DATETIME2(3)   NOT NULL,
            Tipo       VARCHAR(12)    NOT NULL,
            Mensaje    NVARCHAR(1000) NOT NULL,
            CONSTRAINT PK_Bitacora PRIMARY KEY (Id),
            CONSTRAINT CK_Bitacora_Tipo CHECK (Tipo IN ('INFO', 'ADVERTENCIA', 'ERROR'))
        );
        """,
		"""
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bitacora_FechaHora' AND object_id = OBJECT_ID(N'dbo.Bitacora'))
        CREATE NONCLUSTERED INDEX IX_Bitacora_FechaHora ON dbo.Bitacora (FechaHora DESC) INCLUDE (Tipo);
        """,

        // ---------------------------------------------------------------- Vista del historial
        // El historial se deriva de los turnos finalizados: no se guardan datos duplicados.
        """
        CREATE OR ALTER VIEW dbo.vw_Historial AS
            SELECT t.PacienteId,
                   t.HoraLlegada,
                   t.Motivo,
                   t.NivelId,
                   COALESCE(m.Nombre, N'Sin asignar') AS Medico,
                   DATEDIFF(MINUTE, t.HoraLlegada, t.HoraLlamado) AS MinutosEspera
            FROM dbo.Turno AS t
            LEFT JOIN dbo.Medico AS m ON m.Id = t.MedicoId
            WHERE t.Estado = 'FINALIZADO';
        """,

        // ---------------------------------------------------------------- Procedimientos almacenados
        """
        CREATE OR ALTER PROCEDURE dbo.usp_HistorialPaciente
            @PacienteId INT
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT HoraLlegada, Motivo, NivelId, Medico, COALESCE(MinutosEspera, 0) AS MinutosEspera
            FROM dbo.vw_Historial
            WHERE PacienteId = @PacienteId
            ORDER BY HoraLlegada DESC;
        END;
        """,
		"""
        CREATE OR ALTER PROCEDURE dbo.usp_CerrarDiasAnteriores
            @Hoy DATE,
            @Ahora DATETIME2(0)
        AS
        BEGIN
            SET NOCOUNT ON;
            UPDATE dbo.Turno
               SET Estado = 'CANCELADO', HoraFin = @Ahora, ReingresoEn = NULL
             WHERE Estado IN ('EN_ESPERA', 'EN_ATENCION', 'AUSENTE')
               AND Fecha < @Hoy;
            SELECT @@ROWCOUNT AS TurnosCerrados;
        END;
        """,
		"""
        CREATE OR ALTER PROCEDURE dbo.usp_ResumenPorNivel
            @Desde DATE,
            @Hasta DATE
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT n.Id AS Nivel,
                   n.Color,
                   COUNT(t.Id) AS Atenciones,
                   AVG(CAST(DATEDIFF(MINUTE, t.HoraLlegada, t.HoraLlamado) AS DECIMAL(9, 1))) AS EsperaPromedioMin,
                   n.MinutosReferencia
            FROM dbo.NivelPrioridad AS n
            LEFT JOIN dbo.Turno AS t
                   ON t.NivelId = n.Id AND t.Estado = 'FINALIZADO' AND t.Fecha BETWEEN @Desde AND @Hasta
            GROUP BY n.Id, n.Color, n.MinutosReferencia
            ORDER BY n.Id;
        END;
        """
	};
}
