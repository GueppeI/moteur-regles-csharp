namespace MoteurRegles.Core;

/// <summary>
/// Classe de base facultative pour écrire une règle : fournit des valeurs par défaut
/// (priorité 100, pas d'arrêt) afin que chaque règle concrète ne déclare que l'essentiel.
/// </summary>
public abstract class RegleBase<TContexte, TEffet> : IRegle<TContexte, TEffet>
{
    public const int PrioriteParDefaut = 100;

    public abstract string Code { get; }

    public abstract string Libelle { get; }

    public virtual int Priorite => PrioriteParDefaut;

    public virtual bool ArreteEvaluation => false;

    public abstract bool EstApplicable(TContexte contexte);

    public abstract TEffet ProduireEffet(TContexte contexte);

    public override string ToString() => $"[{Code}] {Libelle} (priorité {Priorite})";
}
