using System.Globalization;
using System.Text.Json;
using MoteurRegles.Core;
using MoteurRegles.Remises.Modele;
using MoteurRegles.Remises.Regles;

namespace MoteurRegles.Remises.Configuration;

/// <summary>Résultat du chargement d'un fichier de configuration.</summary>
public sealed record ConfigurationRemises(
    PolitiqueRemise Politique,
    IReadOnlyList<IRegle<Commande, Remise>> Regles)
{
    public ServiceRemise CreerService() =>
        new(new MoteurDeRegles<Commande, Remise>(Regles), new CalculateurRemise(Politique));
}

/// <summary>
/// Charge les règles depuis le JSON. Chaque type est associé à une fonction de création.
/// </summary>
/// <remarks>
/// Le JSON permet de changer les paramètres et l'état actif des règles existantes.
/// Pour ajouter un type, associer son nom à une fonction avec <see cref="Enregistrer"/>.
/// </remarks>
public sealed class FabriqueRegles
{
    private readonly Dictionary<string, Func<JsonElement, IRegle<Commande, Remise>>> _createurs =
        new(StringComparer.OrdinalIgnoreCase);

    public FabriqueRegles()
    {
        Enregistrer("ClientAvecImpayes", _ => new RegleClientAvecImpayes());
        Enregistrer("MontantMinimum", p => new RegleMontantMinimum(LireDecimal(p, "seuil")));
        Enregistrer("Employe", p => new RegleEmploye(LireDecimal(p, "taux", 20m)));
        Enregistrer("ClientPremium", p => new RegleClientPremium(LireDecimal(p, "taux", 10m)));
        Enregistrer("ClientFidele", p => new RegleClientFidele(LireEntier(p, "nombreCommandesMin", 10), LireDecimal(p, "taux", 5m)));
        Enregistrer("Anciennete", p => new RegleAnciennete(LireEntier(p, "annees", 3), LireDecimal(p, "taux", 3m)));
        Enregistrer("GrosseCommande", p => new RegleGrosseCommande(LireDecimal(p, "seuil", 500m), LireDecimal(p, "taux", 5m)));
        Enregistrer("QuantiteCategorie", p => new RegleQuantiteCategorie(
            LireTexte(p, "categorie"), LireEntier(p, "quantiteMin"), LireDecimal(p, "taux")));
        Enregistrer("CodePromo", CreerCodePromo);
    }

    public IReadOnlyCollection<string> TypesConnus => _createurs.Keys;

    /// <summary>Associe un nom de type (champ <c>"type"</c> du JSON) à une fonction de création.</summary>
    public FabriqueRegles Enregistrer(string type, Func<JsonElement, IRegle<Commande, Remise>> createur)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(createur);
        _createurs[type] = createur;
        return this;
    }

    public IRegle<Commande, Remise> Creer(JsonElement definition)
    {
        var type = LireTexte(definition, "type");
        if (!_createurs.TryGetValue(type, out var createur))
            throw new ConfigurationInvalideException(
                $"Type de règle inconnu : '{type}'. Types connus : {string.Join(", ", _createurs.Keys)}.");

        try
        {
            return createur(definition);
        }
        catch (Exception ex) when (ex is not ConfigurationInvalideException)
        {
            throw new ConfigurationInvalideException($"Paramètres invalides pour la règle '{type}' : {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Charge une configuration complète. Les règles marquées <c>"actif": false</c> sont ignorées.
    /// </summary>
    public ConfigurationRemises Charger(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException ex)
        {
            throw new ConfigurationInvalideException($"JSON invalide : {ex.Message}", ex);
        }

        using (document)
        {
            var racine = document.RootElement;

            var politique = PolitiqueRemise.ParDefaut;
            if (racine.TryGetProperty("politique", out var p))
            {
                politique = new PolitiqueRemise
                {
                    TauxMaximalCumule = LireDecimal(p, "tauxMaximalCumule", politique.TauxMaximalCumule),
                    ArrondiCinqCentimes = LireBooleen(p, "arrondiCinqCentimes", politique.ArrondiCinqCentimes),
                };
            }

            if (!racine.TryGetProperty("regles", out var regles) || regles.ValueKind != JsonValueKind.Array)
                throw new ConfigurationInvalideException("La configuration doit contenir un tableau 'regles'.");

            var liste = regles.EnumerateArray()
                .Where(r => LireBooleen(r, "actif", true))
                .Select(Creer)
                .ToList();

            return new ConfigurationRemises(politique, liste);
        }
    }

    public ConfigurationRemises ChargerFichier(string chemin) => Charger(File.ReadAllText(chemin));

    private static IRegle<Commande, Remise> CreerCodePromo(JsonElement p)
    {
        var code = LireTexte(p, "code");
        var cumulable = LireBooleen(p, "cumulable", true);
        var motif = $"code {code.ToUpperInvariant()}";

        Remise remise;
        if (p.TryGetProperty("taux", out _))
            remise = Remise.Pourcentage(LireDecimal(p, "taux"), motif, cumulable);
        else if (p.TryGetProperty("montant", out _))
            remise = Remise.MontantFixe(LireDecimal(p, "montant"), motif, cumulable);
        else
            throw new ConfigurationInvalideException($"Le code promo '{code}' doit définir 'taux' ou 'montant'.");

        return new RegleCodePromo(code, remise, LireDate(p, "debut"), LireDate(p, "fin"),
            LireBooleen(p, "premiereCommandeUniquement", false));
    }

    // --- Lecture des paramètres ---------------------------------------------------------------

    private static string LireTexte(JsonElement e, string nom) =>
        e.TryGetProperty(nom, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!
            : throw new ConfigurationInvalideException($"Paramètre texte obligatoire manquant : '{nom}'.");

    private static decimal LireDecimal(JsonElement e, string nom, decimal? defaut = null)
    {
        if (e.TryGetProperty(nom, out var v) && v.ValueKind == JsonValueKind.Number)
            return v.GetDecimal();
        return defaut ?? throw new ConfigurationInvalideException($"Paramètre numérique obligatoire manquant : '{nom}'.");
    }

    private static int LireEntier(JsonElement e, string nom, int? defaut = null)
    {
        if (e.TryGetProperty(nom, out var v) && v.ValueKind == JsonValueKind.Number)
            return v.GetInt32();
        return defaut ?? throw new ConfigurationInvalideException($"Paramètre entier obligatoire manquant : '{nom}'.");
    }

    private static bool LireBooleen(JsonElement e, string nom, bool defaut) =>
        e.TryGetProperty(nom, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? v.GetBoolean()
            : defaut;

    private static DateOnly? LireDate(JsonElement e, string nom)
    {
        if (!e.TryGetProperty(nom, out var v) || v.ValueKind == JsonValueKind.Null)
            return null;
        if (v.ValueKind == JsonValueKind.String
            && DateOnly.TryParseExact(v.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        throw new ConfigurationInvalideException($"Date invalide pour '{nom}' (format attendu : yyyy-MM-dd).");
    }
}

public sealed class ConfigurationInvalideException(string message, Exception? inner = null)
    : Exception(message, inner);
