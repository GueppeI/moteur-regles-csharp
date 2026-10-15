namespace MoteurRegles.Core.Conditions;

/// <summary>
/// Prédicat métier nommé et composable (patron « Specification »).
/// Une condition répond uniquement à la question « est-ce vrai pour ce contexte ? ».
/// </summary>
/// <typeparam name="TContexte">Type des données évaluées (ex. une commande).</typeparam>
public interface ICondition<in TContexte>
{
    /// <summary>Description lisible, utilisée dans les traces d'évaluation.</summary>
    string Description { get; }

    /// <summary>Indique si la condition est vraie pour le contexte donné.</summary>
    bool EstSatisfaite(TContexte contexte);
}
