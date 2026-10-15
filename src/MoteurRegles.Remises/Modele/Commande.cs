namespace MoteurRegles.Remises.Modele;

public enum CategorieClient
{
    Standard,
    Premium,
    Employe,
}

public sealed record Client(
    string Id,
    string Nom,
    CategorieClient Categorie,
    DateOnly DateInscription,
    int NombreCommandesPassees,
    bool ADesImpayes = false);

public sealed record LigneCommande(
    string Reference,
    string Libelle,
    string Categorie,
    decimal PrixUnitaire,
    int Quantite)
{
    public decimal Total => PrixUnitaire * Quantite;
}

/// <summary>
/// Contexte évalué par le moteur de règles de remise.
/// La date de commande fait partie du contexte : les règles ne lisent jamais l'horloge système,
/// ce qui rend l'évaluation déterministe et testable.
/// </summary>
public sealed record Commande(
    string Numero,
    DateOnly Date,
    Client Client,
    IReadOnlyList<LigneCommande> Lignes,
    string? CodePromo = null)
{
    public decimal MontantBrut => Lignes.Sum(l => l.Total);

    public int NombreArticles(string categorie) =>
        Lignes.Where(l => string.Equals(l.Categorie, categorie, StringComparison.OrdinalIgnoreCase))
              .Sum(l => l.Quantite);
}
