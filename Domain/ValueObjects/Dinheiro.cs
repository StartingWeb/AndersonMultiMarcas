namespace Domain.ValueObjects;

public readonly record struct Dinheiro : IComparable<Dinheiro>
{
    public decimal Valor { get; }

    public Dinheiro(decimal valor)
    {
        if (valor < 0) throw new ArgumentException("Valor monetario nao pode ser negativo.");
        Valor = decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
    }

    public int CompareTo(Dinheiro other) => Valor.CompareTo(other.Valor);

    public static Dinheiro Zero => new(0m);
    public override string ToString() => Valor.ToString("F2");
}
