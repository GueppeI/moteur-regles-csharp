using System.Globalization;

namespace MoteurRegles.Remises.Modele;

public enum TypeRemise
{
    /// <summary>Pourcentage du montant brut.</summary>
    Pourcentage,

    /// <summary>Montant fixe en CHF.</summary>
    MontantFixe,

    /// <summary>La commande n'est éligible à aucune remise.</summary>
    Exclusion,
}

/// <summary>
/// Effet produit par une règle de remise.
/// Une remise <see cref="Cumulable"/> s'additionne aux autres ; une remise non cumulable (exclusive)
/// est mise en concurrence avec le cumul des autres, et seule la plus avantageuse est retenue.
/// </summary>
public sealed record Remise
{
    private Remise(TypeRemise type, decimal valeur, string motif, bool cumulable)
    {
        Type = type;
        Valeur = valeur;
        Motif = motif;
        Cumulable = cumulable;
    }

    public TypeRemise Type { get; }

    public decimal Valeur { get; }

    public string Motif { get; }

    public bool Cumulable { get; }

    public static Remise Pourcentage(decimal taux, string motif, bool cumulable = true)
    {
        if (taux is <= 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(taux), taux, "Le taux doit être compris entre 0 (exclu) et 100.");
        ArgumentException.ThrowIfNullOrWhiteSpace(motif);
        return new Remise(TypeRemise.Pourcentage, taux, motif, cumulable);
    }

    public static Remise MontantFixe(decimal montant, string motif, bool cumulable = true)
    {
        if (montant <= 0)
            throw new ArgumentOutOfRangeException(nameof(montant), montant, "Le montant doit être strictement positif.");
        ArgumentException.ThrowIfNullOrWhiteSpace(motif);
        return new Remise(TypeRemise.MontantFixe, montant, motif, cumulable);
    }

    public static Remise Exclusion(string motif)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motif);
        return new Remise(TypeRemise.Exclusion, 0m, motif, cumulable: false);
    }

    public override string ToString()
    {
        var ch = CultureInfo.GetCultureInfo("fr-CH");
        var valeur = Type switch
        {
            TypeRemise.Pourcentage => $"-{Valeur.ToString("0.##", ch)} %",
            TypeRemise.MontantFixe => $"-{Valeur.ToString("0.00", ch)} CHF",
            _ => "EXCLUSION",
        };
        var exclusive = Type != TypeRemise.Exclusion && !Cumulable ? " [exclusive]" : string.Empty;
        return $"{valeur}{exclusive} : {Motif}";
    }
}
