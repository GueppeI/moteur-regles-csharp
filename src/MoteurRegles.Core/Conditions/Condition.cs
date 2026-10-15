namespace MoteurRegles.Core.Conditions;

/// <summary>
/// Condition élémentaire construite à partir d'une expression lambda.
/// </summary>
public sealed class Condition<TContexte> : ICondition<TContexte>
{
    private readonly Func<TContexte, bool> _predicat;

    public Condition(string description, Func<TContexte, bool> predicat)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(predicat);
        Description = description;
        _predicat = predicat;
    }

    public string Description { get; }

    public bool EstSatisfaite(TContexte contexte) => _predicat(contexte);

    /// <summary>Condition toujours vraie (utile pour une règle inconditionnelle).</summary>
    public static Condition<TContexte> Toujours { get; } = new("toujours", _ => true);

    public override string ToString() => Description;
}

/// <summary>Conjonction logique : vraie si les deux conditions sont vraies.</summary>
internal sealed class ConditionEt<TContexte>(ICondition<TContexte> gauche, ICondition<TContexte> droite)
    : ICondition<TContexte>
{
    public string Description => $"({gauche.Description} ET {droite.Description})";

    public bool EstSatisfaite(TContexte contexte) =>
        gauche.EstSatisfaite(contexte) && droite.EstSatisfaite(contexte);
}

/// <summary>Disjonction logique : vraie si au moins une des deux conditions est vraie.</summary>
internal sealed class ConditionOu<TContexte>(ICondition<TContexte> gauche, ICondition<TContexte> droite)
    : ICondition<TContexte>
{
    public string Description => $"({gauche.Description} OU {droite.Description})";

    public bool EstSatisfaite(TContexte contexte) =>
        gauche.EstSatisfaite(contexte) || droite.EstSatisfaite(contexte);
}

/// <summary>Négation logique.</summary>
internal sealed class ConditionNon<TContexte>(ICondition<TContexte> condition) : ICondition<TContexte>
{
    public string Description => $"NON {condition.Description}";

    public bool EstSatisfaite(TContexte contexte) => !condition.EstSatisfaite(contexte);
}

/// <summary>
/// Opérateurs de composition : <c>a.Et(b).Ou(c.Non())</c>.
/// </summary>
public static class ConditionExtensions
{
    public static ICondition<T> Et<T>(this ICondition<T> gauche, ICondition<T> droite)
    {
        ArgumentNullException.ThrowIfNull(gauche);
        ArgumentNullException.ThrowIfNull(droite);
        return new ConditionEt<T>(gauche, droite);
    }

    public static ICondition<T> Ou<T>(this ICondition<T> gauche, ICondition<T> droite)
    {
        ArgumentNullException.ThrowIfNull(gauche);
        ArgumentNullException.ThrowIfNull(droite);
        return new ConditionOu<T>(gauche, droite);
    }

    public static ICondition<T> Non<T>(this ICondition<T> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return new ConditionNon<T>(condition);
    }
}
