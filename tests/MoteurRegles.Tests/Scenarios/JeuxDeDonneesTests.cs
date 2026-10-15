using MoteurRegles.Core;
using MoteurRegles.Remises;
using MoteurRegles.Remises.Configuration;
using static MoteurRegles.Tests.Donnees;

namespace MoteurRegles.Tests.Scenarios;

/// <summary>
/// Tests de bout en bout sur le jeu de données de référence (<c>donnees/commandes.json</c>) :
/// chaque commande est évaluée et comparée au résultat attendu inscrit dans le fichier.
/// </summary>
public class JeuxDeDonneesTests
{
    public static TheoryData<string> NumerosDeCommande()
    {
        var donnees = new TheoryData<string>();
        foreach (var cas in CasDeReference)
            donnees.Add(cas.Commande.Numero);
        return donnees;
    }

    [Theory]
    [MemberData(nameof(NumerosDeCommande))]
    public void Catalogue_par_defaut_resultat_conforme_au_jeu_de_donnees(string numero)
    {
        var (commande, attendu) = Cas(numero);

        var decision = CatalogueReglesRemise.CreerService().Evaluer(commande);

        Assert.NotNull(attendu);
        Assert.Equal(attendu.Eligible, decision.Eligible);
        Assert.Equal(attendu.MontantRemise, decision.MontantRemise);
        Assert.Equal(decision.MontantBrut - attendu.MontantRemise, decision.MontantNet);
    }

    [Theory]
    [MemberData(nameof(NumerosDeCommande))]
    public void Configuration_json_resultat_conforme_au_jeu_de_donnees(string numero)
    {
        var (commande, attendu) = Cas(numero);
        var service = new FabriqueRegles().ChargerFichier(CheminDonnees("regles.json")).CreerService();

        var decision = service.Evaluer(commande);

        Assert.Equal(attendu!.Eligible, decision.Eligible);
        Assert.Equal(attendu.MontantRemise, decision.MontantRemise);
    }

    [Fact]
    public void Le_catalogue_code_et_le_fichier_json_definissent_les_memes_regles_dans_le_meme_ordre()
    {
        var parCode = CatalogueReglesRemise.CreerMoteur().Regles.Select(r => (r.Code, r.Priorite));
        var parJson = new FabriqueRegles().ChargerFichier(CheminDonnees("regles.json")).CreerService()
            .Moteur.Regles.Select(r => (r.Code, r.Priorite));

        Assert.Equal(parCode, parJson);
    }

    [Fact]
    public void CMD_002_client_avec_impayes_les_autres_regles_ne_sont_pas_evaluees()
    {
        var decision = CatalogueReglesRemise.CreerService().Evaluer(Cas("CMD-002").Commande);

        Assert.Equal(["EXCL-IMPAYES"], decision.Rapport.CodesAppliques);
        Assert.All(decision.Rapport.Traces.Skip(1), t => Assert.Equal(StatutRegle.Ignoree, t.Statut));
    }

    [Fact]
    public void CMD_006_cinq_remises_cumulees_puis_plafonnees_a_30_pourcent()
    {
        var decision = CatalogueReglesRemise.CreerService().Evaluer(Cas("CMD-006").Commande);

        Assert.Equal(5, decision.RemisesRetenues.Count);
        Assert.Contains(decision.Explications, e => e.Contains("31") && e.Contains("plafonné"));
        Assert.Equal(385.15m, decision.MontantNet);
    }

    [Fact]
    public void CMD_007_la_remise_collaborateur_exclusive_ecarte_le_cumul()
    {
        var decision = CatalogueReglesRemise.CreerService().Evaluer(Cas("CMD-007").Commande);

        Assert.Equal("remise collaborateur", Assert.Single(decision.RemisesRetenues).Motif);
        Assert.Equal(2, decision.RemisesEcartees.Count);
    }

    [Fact]
    public void CMD_009_code_promo_hors_periode_est_tracé_comme_non_applicable()
    {
        var decision = CatalogueReglesRemise.CreerService().Evaluer(Cas("CMD-009").Commande);

        Assert.Equal(StatutRegle.NonApplicable, decision.Rapport.Trace("PROMO-AUTOMNE15")!.Statut);
        Assert.Equal(0m, decision.MontantRemise);
    }

    [Fact]
    public void Le_service_evalue_un_lot_de_commandes()
    {
        var decisions = CatalogueReglesRemise.CreerService().EvaluerTout(CasDeReference.Select(c => c.Commande));

        Assert.Equal(13, decisions.Count);
        Assert.Equal(2, decisions.Count(d => !d.Eligible));
        Assert.Equal(637.85m, decisions.Sum(d => d.MontantRemise));
    }
}
