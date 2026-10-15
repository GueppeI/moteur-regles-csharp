using MoteurRegles.Remises.Modele;

namespace MoteurRegles.Remises.Regles;

/// <summary>Un client ayant des factures impayées n'a droit à aucune remise.</summary>
public sealed class RegleClientAvecImpayes : RegleRemise
{
    public override string Code => "EXCL-IMPAYES";

    public override string Libelle => "Client avec factures impayées";

    public override int Priorite => PrioriteExclusion;

    public override bool ArreteEvaluation => true;

    public override bool EstApplicable(Commande commande) => commande.Client.ADesImpayes;

    public override Remise ProduireEffet(Commande commande) =>
        Remise.Exclusion($"le client {commande.Client.Id} a des factures impayées");
}

/// <summary>Aucune remise en dessous d'un montant brut minimum.</summary>
public sealed class RegleMontantMinimum(decimal seuil) : RegleRemise
{
    public decimal Seuil { get; } = seuil >= 0
        ? seuil
        : throw new ArgumentOutOfRangeException(nameof(seuil), "Le seuil ne peut pas être négatif.");

    public override string Code => "EXCL-MONTANT-MIN";

    public override string Libelle => $"Montant brut inférieur à {Seuil:0.00} CHF";

    public override int Priorite => PrioriteExclusion + 10;

    public override bool ArreteEvaluation => true;

    public override bool EstApplicable(Commande commande) => commande.MontantBrut < Seuil;

    public override Remise ProduireEffet(Commande commande) =>
        Remise.Exclusion($"montant brut {commande.MontantBrut:0.00} CHF < minimum {Seuil:0.00} CHF");
}
