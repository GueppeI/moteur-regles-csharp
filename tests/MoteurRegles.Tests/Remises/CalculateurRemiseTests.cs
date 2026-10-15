using MoteurRegles.Core;
using MoteurRegles.Remises;
using MoteurRegles.Remises.Modele;
using static MoteurRegles.Tests.Donnees;

namespace MoteurRegles.Tests.Remises;

/// <summary>Tests de la politique commerciale : cumul, plafond, offres exclusives, arrondi.</summary>
public class CalculateurRemiseTests
{
    [Fact]
    public void Sans_remise_la_commande_est_eligible_mais_rien_n_est_deduit()
    {
        var decision = Decider(100m);

        Assert.True(decision.Eligible);
        Assert.Equal(0m, decision.MontantRemise);
        Assert.Equal(100m, decision.MontantNet);
        Assert.Contains(decision.Explications, e => e.Contains("Aucune règle"));
    }

    [Fact]
    public void Une_exclusion_annule_toutes_les_autres_remises()
    {
        var decision = Decider(1000m, Remise.Pourcentage(10, "premium"), Remise.Exclusion("impayés"));

        Assert.False(decision.Eligible);
        Assert.Equal(0m, decision.MontantRemise);
        Assert.Equal(1000m, decision.MontantNet);
        Assert.Empty(decision.RemisesRetenues);
        Assert.Contains("Non éligible : impayés", decision.Explications);
    }

    [Fact]
    public void Les_pourcentages_cumulables_s_additionnent()
    {
        var decision = Decider(200m, Remise.Pourcentage(10, "a"), Remise.Pourcentage(5, "b"));

        Assert.Equal(30m, decision.MontantRemise);
        Assert.Equal(15m, decision.TauxEffectif);
        Assert.Equal(2, decision.RemisesRetenues.Count);
    }

    [Fact]
    public void Pourcentages_et_montants_fixes_se_cumulent()
    {
        var decision = Decider(200m, Remise.Pourcentage(10, "a"), Remise.MontantFixe(10, "b"));

        Assert.Equal(30m, decision.MontantRemise);
        Assert.Equal(170m, decision.MontantNet);
    }

    [Fact]
    public void Le_taux_cumule_est_plafonne()
    {
        var decision = Decider(100m, Remise.Pourcentage(20, "a"), Remise.Pourcentage(15, "b"));

        Assert.Equal(30m, decision.MontantRemise);
        Assert.Contains(decision.Explications, e => e.Contains("plafonné"));
    }

    [Fact]
    public void Le_plafond_est_parametrable_par_la_politique()
    {
        var politique = new PolitiqueRemise { TauxMaximalCumule = 50m };

        var decision = Decider(100m, politique, Remise.Pourcentage(20, "a"), Remise.Pourcentage(15, "b"));

        Assert.Equal(35m, decision.MontantRemise);
    }

    [Fact]
    public void Une_offre_exclusive_plus_avantageuse_remplace_le_cumul()
    {
        var exclusive = Remise.Pourcentage(20, "collaborateur", cumulable: false);
        var decision = Decider(100m, Remise.Pourcentage(5, "fidélité"), Remise.Pourcentage(3, "ancienneté"), exclusive);

        Assert.Equal(20m, decision.MontantRemise);
        Assert.Equal([exclusive], decision.RemisesRetenues);
        Assert.Equal(2, decision.RemisesEcartees.Count);
    }

    [Fact]
    public void Un_cumul_plus_avantageux_l_emporte_sur_l_offre_exclusive()
    {
        var exclusive = Remise.Pourcentage(15, "AUTOMNE15", cumulable: false);
        var decision = Decider(100m, Remise.Pourcentage(10, "premium"), Remise.Pourcentage(10, "gros"), exclusive);

        Assert.Equal(20m, decision.MontantRemise);
        Assert.Equal([exclusive], decision.RemisesEcartees);
    }

    [Fact]
    public void A_egalite_le_cumul_est_retenu()
    {
        var exclusive = Remise.Pourcentage(10, "exclusive", cumulable: false);
        var decision = Decider(100m, Remise.Pourcentage(10, "cumulable"), exclusive);

        Assert.Equal(10m, decision.MontantRemise);
        Assert.Equal("cumulable", Assert.Single(decision.RemisesRetenues).Motif);
    }

    [Fact]
    public void Parmi_plusieurs_offres_exclusives_la_meilleure_est_retenue()
    {
        var decision = Decider(100m,
            Remise.Pourcentage(15, "quinze", cumulable: false),
            Remise.MontantFixe(25, "vingt-cinq CHF", cumulable: false),
            Remise.Pourcentage(20, "vingt", cumulable: false));

        Assert.Equal("vingt-cinq CHF", Assert.Single(decision.RemisesRetenues).Motif);
        Assert.Equal(25m, decision.MontantRemise);
    }

    [Fact]
    public void Une_offre_exclusive_en_pourcentage_respecte_aussi_le_plafond()
    {
        var decision = Decider(100m, Remise.Pourcentage(50, "liquidation", cumulable: false));

        Assert.Equal(30m, decision.MontantRemise);
    }

    [Fact]
    public void La_remise_ne_depasse_jamais_le_montant_brut()
    {
        var decision = Decider(8m, Remise.MontantFixe(10, "bon"));

        Assert.Equal(8m, decision.MontantRemise);
        Assert.Equal(0m, decision.MontantNet);
    }

    [Theory]
    [InlineData(228.90, 20, 45.80)] // 45.78 → 45.80
    [InlineData(550.20, 30, 165.05)] // 165.06 → 165.05
    [InlineData(10.15, 10, 1.00)]   // 1.015 → 1.00
    [InlineData(10.50, 25, 2.65)]   // 2.625 → 2.65 (milieu arrondi vers le haut)
    public void La_remise_est_arrondie_aux_5_centimes(decimal brut, decimal taux, decimal attendu) =>
        Assert.Equal(attendu, Decider(brut, Remise.Pourcentage(taux, "x")).MontantRemise);

    [Fact]
    public void Sans_arrondi_suisse_la_remise_est_arrondie_au_centime()
    {
        var politique = new PolitiqueRemise { ArrondiCinqCentimes = false };

        Assert.Equal(45.78m, Decider(228.90m, politique, Remise.Pourcentage(20, "x")).MontantRemise);
    }

    [Fact]
    public void Une_regle_en_erreur_est_signalee_sans_bloquer_les_autres_remises()
    {
        var moteur = new MoteurDeRegles<Commande, Remise>()
            .Ajouter(Regle.Creer<Commande, Remise>("OK", "ok").Toujours().Alors(_ => Remise.Pourcentage(10, "ok")).Construire())
            .Ajouter(Regle.Creer<Commande, Remise>("KO", "ko").Toujours()
                .Alors(_ => throw new InvalidOperationException("base de données indisponible")).Construire());

        var decision = new CalculateurRemise().Calculer(moteur.Evaluer(Commande(100m)));

        Assert.Equal(10m, decision.MontantRemise);
        Assert.Contains(decision.Explications, e => e.Contains("KO") && e.Contains("indisponible"));
    }

    [Fact]
    public void Commande_vide_taux_effectif_nul()
    {
        var commande = new Commande("VIDE", Aujourdhui, Client(), []);
        var decision = new CalculateurRemise().Calculer(new MoteurDeRegles<Commande, Remise>().Evaluer(commande));

        Assert.Equal(0m, decision.MontantBrut);
        Assert.Equal(0m, decision.TauxEffectif);
    }

    [Fact]
    public void Calculer_un_rapport_null_est_refuse() =>
        Assert.Throws<ArgumentNullException>(() => new CalculateurRemise().Calculer(null!));
}
