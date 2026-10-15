using MoteurRegles.Remises.Configuration;
using MoteurRegles.Remises.Regles;
using static MoteurRegles.Tests.Donnees;

namespace MoteurRegles.Tests.Remises;

/// <summary>Tests du chargement des règles (FabriqueRegles) et des jeux de données depuis JSON.</summary>
public class ConfigurationTests
{
    [Fact]
    public void Le_fichier_regles_json_est_charge_et_les_regles_inactives_ignorees()
    {
        var configuration = new FabriqueRegles().ChargerFichier(CheminDonnees("regles.json"));

        Assert.Equal(10, configuration.Regles.Count);
        Assert.DoesNotContain(configuration.Regles, r => r.Code == "CMD-VOLUME-ELECTRONIQUE");
        Assert.Equal(30m, configuration.Politique.TauxMaximalCumule);
        Assert.True(configuration.Politique.ArrondiCinqCentimes);
    }

    [Fact]
    public void Les_parametres_du_json_sont_transmis_aux_regles()
    {
        const string json = """
            { "regles": [
                { "type": "ClientFidele", "nombreCommandesMin": 3, "taux": 7.5 },
                { "type": "CodePromo", "code": "ete", "montant": 12, "debut": "2027-06-01", "fin": "2027-08-31" }
            ] }
            """;

        var regles = new FabriqueRegles().Charger(json).Regles;

        var fidele = Assert.IsType<RegleClientFidele>(regles[0]);
        Assert.Equal(3, fidele.NombreCommandesMin);
        Assert.Equal(7.5m, fidele.Taux);

        var promo = Assert.IsType<RegleCodePromo>(regles[1]);
        Assert.Equal("ETE", promo.CodePromo);
        Assert.Equal(12m, promo.RemiseAccordee.Valeur);
        Assert.Equal(new DateOnly(2027, 8, 31), promo.Fin);
    }

    [Fact]
    public void Les_parametres_absents_prennent_leur_valeur_par_defaut()
    {
        var configuration = new FabriqueRegles().Charger("""{ "regles": [ { "type": "clientpremium" } ] }""");

        Assert.Equal(10m, Assert.IsType<RegleClientPremium>(configuration.Regles[0]).Taux);
        Assert.Equal(30m, configuration.Politique.TauxMaximalCumule);
    }

    [Theory]
    [InlineData("""{ "regles": [ { "type": "Inexistante" } ] }""", "Inexistante")]
    [InlineData("""{ "regles": [ { "type": "MontantMinimum" } ] }""", "seuil")]
    [InlineData("""{ "regles": [ { "type": "QuantiteCategorie", "categorie": "Papeterie", "taux": 8 } ] }""", "quantiteMin")]
    [InlineData("""{ "regles": [ { "taux": 5 } ] }""", "type")]
    [InlineData("""{ "regles": [ { "type": "CodePromo", "code": "X" } ] }""", "taux")]
    [InlineData("""{ "regles": [ { "type": "CodePromo", "code": "X", "taux": 5, "debut": "01.10.2026" } ] }""", "Date invalide")]
    [InlineData("""{ "regles": [ { "type": "ClientPremium", "taux": 150 } ] }""", "ClientPremium")]
    [InlineData("""{ "politique": {} }""", "regles")]
    [InlineData("""{ "regles": [ """, "JSON invalide")]
    public void Une_configuration_invalide_produit_une_erreur_explicite(string json, string fragmentMessage)
    {
        var ex = Assert.Throws<ConfigurationInvalideException>(() => new FabriqueRegles().Charger(json));

        Assert.Contains(fragmentMessage, ex.Message);
    }

    [Fact]
    public void Un_nouveau_type_de_regle_peut_etre_enregistre_dans_la_fabrique()
    {
        var fabrique = new FabriqueRegles()
            .Enregistrer("Seuil", p => new RegleGrosseCommande(p.GetProperty("min").GetDecimal(), 2m));

        var regle = fabrique.Charger("""{ "regles": [ { "type": "Seuil", "min": 42 } ] }""").Regles.Single();

        Assert.Equal(42m, Assert.IsType<RegleGrosseCommande>(regle).Seuil);
        Assert.Contains("Seuil", fabrique.TypesConnus);
    }

    [Fact]
    public void Le_jeu_de_donnees_associe_chaque_commande_a_son_client()
    {
        var cas = ChargeurJeuDeDonnees.ChargerFichier(CheminDonnees("commandes.json"));

        Assert.Equal(13, cas.Count);
        var cmd6 = cas.Single(c => c.Commande.Numero == "CMD-006");
        Assert.Equal("Farid Benali", cmd6.Commande.Client.Nom);
        Assert.Equal(3, cmd6.Commande.Lignes.Count);
        Assert.Equal(550.20m, cmd6.Commande.MontantBrut);
        Assert.NotNull(cmd6.Attendu);
    }

    [Fact]
    public void Une_commande_referencant_un_client_inconnu_est_refusee()
    {
        const string json = """{ "clients": [], "commandes": [ { "numero": "X", "date": "2026-10-01", "clientId": "C99" } ] }""";

        var ex = Assert.Throws<ConfigurationInvalideException>(() => ChargeurJeuDeDonnees.Charger(json));
        Assert.Contains("C99", ex.Message);
    }
}
