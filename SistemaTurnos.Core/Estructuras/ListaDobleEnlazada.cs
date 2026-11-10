using System.Collections;

namespace SistemaTurnos.Core.Estructuras;

/// <summary>Nodo con referencias al anterior y al siguiente.</summary>
public sealed class NodoDoble<T>
{
	public T Valor { get; }
	public NodoDoble<T>? Anterior { get; internal set; }
	public NodoDoble<T>? Siguiente { get; internal set; }
	internal ListaDobleEnlazada<T>? Lista { get; set; }

	internal NodoDoble(T valor) => Valor = valor;
}

/// <summary>
/// Lista doblemente enlazada implementada desde cero. Inserción y eliminación en O(1)
/// cuando se conoce el nodo; recorrido en ambos sentidos.
/// Se usa para el historial de atenciones de cada paciente.
/// </summary>
public class ListaDobleEnlazada<T> : IEnumerable<T>
{
	public NodoDoble<T>? Primero { get; private set; }
	public NodoDoble<T>? Ultimo { get; private set; }
	public int Count { get; private set; }

	public NodoDoble<T> AgregarAlFinal(T valor)
	{
		var nodo = new NodoDoble<T>(valor) { Lista = this, Anterior = Ultimo };
		if (Ultimo is null) Primero = nodo; else Ultimo.Siguiente = nodo;
		Ultimo = nodo;
		Count++;
		return nodo;
	}

	public NodoDoble<T> AgregarAlInicio(T valor)
	{
		var nodo = new NodoDoble<T>(valor) { Lista = this, Siguiente = Primero };
		if (Primero is null) Ultimo = nodo; else Primero.Anterior = nodo;
		Primero = nodo;
		Count++;
		return nodo;
	}

	public void Eliminar(NodoDoble<T> nodo)
	{
		if (nodo.Lista != this) throw new InvalidOperationException("El nodo no pertenece a esta lista.");
		if (nodo.Anterior is null) Primero = nodo.Siguiente; else nodo.Anterior.Siguiente = nodo.Siguiente;
		if (nodo.Siguiente is null) Ultimo = nodo.Anterior; else nodo.Siguiente.Anterior = nodo.Anterior;
		// Se cortan las referencias para que el nodo pueda ser recolectado (evita referencias colgantes).
		nodo.Anterior = nodo.Siguiente = null;
		nodo.Lista = null;
		Count--;
	}

	public void Limpiar()
	{
		Primero = Ultimo = null;
		Count = 0;
	}

	public IEnumerable<T> RecorridoInverso()
	{
		for (var n = Ultimo; n is not null; n = n.Anterior) yield return n.Valor;
	}

	public IEnumerator<T> GetEnumerator()
	{
		for (var n = Primero; n is not null; n = n.Siguiente) yield return n.Valor;
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
