using MoteurRegles.Core;
using MoteurRegles.Remises;
using MoteurRegles.Remises.Configuration;
using MoteurRegles.Remises.Modele;

namespace MoteurRegles.Tests;

/// <summary>Fabriques de données de test (patron « Object Mother »).</summary>
internal static class Donnees
{
    public static readonly DateOnly Aujourdhui = new(2026, 10, 12);

    public static string CheminDonnees(string fichier) => Path.Combine(AppContext.BaseDirectory, "donnees", fichier);

    public static Client Client(
        CategorieClient categorie = CategorieClient.Standard,
        int commandes = 0,
        bool impayes = false,
        DateOnly? inscription = null) =>
        new("CTEST", "Client Test", categorie, inscription ?? new DateOnly(2026, 1, 1), commandes, impayes);

    /// <summary>Commande d'une seule ligne dont le total vaut <paramref name="montant"/>.</summary>
    public static Commande Commande(
        decimal montant = 100m,
        Client? client = null,
        string? code = null,
        DateOnly? date = null,
        string categorie = "Divers",
        int quantite = 1) =>
        new("CMD-TEST", date ?? Aujourdhui, client ?? Client(),
            [new LigneCommande("REF", "Article", categorie, montant / quantite, quantite)], code);

    public static Commande CommandeMultiLignes(params LigneCommande[] lignes) =>
        new("CMD-TEST", Aujourdhui, Client(), lignes);

    /// <summary>Évalue une commande avec un moteur dont chaque règle applique inconditionnellement une remise donnée.</summary>
    public static DecisionRemise Decider(decimal brut, PolitiqueRemise? politique, params Remise[] remises)
    {
        var moteur = new MoteurDeRegles<Commande, Remise>();
        for (var i = 0; i < remises.Length; i++)
        {
            var remise = remises[i];
            moteur.Ajouter(Regle.Creer<Commande, Remise>($"R{i}", remise.Motif).Toujours().Alors(_ => remise).Construire());
        }

        return new CalculateurRemise(politique).Calculer(moteur.Evaluer(Commande(brut)));
    }

    public static DecisionRemise Decider(decimal brut, params Remise[] remises) => Decider(brut, null, remises);

    private static readonly Lazy<IReadOnlyList<CasCommande>> JeuDeReference =
        new(() => ChargeurJeuDeDonnees.ChargerFichier(CheminDonnees("commandes.json")));

    public static IReadOnlyList<CasCommande> CasDeReference => JeuDeReference.Value;

    public static CasCommande Cas(string numero) => CasDeReference.Single(c => c.Commande.Numero == numero);
}
