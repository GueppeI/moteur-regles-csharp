using System.Text;

namespace MoteurRegles.Core;

/// <summary>Issue de l'évaluation d'une règle.</summary>
public enum StatutRegle
{
    /// <summary>La condition était vraie : un effet a été produit.</summary>
    Appliquee,

    /// <summary>La condition était fausse.</summary>
    NonApplicable,

    /// <summary>Non évaluée car une règle précédente a arrêté l'évaluation.</summary>
    Ignoree,

    /// <summary>La règle a levé une exception ; elle est isolée sans bloquer les autres.</summary>
    Erreur,
}

/// <summary>Trace d'une règle dans un rapport (pour l'audit et l'explication de la décision).</summary>
public sealed record TraceRegle<TEffet>(
    string Code,
    string Libelle,
    int Priorite,
    StatutRegle Statut,
    TEffet? Effet = default,
    string? Message = null)
{
    public override string ToString()
    {
        var detail = Statut switch
        {
            StatutRegle.Appliquee => $"→ {Effet}",
            StatutRegle.Ignoree or StatutRegle.Erreur => $"({Message})",
            _ => string.Empty,
        };
        return $"[{Statut,-13}] {Code,-22} {detail}".TrimEnd();
    }
}

/// <summary>
/// Résultat complet de l'évaluation d'un contexte : effets produits et trace de chaque règle.
/// </summary>
public sealed class RapportEvaluation<TContexte, TEffet>
{
    public RapportEvaluation(TContexte contexte, IReadOnlyList<TraceRegle<TEffet>> traces)
    {
        Contexte = contexte;
        Traces = traces;
    }

    public TContexte Contexte { get; }

    /// <summary>Une trace par règle du moteur, dans l'ordre d'évaluation.</summary>
    public IReadOnlyList<TraceRegle<TEffet>> Traces { get; }

    /// <summary>Effets des règles appliquées, dans l'ordre d'évaluation.</summary>
    public IEnumerable<TEffet> Effets =>
        Traces.Where(t => t.Statut == StatutRegle.Appliquee).Select(t => t.Effet!);

    public IEnumerable<string> CodesAppliques =>
        Traces.Where(t => t.Statut == StatutRegle.Appliquee).Select(t => t.Code);

    public bool ContientErreurs => Traces.Any(t => t.Statut == StatutRegle.Erreur);

    public bool EvaluationInterrompue => Traces.Any(t => t.Statut == StatutRegle.Ignoree);

    public TraceRegle<TEffet>? Trace(string code) =>
        Traces.FirstOrDefault(t => string.Equals(t.Code, code, StringComparison.OrdinalIgnoreCase));

    public string Formater()
    {
        var sb = new StringBuilder();
        foreach (var trace in Traces)
            sb.AppendLine(trace.ToString());
        return sb.ToString();
    }
}
