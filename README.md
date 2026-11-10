# Sistema de gestión priorizada de turnos

Aplicación de escritorio desarrollada en C# con .NET 8, Windows Forms y SQL Server. Simula la gestión de turnos de la Clínica Médica Integral del Pacífico, una clínica ficticia.

Proyecto académico del curso SC-250 Paradigmas de Programación de la Universidad Fidélitas.

## Funcionalidades

- **Recepción:** registro de pacientes y asignación de turnos según el nivel de prioridad.
- **Consultorio:** llamado de pacientes, gestión de ausencias y consulta del historial de atenciones.
- **Pantalla pública:** visualización de los llamados en la sala de espera.
- **Reportes:** consulta de tiempos de espera, ausencias, cancelaciones y demanda por franja horaria.

La fila de espera se organiza mediante una cola de prioridad que considera el nivel de triaje, la atención preferencial y la hora de llegada.

## Estructura del proyecto

- `SistemaTurnos`: interfaz gráfica.
- `SistemaTurnos.Core`: lógica del sistema, estructuras de datos y acceso a la base de datos.
- `SistemaTurnos.Pruebas`: pruebas automatizadas.

## Ejecución

Abra `SistemaTurnos.slnx` en Visual Studio. En el Explorador de soluciones,
haga clic derecho sobre `SistemaTurnos`, seleccione
**Establecer como proyecto de inicio** y presione **F5**.