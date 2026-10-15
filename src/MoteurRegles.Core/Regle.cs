using MoteurRegles.Core.Conditions;

namespace MoteurRegles.Core;

/// <summary>
/// Règle générique construite par composition (condition + fabrique d'effet),
/// sans avoir à écrire de classe dédiée. Se crée via <see cref="Regle.Creer{TContexte,TEffet}"/>.
/// </summary>
public sealed class Regle<TContexte, TEffet> : RegleBase<TContexte, TEffet>
{
    private readonly Func<TContexte, TEffet> _effet;

    internal Regle(string code, string libelle, ICondition<TContexte> condition,
        Func<TContexte, TEffet> effet, int priorite, bool arreteEvaluation)
    {
        Code = code;
        Libelle = libelle;
        Condition = condition;
        _effet = effet;
        Priorite = priorite;
        ArreteEvaluation = arreteEvaluation;
    }

    public override string Code { get; }

    public override string Libelle { get; }

    public override int Priorite { get; }

    public override bool ArreteEvaluation { get; }

    public ICondition<TContexte> Condition { get; }

    public override bool EstApplicable(TContexte contexte) => Condition.EstSatisfaite(contexte);

    public override TEffet ProduireEffet(TContexte contexte) => _effet(contexte);
}

/// <summary>Point d'entrée de l'API fluide de création de règles.</summary>
public static class Regle
{
    /// <example>
    /// <code>
    /// var regle = Regle.Creer&lt;Commande, Remise&gt;("NOEL", "Remise de Noël")
    ///     .Quand("commande en décembre", c =&gt; c.Date.Month == 12)
    ///     .Alors(_ =&gt; Remise.Pourcentage(5, "Noël"))
    ///     .Construire();
    /// </code>
    /// </example>
    public static ConstructeurRegle<TContexte, TEffet> Creer<TContexte, TEffet>(string code, string libelle) =>
        new(code, libelle);
}

/// <summary>Constructeur (patron « Builder ») d'une <see cref="Regle{TContexte,TEffet}"/>.</summary>
public sealed class ConstructeurRegle<TContexte, TEffet>
{
    private readonly string _code;
    private readonly string _libelle;
    private ICondition<TContexte>? _condition;
    private Func<TContexte, TEffet>? _effet;
    private int _priorite = RegleBase<TContexte, TEffet>.PrioriteParDefaut;
    private bool _arrete;

    internal ConstructeurRegle(string code, string libelle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(libelle);
        _code = code;
        _libelle = libelle;
    }

    public ConstructeurRegle<TContexte, TEffet> Quand(ICondition<TContexte> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        _condition = _condition is null ? condition : _condition.Et(condition);
        return this;
    }

    public ConstructeurRegle<TContexte, TEffet> Quand(string description, Func<TContexte, bool> predicat) =>
        Quand(new Condition<TContexte>(description, predicat));

    public ConstructeurRegle<TContexte, TEffet> Toujours() => Quand(Condition<TContexte>.Toujours);

    public ConstructeurRegle<TContexte, TEffet> Alors(Func<TContexte, TEffet> effet)
    {
        ArgumentNullException.ThrowIfNull(effet);
        _effet = effet;
        return this;
    }

    public ConstructeurRegle<TContexte, TEffet> AvecPriorite(int priorite)
    {
        _priorite = priorite;
        return this;
    }

    public ConstructeurRegle<TContexte, TEffet> ArreterSiAppliquee()
    {
        _arrete = true;
        return this;
    }

    public Regle<TContexte, TEffet> Construire()
    {
        if (_condition is null)
            throw new InvalidOperationException($"La règle '{_code}' n'a pas de condition (appeler Quand(...) ou Toujours()).");
        if (_effet is null)
            throw new InvalidOperationException($"La règle '{_code}' n'a pas d'effet (appeler Alors(...)).");

        return new Regle<TContexte, TEffet>(_code, _libelle, _condition, _effet, _priorite, _arrete);
    }
}
