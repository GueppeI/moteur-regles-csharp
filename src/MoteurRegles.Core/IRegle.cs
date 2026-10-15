namespace MoteurRegles.Core;

/// <summary>
/// Contrat d'une règle métier : « SI condition ALORS effet ».
/// Le moteur ne connaît que cette interface ; il ignore tout du métier.
/// </summary>
/// <typeparam name="TContexte">Données d'entrée évaluées (ex. une commande).</typeparam>
/// <typeparam name="TEffet">Résultat produit quand la règle s'applique (ex. une remise).</typeparam>
public interface IRegle<in TContexte, out TEffet>
{
    /// <summary>Identifiant unique de la règle dans un moteur (ex. « CLIENT-PREMIUM »).</summary>
    string Code { get; }

    /// <summary>Libellé lisible par un humain.</summary>
    string Libelle { get; }

    /// <summary>Ordre d'évaluation : la plus petite valeur est évaluée en premier.</summary>
    int Priorite { get; }

    /// <summary>
    /// Si <c>true</c> et que la règle s'applique, les règles suivantes ne sont pas évaluées
    /// (ex. une règle d'exclusion qui rend tout le reste inutile).
    /// </summary>
    bool ArreteEvaluation { get; }

    /// <summary>Indique si la règle s'applique au contexte.</summary>
    bool EstApplicable(TContexte contexte);

    /// <summary>Produit l'effet de la règle. N'est appelé que si <see cref="EstApplicable"/> a renvoyé <c>true</c>.</summary>
    TEffet ProduireEffet(TContexte contexte);
}
