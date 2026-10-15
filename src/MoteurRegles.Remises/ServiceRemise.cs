using MoteurRegles.Core;
using MoteurRegles.Remises.Modele;
using MoteurRegles.Remises.Regles;

namespace MoteurRegles.Remises;

/// <summary>
/// Façade du domaine : évalue une commande avec le moteur puis calcule la décision.
/// </summary>
public sealed class ServiceRemise(MoteurDeRegles<Commande, Remise> moteur, CalculateurRemise? calculateur = null)
{
    public MoteurDeRegles<Commande, Remise> Moteur { get; } = moteur ?? throw new ArgumentNullException(nameof(moteur));

    public CalculateurRemise Calculateur { get; } = calculateur ?? new CalculateurRemise();

    public DecisionRemise Evaluer(Commande commande) => Calculateur.Calculer(Moteur.Evaluer(commande));

    public IReadOnlyList<DecisionRemise> EvaluerTout(IEnumerable<Commande> commandes) =>
        commandes.Select(Evaluer).ToList();
}

/// <summary>
/// Jeu de règles convenu pour le cas métier « éligibilité à une remise » (boutique en ligne de fournitures).
/// Le même jeu est décrit dans <c>donnees/regles.json</c> pour un chargement par configuration.
/// </summary>
public static class CatalogueReglesRemise
{
    public static readonly DateOnly DebutAutomne = new(2026, 9, 22);
    public static readonly DateOnly FinAutomne = new(2026, 12, 20);

    public static IEnumerable<IRegle<Commande, Remise>> ReglesParDefaut()
    {
        yield return new RegleClientAvecImpayes();
        yield return new RegleMontantMinimum(30m);
        yield return new RegleEmploye(20m);
        yield return new RegleClientPremium(10m);
        yield return new RegleClientFidele(10, 5m);
        yield return new RegleAnciennete(3, 3m);
        yield return new RegleGrosseCommande(500m, 5m);
        yield return new RegleQuantiteCategorie("Papeterie", 4, 8m);
        yield return new RegleCodePromo("BIENVENUE10", Remise.MontantFixe(10m, "code BIENVENUE10"),
            premiereCommandeUniquement: true);
        yield return new RegleCodePromo("AUTOMNE15", Remise.Pourcentage(15m, "code AUTOMNE15", cumulable: false),
            DebutAutomne, FinAutomne);
    }

    public static MoteurDeRegles<Commande, Remise> CreerMoteur() => new(ReglesParDefaut());

    public static ServiceRemise CreerService(PolitiqueRemise? politique = null) =>
        new(CreerMoteur(), new CalculateurRemise(politique));
}
