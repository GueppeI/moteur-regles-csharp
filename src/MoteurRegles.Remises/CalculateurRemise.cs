using MoteurRegles.Core;
using MoteurRegles.Remises.Modele;

namespace MoteurRegles.Remises;

/// <summary>Paramètres de la politique commerciale appliquée aux remises produites par les règles.</summary>
public sealed record PolitiqueRemise
{
    /// <summary>Taux cumulé maximum (en %) des remises en pourcentage.</summary>
    public decimal TauxMaximalCumule { get; init; } = 30m;

    /// <summary>Arrondir le montant de la remise aux 5 centimes (usage suisse).</summary>
    public bool ArrondiCinqCentimes { get; init; } = true;

    public static PolitiqueRemise ParDefaut { get; } = new();
}

/// <summary>Décision finale pour une commande, avec son explication.</summary>
public sealed record DecisionRemise(
    string NumeroCommande,
    bool Eligible,
    decimal MontantBrut,
    decimal MontantRemise,
    IReadOnlyList<Remise> RemisesRetenues,
    IReadOnlyList<Remise> RemisesEcartees,
    IReadOnlyList<string> Explications,
    RapportEvaluation<Commande, Remise> Rapport)
{
    public decimal MontantNet => MontantBrut - MontantRemise;

    public decimal TauxEffectif => MontantBrut == 0 ? 0 : Math.Round(MontantRemise / MontantBrut * 100, 2);
}

/// <summary>
/// Calcule la remise finale à partir des effets produits par le moteur.
/// </summary>
/// <remarks>
/// Une exclusion bloque toute remise. Sinon, le cumul des offres est comparé à la meilleure
/// offre exclusive ; à égalité, le cumul est retenu. Le plafond et l'arrondi suivent la politique.
/// </remarks>
public sealed class CalculateurRemise(PolitiqueRemise? politique = null)
{
    public PolitiqueRemise Politique { get; } = politique ?? PolitiqueRemise.ParDefaut;

    public DecisionRemise Calculer(RapportEvaluation<Commande, Remise> rapport)
    {
        ArgumentNullException.ThrowIfNull(rapport);

        var commande = rapport.Contexte;
        var brut = commande.MontantBrut;
        var effets = rapport.Effets.ToList();
        var explications = new List<string>();

        if (rapport.ContientErreurs)
        {
            foreach (var erreur in rapport.Traces.Where(t => t.Statut == StatutRegle.Erreur))
                explications.Add($"Règle {erreur.Code} ignorée suite à une erreur : {erreur.Message}");
        }

        var exclusions = effets.Where(r => r.Type == TypeRemise.Exclusion).ToList();
        if (exclusions.Count > 0)
        {
            explications.AddRange(exclusions.Select(e => $"Non éligible : {e.Motif}"));
            return new DecisionRemise(commande.Numero, false, brut, 0m, [], [], explications, rapport);
        }

        var cumulables = effets.Where(r => r.Cumulable).ToList();
        var exclusives = effets.Where(r => !r.Cumulable).ToList();

        var montantCumul = MontantCumule(cumulables, brut, explications);
        var meilleureExclusive = exclusives.MaxBy(r => MontantUnitaire(r, brut));
        var montantExclusive = meilleureExclusive is null ? 0m : MontantUnitaire(meilleureExclusive, brut);

        List<Remise> retenues;
        decimal montant;
        if (meilleureExclusive is not null && montantExclusive > montantCumul)
        {
            retenues = [meilleureExclusive];
            montant = montantExclusive;
            if (cumulables.Count > 0)
                explications.Add($"Offre exclusive « {meilleureExclusive.Motif} » ({montantExclusive:0.00} CHF) plus avantageuse que le cumul ({montantCumul:0.00} CHF).");
        }
        else
        {
            retenues = cumulables;
            montant = montantCumul;
            if (meilleureExclusive is not null)
                explications.Add($"Cumul des offres ({montantCumul:0.00} CHF) au moins aussi avantageux que l'offre exclusive « {meilleureExclusive.Motif} » ({montantExclusive:0.00} CHF).");
        }

        montant = Arrondir(Math.Min(montant, brut));
        var ecartees = effets.Where(e => !retenues.Any(r => ReferenceEquals(r, e))).ToList();

        if (retenues.Count == 0)
            explications.Add("Aucune règle de remise ne s'applique.");

        return new DecisionRemise(commande.Numero, true, brut, montant, retenues, ecartees, explications, rapport);
    }

    private decimal MontantCumule(IReadOnlyCollection<Remise> cumulables, decimal brut, List<string> explications)
    {
        var tauxTotal = cumulables.Where(r => r.Type == TypeRemise.Pourcentage).Sum(r => r.Valeur);
        if (tauxTotal > Politique.TauxMaximalCumule)
        {
            explications.Add($"Taux cumulé {tauxTotal} % plafonné à {Politique.TauxMaximalCumule} %.");
            tauxTotal = Politique.TauxMaximalCumule;
        }

        var fixes = cumulables.Where(r => r.Type == TypeRemise.MontantFixe).Sum(r => r.Valeur);
        return brut * tauxTotal / 100m + fixes;
    }

    private decimal MontantUnitaire(Remise remise, decimal brut) => remise.Type switch
    {
        TypeRemise.Pourcentage => brut * Math.Min(remise.Valeur, Politique.TauxMaximalCumule) / 100m,
        TypeRemise.MontantFixe => remise.Valeur,
        _ => 0m,
    };

    private decimal Arrondir(decimal montant) =>
        Politique.ArrondiCinqCentimes
            ? Math.Round(montant * 20m, MidpointRounding.AwayFromZero) / 20m
            : Math.Round(montant, 2, MidpointRounding.AwayFromZero);
}
