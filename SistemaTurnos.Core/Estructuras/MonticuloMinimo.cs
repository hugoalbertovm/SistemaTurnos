namespace SistemaTurnos.Core.Estructuras;

/// <summary>
/// Montículo binario de mínimos implementado sobre un arreglo dinámico.
/// Los hijos del nodo i están en 2i + 1 y 2i + 2; el padre está en (i - 1) / 2.
/// Insertar y Extraer: O(log n). VerMinimo: O(1).
/// </summary>
public class MonticuloMinimo<T>
{
	private readonly List<T> _datos;
	private readonly IComparer<T> _comparador;

	public MonticuloMinimo(IComparer<T> comparador, int capacidadInicial = 16)
	{
		_comparador = comparador ?? throw new ArgumentNullException(nameof(comparador));
		_datos = new List<T>(capacidadInicial);
	}

	public int Count => _datos.Count;
	public bool EstaVacio => _datos.Count == 0;

	public void Insertar(T elemento)
	{
		_datos.Add(elemento);
		Flotar(_datos.Count - 1);
	}

	public T VerMinimo()
	{
		if (EstaVacio) throw new InvalidOperationException("El montículo está vacío.");
		return _datos[0];
	}

	public bool TryExtraerMinimo(out T elemento)
	{
		if (EstaVacio) { elemento = default!; return false; }
		elemento = ExtraerMinimo();
		return true;
	}

	public T ExtraerMinimo()
	{
		if (EstaVacio) throw new InvalidOperationException("El montículo está vacío.");
		T minimo = _datos[0];
		int ultimo = _datos.Count - 1;
		_datos[0] = _datos[ultimo];
		_datos.RemoveAt(ultimo);
		if (_datos.Count > 0) Hundir(0);
		return minimo;
	}

	/// <summary>Elimina el primer elemento que cumpla la condición. O(n) para buscar + O(log n) para reacomodar.</summary>
	public bool Eliminar(Predicate<T> condicion)
	{
		int i = _datos.FindIndex(condicion);
		if (i < 0) return false;
		int ultimo = _datos.Count - 1;
		_datos[i] = _datos[ultimo];
		_datos.RemoveAt(ultimo);
		if (i < _datos.Count)
		{
			Flotar(i);
			Hundir(i);
		}
		return true;
	}

	/// <summary>Copia ordenada para mostrar en pantalla, sin alterar el montículo. O(n log n).</summary>
	public List<T> ComoListaOrdenada()
	{
		var copia = new List<T>(_datos);
		copia.Sort(_comparador);
		return copia;
	}

	/// <summary>Vista del arreglo interno (para depuración y para la figura del documento).</summary>
	public IReadOnlyList<T> ArregloInterno => _datos;

	/// <summary>Verifica la propiedad de montículo: cada padre es menor o igual que sus hijos.</summary>
	public bool EsValido()
	{
		for (int i = 0; i < _datos.Count; i++)
		{
			int izq = 2 * i + 1, der = 2 * i + 2;
			if (izq < _datos.Count && _comparador.Compare(_datos[i], _datos[izq]) > 0) return false;
			if (der < _datos.Count && _comparador.Compare(_datos[i], _datos[der]) > 0) return false;
		}
		return true;
	}

	private void Flotar(int i)
	{
		while (i > 0)
		{
			int padre = (i - 1) / 2;
			if (_comparador.Compare(_datos[i], _datos[padre]) >= 0) break;
			(_datos[i], _datos[padre]) = (_datos[padre], _datos[i]);
			i = padre;
		}
	}

	private void Hundir(int i)
	{
		int n = _datos.Count;
		while (true)
		{
			int izq = 2 * i + 1, der = 2 * i + 2, menor = i;
			if (izq < n && _comparador.Compare(_datos[izq], _datos[menor]) < 0) menor = izq;
			if (der < n && _comparador.Compare(_datos[der], _datos[menor]) < 0) menor = der;
			if (menor == i) break;
			(_datos[i], _datos[menor]) = (_datos[menor], _datos[i]);
			i = menor;
		}
	}
}
