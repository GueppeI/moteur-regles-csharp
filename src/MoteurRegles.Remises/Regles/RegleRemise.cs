using MoteurRegles.Core;
using MoteurRegles.Remises.Modele;

namespace MoteurRegles.Remises.Regles;

/// <summary>
/// Base de toutes les règles de remise. Fixe les types génériques et documente
/// la convention de priorités utilisée dans ce domaine.
/// </summary>
public abstract class RegleRemise : RegleBase<Commande, Remise>
{
    /// <summary>Règles d'exclusion : évaluées en premier, elles arrêtent l'évaluation.</summary>
    public const int PrioriteExclusion = 0;

    /// <summary>Remises liées au profil du client.</summary>
    public const int PrioriteClient = 50;

    /// <summary>Remises liées au contenu de la commande.</summary>
    public const int PrioriteCommande = 60;

    /// <summary>Codes promotionnels.</summary>
    public const int PrioriteCodePromo = 70;

    /// <summary>Vérifie un taux dès la construction, pour qu'une erreur de configuration soit détectée au chargement.</summary>
    protected static decimal ValiderTaux(decimal taux) =>
        taux is > 0 and <= 100
            ? taux
            : throw new ArgumentOutOfRangeException(nameof(taux), taux, "Le taux doit être compris entre 0 (exclu) et 100.");
}
