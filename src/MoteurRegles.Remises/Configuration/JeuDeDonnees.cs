using System.Text.Json;
using System.Text.Json.Serialization;
using MoteurRegles.Remises.Modele;

namespace MoteurRegles.Remises.Configuration;

/// <summary>Résultat attendu pour une commande d'un jeu de données (sert de référence aux tests).</summary>
public sealed record ResultatAttendu(bool Eligible, decimal MontantRemise, string? Commentaire = null);

/// <summary>Une commande d'un jeu de données et, éventuellement, le résultat attendu.</summary>
public sealed record CasCommande(Commande Commande, ResultatAttendu? Attendu)
{
    public override string ToString() => Commande.Numero;
}

/// <summary>
/// Charge un jeu de données JSON (clients + commandes) et le convertit en objets du domaine.
/// </summary>
public static class ChargeurJeuDeDonnees
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static IReadOnlyList<CasCommande> Charger(string json)
    {
        var dto = JsonSerializer.Deserialize<JeuDto>(json, Options)
                  ?? throw new ConfigurationInvalideException("Jeu de données vide.");

        var clients = (dto.Clients ?? []).ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);

        return (dto.Commandes ?? []).Select(c =>
        {
            if (!clients.TryGetValue(c.ClientId, out var client))
                throw new ConfigurationInvalideException($"Commande {c.Numero} : client '{c.ClientId}' introuvable.");
            var commande = new Commande(c.Numero, c.Date, client, c.Lignes ?? [], c.CodePromo);
            return new CasCommande(commande, c.Attendu);
        }).ToList();
    }

    public static IReadOnlyList<CasCommande> ChargerFichier(string chemin) => Charger(File.ReadAllText(chemin));

    private sealed record JeuDto(List<Client>? Clients, List<CommandeDto>? Commandes);

    private sealed record CommandeDto(
        string Numero,
        DateOnly Date,
        string ClientId,
        List<LigneCommande>? Lignes,
        string? CodePromo,
        ResultatAttendu? Attendu);
}
