using MoteurRegles.Remises.Modele;

namespace MoteurRegles.Remises.Regles;

/// <summary>Remise exclusive réservée au personnel (non cumulable avec les autres offres).</summary>
public sealed class RegleEmploye(decimal taux = 20m) : RegleRemise
{
    public decimal Taux { get; } = ValiderTaux(taux);

    public override string Code => "CLIENT-EMPLOYE";

    public override string Libelle => $"Remise collaborateur {Taux} %";

    public override int Priorite => PrioriteClient - 10;

    public override bool EstApplicable(Commande commande) =>
        commande.Client.Categorie == CategorieClient.Employe;

    public override Remise ProduireEffet(Commande commande) =>
        Remise.Pourcentage(Taux, "remise collaborateur", cumulable: false);
}

/// <summary>Remise pour les clients de catégorie Premium.</summary>
public sealed class RegleClientPremium(decimal taux = 10m) : RegleRemise
{
    public decimal Taux { get; } = ValiderTaux(taux);

    public override string Code => "CLIENT-PREMIUM";

    public override string Libelle => $"Client Premium {Taux} %";

    public override int Priorite => PrioriteClient;

    public override bool EstApplicable(Commande commande) =>
        commande.Client.Categorie == CategorieClient.Premium;

    public override Remise ProduireEffet(Commande commande) =>
        Remise.Pourcentage(Taux, "client Premium");
}

/// <summary>Remise de fidélité à partir d'un certain nombre de commandes passées.</summary>
public sealed class RegleClientFidele(int nombreCommandesMin = 10, decimal taux = 5m) : RegleRemise
{
    public int NombreCommandesMin { get; } = nombreCommandesMin;

    public decimal Taux { get; } = ValiderTaux(taux);

    public override string Code => "CLIENT-FIDELE";

    public override string Libelle => $"Fidélité (≥ {NombreCommandesMin} commandes) {Taux} %";

    public override int Priorite => PrioriteClient;

    public override bool EstApplicable(Commande commande) =>
        commande.Client.NombreCommandesPassees >= NombreCommandesMin;

    public override Remise ProduireEffet(Commande commande) =>
        Remise.Pourcentage(Taux, $"fidélité ({commande.Client.NombreCommandesPassees} commandes)");
}

/// <summary>Remise d'ancienneté : client inscrit depuis au moins N années à la date de la commande.</summary>
public sealed class RegleAnciennete(int annees = 3, decimal taux = 3m) : RegleRemise
{
    public int Annees { get; } = annees;

    public decimal Taux { get; } = ValiderTaux(taux);

    public override string Code => "CLIENT-ANCIENNETE";

    public override string Libelle => $"Ancienneté (≥ {Annees} ans) {Taux} %";

    public override int Priorite => PrioriteClient;

    public override bool EstApplicable(Commande commande) =>
        commande.Client.DateInscription.AddYears(Annees) <= commande.Date;

    public override Remise ProduireEffet(Commande commande) =>
        Remise.Pourcentage(Taux, $"ancienneté (client depuis le {commande.Client.DateInscription:dd.MM.yyyy})");
}
