namespace SistemaTurnos.Core.Estructuras;

/// <summary>
/// Árbol binario de búsqueda autobalanceado (AVL). La diferencia de altura entre
/// subárboles nunca supera 1, por lo que buscar e insertar cuestan O(log n) incluso
/// cuando las claves llegan ordenadas (caso en el que un ABB simple degeneraría a O(n)).
/// Se usa como índice de pacientes por cédula.
/// </summary>
public class ArbolAVL<TClave, TValor> where TClave : IComparable<TClave>
{
	private sealed class Nodo
	{
		public TClave Clave;
		public TValor Valor;
		public Nodo? Izquierdo, Derecho;
		public int Altura = 1;
		public Nodo(TClave clave, TValor valor) { Clave = clave; Valor = valor; }
	}

	private Nodo? _raiz;

	public int Count { get; private set; }
	public int Altura => AlturaDe(_raiz);

	/// <summary>Inserta o reemplaza el valor asociado a la clave.</summary>
	public void Insertar(TClave clave, TValor valor) => _raiz = Insertar(_raiz, clave, valor);

	public bool TryBuscar(TClave clave, out TValor valor)
	{
		var n = _raiz;
		while (n is not null)
		{
			int c = clave.CompareTo(n.Clave);
			if (c == 0) { valor = n.Valor; return true; }
			n = c < 0 ? n.Izquierdo : n.Derecho;
		}
		valor = default!;
		return false;
	}

	public bool Contiene(TClave clave) => TryBuscar(clave, out _);

	/// <summary>Recorrido en orden: devuelve los valores ordenados por clave.</summary>
	public IEnumerable<TValor> EnOrden()
	{
		var pila = new Stack<Nodo>();
		var actual = _raiz;
		while (actual is not null || pila.Count > 0)
		{
			while (actual is not null) { pila.Push(actual); actual = actual.Izquierdo; }
			actual = pila.Pop();
			yield return actual.Valor;
			actual = actual.Derecho;
		}
	}

	/// <summary>Comprueba que el árbol esté ordenado y balanceado (para pruebas).</summary>
	public bool EsValido() => Validar(_raiz, out _);

	private bool Validar(Nodo? n, out int altura)
	{
		altura = 0;
		if (n is null) return true;
		if (!Validar(n.Izquierdo, out int hi) || !Validar(n.Derecho, out int hd)) return false;
		if (Math.Abs(hi - hd) > 1) return false;
		if (n.Izquierdo is not null && n.Izquierdo.Clave.CompareTo(n.Clave) >= 0) return false;
		if (n.Derecho is not null && n.Derecho.Clave.CompareTo(n.Clave) <= 0) return false;
		altura = 1 + Math.Max(hi, hd);
		return true;
	}

	private Nodo Insertar(Nodo? n, TClave clave, TValor valor)
	{
		if (n is null) { Count++; return new Nodo(clave, valor); }
		int c = clave.CompareTo(n.Clave);
		if (c < 0) n.Izquierdo = Insertar(n.Izquierdo, clave, valor);
		else if (c > 0) n.Derecho = Insertar(n.Derecho, clave, valor);
		else { n.Valor = valor; return n; }

		Actualizar(n);
		int balance = Balance(n);
		if (balance > 1)
		{
			if (Balance(n.Izquierdo!) < 0) n.Izquierdo = RotarIzquierda(n.Izquierdo!);
			return RotarDerecha(n);
		}
		if (balance < -1)
		{
			if (Balance(n.Derecho!) > 0) n.Derecho = RotarDerecha(n.Derecho!);
			return RotarIzquierda(n);
		}
		return n;
	}

	private static int AlturaDe(Nodo? n) => n?.Altura ?? 0;
	private static int Balance(Nodo n) => AlturaDe(n.Izquierdo) - AlturaDe(n.Derecho);
	private static void Actualizar(Nodo n) => n.Altura = 1 + Math.Max(AlturaDe(n.Izquierdo), AlturaDe(n.Derecho));

	private static Nodo RotarDerecha(Nodo y)
	{
		Nodo x = y.Izquierdo!;
		y.Izquierdo = x.Derecho;
		x.Derecho = y;
		Actualizar(y);
		Actualizar(x);
		return x;
	}

	private static Nodo RotarIzquierda(Nodo x)
	{
		Nodo y = x.Derecho!;
		x.Derecho = y.Izquierdo;
		y.Izquierdo = x;
		Actualizar(x);
		Actualizar(y);
		return y;
	}
}
