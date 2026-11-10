/* =====================================================================
   Consultas para revisar la base de datos en SQL Server Management Studio (SSMS).
   Útiles como evidencia del diseño y de la optimización.
   ===================================================================== */
USE SistemaTurnos;
GO

-- 1. Objetos creados por la aplicación
SELECT name AS Objeto, type_desc AS Tipo
FROM sys.objects
WHERE is_ms_shipped = 0 AND type IN ('U', 'V', 'P')
ORDER BY type_desc, name;

-- 2. Restricciones (llaves primarias, únicas, foráneas, CHECK y valores por defecto)
SELECT OBJECT_NAME(parent_object_id) AS Tabla, name AS Restriccion, type_desc AS Tipo
FROM sys.objects
WHERE type IN ('PK', 'UQ', 'F', 'C', 'D')
ORDER BY Tabla, Tipo;

-- 3. Índices no agrupados, columnas incluidas y filtros
SELECT OBJECT_NAME(i.object_id) AS Tabla, i.name AS Indice, i.filter_definition AS Filtro,
       STRING_AGG(CASE WHEN ic.is_included_column = 0 THEN c.name END, ', ') AS ColumnasClave,
       STRING_AGG(CASE WHEN ic.is_included_column = 1 THEN c.name END, ', ') AS ColumnasIncluidas
FROM sys.indexes AS i
JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.name LIKE 'IX[_]%'
GROUP BY i.object_id, i.name, i.filter_definition;

-- 4. Plan de ejecución: active "Incluir plan de ejecución real" (Ctrl + M) y ejecute.
--    Debe aparecer una búsqueda (Index Seek) sobre IX_Turno_Activos y no un recorrido de la tabla.
SET STATISTICS IO ON;
SELECT Id, Codigo, PacienteId, NivelId, Motivo, Preferencial, HoraLlegada, Estado, Llamados,
       ConsultorioId, HoraLlamado, ReingresoEn
FROM dbo.Turno
WHERE Estado IN ('EN_ESPERA', 'EN_ATENCION', 'AUSENTE')
ORDER BY NivelId, Preferencial DESC, HoraLlegada;

-- 5. Historial de un paciente mediante el procedimiento almacenado (usa IX_Turno_Paciente)
EXEC dbo.usp_HistorialPaciente @PacienteId = 1;

-- 6. Resumen por nivel calculado en el servidor
DECLARE @Hoy DATE = CAST(SYSDATETIME() AS DATE);
DECLARE @Desde DATE = DATEADD(DAY, -30, @Hoy);
EXEC dbo.usp_ResumenPorNivel @Desde = @Desde, @Hasta = @Hoy;
SET STATISTICS IO OFF;

-- 7. Últimos eventos de la bitácora
SELECT TOP (20) FechaHora, Tipo, Mensaje FROM dbo.Bitacora ORDER BY FechaHora DESC;