namespace MoteurRegles.Core;

/// <summary>
/// Évalue les règles sur un contexte et retourne un <see cref="RapportEvaluation{TContexte,TEffet}"/>.
/// </summary>
/// <remarks>
/// Les règles passent par priorité croissante, puis par ordre d'ajout à priorité égale.
/// Une règle peut arrêter l'évaluation ; les suivantes sont alors marquées comme ignorées.
/// Une exception est enregistrée dans la trace sans empêcher l'évaluation des autres règles.
/// </remarks>
public sealed class MoteurDeRegles<TContexte, TEffet>
{
    private readonly List<IRegle<TContexte, TEffet>> _regles = [];

    public MoteurDeRegles()
    {
    }

    public MoteurDeRegles(IEnumerable<IRegle<TContexte, TEffet>> regles) => AjouterPlusieurs(regles);

    /// <summary>Règles dans l'ordre où elles seront évaluées.</summary>
    public IReadOnlyList<IRegle<TContexte, TEffet>> Regles => _regles.OrderBy(r => r.Priorite).ToList();

    public int Nombre => _regles.Count;

    /// <summary>Ajoute une règle. Le code doit être unique (insensible à la casse).</summary>
    /// <exception cref="InvalidOperationException">Une règle avec le même code existe déjà.</exception>
    public MoteurDeRegles<TContexte, TEffet> Ajouter(IRegle<TContexte, TEffet> regle)
    {
        ArgumentNullException.ThrowIfNull(regle);
        if (Contient(regle.Code))
            throw new InvalidOperationException($"Une règle avec le code '{regle.Code}' est déjà enregistrée.");

        _regles.Add(regle);
        return this;
    }

    public MoteurDeRegles<TContexte, TEffet> AjouterPlusieurs(IEnumerable<IRegle<TContexte, TEffet>> regles)
    {
        ArgumentNullException.ThrowIfNull(regles);
        foreach (var regle in regles)
            Ajouter(regle);
        return this;
    }

    /// <summary>Retire une règle par son code. Renvoie <c>false</c> si elle n'existait pas.</summary>
    public bool Retirer(string code) =>
        _regles.RemoveAll(r => string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase)) > 0;

    public bool Contient(string code) =>
        _regles.Exists(r => string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>Évalue toutes les règles sur le contexte donné.</summary>
    public RapportEvaluation<TContexte, TEffet> Evaluer(TContexte contexte)
    {
        ArgumentNullException.ThrowIfNull(contexte);

        var traces = new List<TraceRegle<TEffet>>(_regles.Count);
        string? arreteePar = null;

        foreach (var regle in Regles)
        {
            if (arreteePar is not null)
            {
                traces.Add(new TraceRegle<TEffet>(regle.Code, regle.Libelle, regle.Priorite,
                    StatutRegle.Ignoree, Message: $"évaluation arrêtée par {arreteePar}"));
                continue;
            }

            traces.Add(EvaluerRegle(regle, contexte));

            if (traces[^1].Statut == StatutRegle.Appliquee && regle.ArreteEvaluation)
                arreteePar = regle.Code;
        }

        return new RapportEvaluation<TContexte, TEffet>(contexte, traces);
    }

    private static TraceRegle<TEffet> EvaluerRegle(IRegle<TContexte, TEffet> regle, TContexte contexte)
    {
        try
        {
            if (!regle.EstApplicable(contexte))
                return new TraceRegle<TEffet>(regle.Code, regle.Libelle, regle.Priorite, StatutRegle.NonApplicable);

            var effet = regle.ProduireEffet(contexte);
            return new TraceRegle<TEffet>(regle.Code, regle.Libelle, regle.Priorite, StatutRegle.Appliquee, effet);
        }
        catch (Exception ex)
        {
            return new TraceRegle<TEffet>(regle.Code, regle.Libelle, regle.Priorite, StatutRegle.Erreur,
                Message: $"{ex.GetType().Name} : {ex.Message}");
        }
    }
}
