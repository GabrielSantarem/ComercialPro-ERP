using System;

namespace GetStartedApp.Models;

public class Vendedor : IEquatable<Vendedor>
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;

    public bool Equals(Vendedor? other)
    {
        if (other is null) return false;
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Vendedor);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public override string ToString()
    {
        return Nome;
    }
}
