using MoteurRegles.Remises.Modele;

namespace MoteurRegles.Remises.Regles;

/// <summary>Remise à partir d'un montant brut élevé.</summary>
public sealed class RegleGrosseCommande(decimal seuil = 500m, decimal taux = 5m) : RegleRemise
{
    public decimal Seuil { get; } = seuil;

    public decimal Taux { get; } = ValiderTaux(taux);

    public override string Code => "CMD-GROS-MONTANT";

    public override string Libelle => $"Commande ≥ {Seuil:0.00} CHF : {Taux} %";

    public override int Priorite => PrioriteCommande;

    public override bool EstApplicable(Commande commande) => commande.MontantBrut >= Seuil;

    public override Remise ProduireEffet(Commande commande) =>
        Remise.Pourcentage(Taux, $"commande ≥ {Seuil:0} CHF");
}

/// <summary>Remise sur volume : au moins N articles d'une catégorie donnée.</summary>
public sealed class RegleQuantiteCategorie : RegleRemise
{
    public RegleQuantiteCategorie(string categorie, int quantiteMin, decimal taux)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categorie);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantiteMin);
        Categorie = categorie;
        QuantiteMin = quantiteMin;
        Taux = ValiderTaux(taux);
    }

    public string Categorie { get; }

    public int QuantiteMin { get; }

    public decimal Taux { get; }

    // Le code inclut la catégorie : plusieurs instances (une par catégorie) peuvent cohabiter dans le moteur.
    public override string Code => $"CMD-VOLUME-{Categorie.ToUpperInvariant()}";

    public override string Libelle => $"≥ {QuantiteMin} articles « {Categorie} » : {Taux} %";

    public override int Priorite => PrioriteCommande;

    public override bool EstApplicable(Commande commande) => commande.NombreArticles(Categorie) >= QuantiteMin;

    public override Remise ProduireEffet(Commande commande) =>
        Remise.Pourcentage(Taux, $"{commande.NombreArticles(Categorie)} articles {Categorie}");
}

/// <summary>
/// Code promotionnel saisi par le client, avec période de validité optionnelle.
/// Une instance par code : ajouter une campagne = ajouter une instance (ou une ligne de configuration).
/// </summary>
public sealed class RegleCodePromo : RegleRemise
{
    public RegleCodePromo(string code, Remise remise, DateOnly? debut = null, DateOnly? fin = null,
        bool premiereCommandeUniquement = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(remise);
        if (remise.Type == TypeRemise.Exclusion)
            throw new ArgumentException("Un code promo ne peut pas produire une exclusion.", nameof(remise));
        if (debut > fin)
            throw new ArgumentException("La date de début doit précéder la date de fin.", nameof(debut));

        CodePromo = code.Trim().ToUpperInvariant();
        RemiseAccordee = remise;
        Debut = debut;
        Fin = fin;
        PremiereCommandeUniquement = premiereCommandeUniquement;
    }

    public string CodePromo { get; }

    public Remise RemiseAccordee { get; }

    public DateOnly? Debut { get; }

    public DateOnly? Fin { get; }

    public bool PremiereCommandeUniquement { get; }

    public override string Code => $"PROMO-{CodePromo}";

    public override string Libelle => $"Code promo {CodePromo}";

    public override int Priorite => PrioriteCodePromo;

    public override bool EstApplicable(Commande commande) =>
        string.Equals(commande.CodePromo?.Trim(), CodePromo, StringComparison.OrdinalIgnoreCase)
        && (Debut is null || commande.Date >= Debut)
        && (Fin is null || commande.Date <= Fin)
        && (!PremiereCommandeUniquement || commande.Client.NombreCommandesPassees == 0);

    public override Remise ProduireEffet(Commande commande) => RemiseAccordee;
}
