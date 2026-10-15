using MoteurRegles.Remises.Modele;
using MoteurRegles.Remises.Regles;
using static MoteurRegles.Tests.Donnees;

namespace MoteurRegles.Tests.Remises;

/// <summary>Tests unitaires de chaque règle de remise, en particulier aux valeurs limites.</summary>
public class ReglesTests
{
    // --- Exclusions ------------------------------------------------------------------------------

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Impayes_exclut_le_client(bool impayes, bool applicable)
    {
        var regle = new RegleClientAvecImpayes();
        var commande = Commande(client: Client(impayes: impayes));

        Assert.Equal(applicable, regle.EstApplicable(commande));
        Assert.True(regle.ArreteEvaluation);
        Assert.Equal(TypeRemise.Exclusion, regle.ProduireEffet(commande).Type);
    }

    [Theory]
    [InlineData(29.95, true)]
    [InlineData(30.00, false)] // le seuil lui-même est éligible
    [InlineData(250, false)]
    public void Montant_minimum_exclut_les_petites_commandes(decimal montant, bool applicable)
    {
        var regle = new RegleMontantMinimum(30m);

        Assert.Equal(applicable, regle.EstApplicable(Commande(montant)));
        Assert.True(regle.ArreteEvaluation);
        Assert.Contains("30", regle.ProduireEffet(Commande(montant)).Motif);
    }

    [Fact]
    public void Montant_minimum_negatif_est_refuse() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegleMontantMinimum(-1));

    [Fact]
    public void Les_exclusions_sont_prioritaires_sur_toutes_les_remises()
    {
        Assert.True(new RegleClientAvecImpayes().Priorite < new RegleMontantMinimum(30).Priorite);
        Assert.True(new RegleMontantMinimum(30).Priorite < new RegleEmploye().Priorite);
    }

    // --- Profil client ---------------------------------------------------------------------------

    [Theory]
    [InlineData(CategorieClient.Employe, true)]
    [InlineData(CategorieClient.Premium, false)]
    [InlineData(CategorieClient.Standard, false)]
    public void Employe_obtient_une_remise_exclusive(CategorieClient categorie, bool applicable)
    {
        var regle = new RegleEmploye(20m);
        var commande = Commande(client: Client(categorie));

        Assert.Equal(applicable, regle.EstApplicable(commande));
        var remise = regle.ProduireEffet(commande);
        Assert.Equal(20m, remise.Valeur);
        Assert.False(remise.Cumulable);
    }

    [Theory]
    [InlineData(CategorieClient.Premium, true)]
    [InlineData(CategorieClient.Standard, false)]
    [InlineData(CategorieClient.Employe, false)]
    public void Premium_obtient_une_remise_cumulable(CategorieClient categorie, bool applicable)
    {
        var regle = new RegleClientPremium(10m);

        Assert.Equal(applicable, regle.EstApplicable(Commande(client: Client(categorie))));
        Assert.True(regle.ProduireEffet(Commande()).Cumulable);
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(50, true)]
    public void Fidelite_a_partir_de_10_commandes(int commandes, bool applicable) =>
        Assert.Equal(applicable, new RegleClientFidele(10, 5m).EstApplicable(Commande(client: Client(commandes: commandes))));

    [Theory]
    [InlineData("2023-10-12", true)]  // exactement 3 ans le jour de la commande
    [InlineData("2023-10-13", false)] // un jour de moins
    [InlineData("2020-01-01", true)]
    public void Anciennete_calculee_a_la_date_de_la_commande(string inscription, bool applicable)
    {
        var client = Client(inscription: DateOnly.Parse(inscription));

        Assert.Equal(applicable, new RegleAnciennete(3, 3m).EstApplicable(Commande(client: client, date: Aujourdhui)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Un_taux_invalide_est_refuse_des_la_construction(decimal taux)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegleClientPremium(taux));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegleEmploye(taux));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegleClientFidele(10, taux));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegleAnciennete(3, taux));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegleGrosseCommande(500, taux));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegleQuantiteCategorie("X", 1, taux));
    }

    // --- Contenu de la commande ------------------------------------------------------------------

    [Theory]
    [InlineData(499.95, false)]
    [InlineData(500.00, true)]
    [InlineData(1200, true)]
    public void Grosse_commande_a_partir_du_seuil(decimal montant, bool applicable) =>
        Assert.Equal(applicable, new RegleGrosseCommande(500m, 5m).EstApplicable(Commande(montant)));

    [Theory]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public void Volume_par_categorie_a_partir_de_la_quantite_minimum(int quantite, bool applicable) =>
        Assert.Equal(applicable,
            new RegleQuantiteCategorie("Papeterie", 4, 8m).EstApplicable(Commande(40m, categorie: "Papeterie", quantite: quantite)));

    [Fact]
    public void Volume_additionne_les_lignes_de_la_categorie_en_ignorant_la_casse()
    {
        var commande = CommandeMultiLignes(
            new LigneCommande("A", "Stylo", "papeterie", 2m, 2),
            new LigneCommande("B", "Cahier", "PAPETERIE", 5m, 2),
            new LigneCommande("C", "Écran", "Electronique", 300m, 5));

        var regle = new RegleQuantiteCategorie("Papeterie", 4, 8m);

        Assert.Equal(4, commande.NombreArticles("Papeterie"));
        Assert.True(regle.EstApplicable(commande));
        Assert.Contains("4 articles", regle.ProduireEffet(commande).Motif);
    }

    [Fact]
    public void Volume_le_code_depend_de_la_categorie_pour_permettre_plusieurs_instances()
    {
        Assert.Equal("CMD-VOLUME-PAPETERIE", new RegleQuantiteCategorie("Papeterie", 4, 8).Code);
        Assert.NotEqual(new RegleQuantiteCategorie("Papeterie", 4, 8).Code, new RegleQuantiteCategorie("Mobilier", 2, 5).Code);
    }

    [Fact]
    public void Volume_parametres_invalides_refuses()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegleQuantiteCategorie("Papeterie", 0, 8));
        Assert.Throws<ArgumentException>(() => new RegleQuantiteCategorie(" ", 2, 8));
    }

    // --- Codes promo -----------------------------------------------------------------------------

    private static readonly DateOnly Debut = new(2026, 9, 22);
    private static readonly DateOnly Fin = new(2026, 12, 20);

    private static RegleCodePromo Automne() =>
        new("AUTOMNE15", Remise.Pourcentage(15, "code AUTOMNE15", cumulable: false), Debut, Fin);

    [Theory]
    [InlineData("AUTOMNE15", true)]
    [InlineData("automne15", true)]
    [InlineData("  Automne15 ", true)]
    [InlineData("AUTOMNE10", false)]
    [InlineData(null, false)]
    public void Code_promo_reconnu_sans_tenir_compte_de_la_casse_ni_des_espaces(string? saisi, bool applicable) =>
        Assert.Equal(applicable, Automne().EstApplicable(Commande(code: saisi, date: new DateOnly(2026, 10, 15))));

    [Theory]
    [InlineData("2026-09-21", false)]
    [InlineData("2026-09-22", true)]  // bornes incluses
    [InlineData("2026-12-20", true)]
    [InlineData("2026-12-21", false)]
    public void Code_promo_valable_uniquement_pendant_la_campagne(string date, bool applicable) =>
        Assert.Equal(applicable, Automne().EstApplicable(Commande(code: "AUTOMNE15", date: DateOnly.Parse(date))));

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Code_de_bienvenue_reserve_a_la_premiere_commande(int commandesPassees, bool applicable)
    {
        var regle = new RegleCodePromo("BIENVENUE10", Remise.MontantFixe(10, "bienvenue"), premiereCommandeUniquement: true);

        Assert.Equal(applicable, regle.EstApplicable(Commande(code: "BIENVENUE10", client: Client(commandes: commandesPassees))));
    }

    [Fact]
    public void Code_promo_sans_dates_est_toujours_valable()
    {
        var regle = new RegleCodePromo("PERMANENT", Remise.Pourcentage(5, "permanent"));

        Assert.True(regle.EstApplicable(Commande(code: "permanent", date: new DateOnly(2030, 1, 1))));
        Assert.Equal("PROMO-PERMANENT", regle.Code);
        Assert.Same(regle.RemiseAccordee, regle.ProduireEffet(Commande()));
    }

    [Fact]
    public void Code_promo_configuration_incoherente_refusee()
    {
        Assert.Throws<ArgumentException>(() => new RegleCodePromo("X", Remise.Exclusion("non")));
        Assert.Throws<ArgumentException>(() => new RegleCodePromo("X", Remise.Pourcentage(5, "x"), Fin, Debut));
        Assert.Throws<ArgumentException>(() => new RegleCodePromo("", Remise.Pourcentage(5, "x")));
        Assert.Throws<ArgumentNullException>(() => new RegleCodePromo("X", null!));
    }
}
