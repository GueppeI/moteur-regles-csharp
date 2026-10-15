using MoteurRegles.Core;
using MoteurRegles.Remises;
using MoteurRegles.Remises.Configuration;
using MoteurRegles.Remises.Modele;
using MoteurRegles.Remises.Regles;
using static MoteurRegles.Tests.Donnees;

namespace MoteurRegles.Tests.Extensibilite;

/// <summary>
/// Nouvelle règle écrite APRÈS coup, en dehors des projets du moteur et du domaine :
/// elle prouve qu'on étend le système sans modifier une seule ligne existante (principe ouvert/fermé).
/// </summary>
public sealed class RegleBlackFriday : RegleRemise
{
    public static readonly DateOnly Jour = new(2026, 11, 27);

    public override string Code => "EVT-BLACK-FRIDAY";

    public override string Libelle => "Black Friday 25 % (exclusif)";

    public override int Priorite => PrioriteCodePromo + 10;

    public override bool EstApplicable(Commande commande) => commande.Date == Jour;

    public override Remise ProduireEffet(Commande commande) =>
        Remise.Pourcentage(25, "Black Friday", cumulable: false);
}

/// <summary>
/// Vérifie les trois façons documentées d'ajouter une règle (voir docs/EXTENSIBILITE.md)
/// et l'absence de régression sur le jeu de données de référence.
/// </summary>
public class AjoutRegleTests
{
    [Theory]
    [InlineData("2026-11-26", false)]
    [InlineData("2026-11-27", true)]
    [InlineData("2026-11-28", false)]
    public void Black_friday_uniquement_le_jour_J(string date, bool attendu) =>
        Assert.Equal(attendu, new RegleBlackFriday().EstApplicable(Commande(date: DateOnly.Parse(date))));

    [Fact]
    public void Methode_1_une_nouvelle_classe_de_regle_s_ajoute_au_moteur_existant()
    {
        var service = CatalogueReglesRemise.CreerService();
        service.Moteur.Ajouter(new RegleBlackFriday());

        var decision = service.Evaluer(Commande(200m, date: RegleBlackFriday.Jour));

        Assert.Equal(50m, decision.MontantRemise);
        Assert.Contains("EVT-BLACK-FRIDAY", decision.Rapport.CodesAppliques);
    }

    [Fact]
    public void Methode_1_ajouter_une_regle_ne_change_aucun_resultat_existant()
    {
        var reference = CatalogueReglesRemise.CreerService();
        var etendu = CatalogueReglesRemise.CreerService();
        etendu.Moteur.Ajouter(new RegleBlackFriday());

        foreach (var cas in CasDeReference)
        {
            Assert.Equal(reference.Evaluer(cas.Commande).MontantRemise, etendu.Evaluer(cas.Commande).MontantRemise);
        }
    }

    [Fact]
    public void Methode_1_une_exclusion_reste_prioritaire_sur_une_nouvelle_regle()
    {
        var service = CatalogueReglesRemise.CreerService();
        service.Moteur.Ajouter(new RegleBlackFriday());

        var decision = service.Evaluer(Commande(200m, client: Client(impayes: true), date: RegleBlackFriday.Jour));

        Assert.False(decision.Eligible);
        Assert.Equal(StatutRegle.Ignoree, decision.Rapport.Trace("EVT-BLACK-FRIDAY")!.Statut);
    }

    [Fact]
    public void Methode_2_une_regle_fluente_sans_classe_dediee()
    {
        var panierMixte = Regle.Creer<Commande, Remise>("CMD-PANIER-MIXTE", "Au moins 3 catégories différentes")
            .Quand("≥ 3 catégories", c => c.Lignes.Select(l => l.Categorie).Distinct().Count() >= 3)
            .Alors(_ => Remise.Pourcentage(2, "panier mixte"))
            .AvecPriorite(RegleRemise.PrioriteCommande)
            .Construire();

        var service = CatalogueReglesRemise.CreerService();
        service.Moteur.Ajouter(panierMixte);

        var commande = CommandeMultiLignes(
            new LigneCommande("A", "Stylo", "Papeterie", 10m, 1),
            new LigneCommande("B", "Lampe", "Mobilier", 40m, 1),
            new LigneCommande("C", "Câble", "Electronique", 50m, 1));

        Assert.Equal(2m, service.Evaluer(commande).MontantRemise);
    }

    [Fact]
    public void Methode_3_une_regle_parametrable_ajoutee_par_configuration_sans_recompiler()
    {
        const string json = """
            { "regles": [
                { "type": "MontantMinimum", "seuil": 30 },
                { "type": "QuantiteCategorie", "categorie": "Mobilier", "quantiteMin": 2, "taux": 6 }
            ] }
            """;
        var service = new FabriqueRegles().Charger(json).CreerService();

        var decision = service.Evaluer(Commande(300m, categorie: "Mobilier", quantite: 2));

        Assert.Equal(18m, decision.MontantRemise);
    }

    [Fact]
    public void Methode_3_une_nouvelle_classe_rendue_configurable_par_enregistrement()
    {
        var fabrique = new FabriqueRegles().Enregistrer("BlackFriday", _ => new RegleBlackFriday());

        var service = fabrique.Charger("""{ "regles": [ { "type": "BlackFriday" } ] }""").CreerService();

        Assert.Equal(25m, service.Evaluer(Commande(100m, date: RegleBlackFriday.Jour)).MontantRemise);
    }

    [Fact]
    public void Retirer_une_regle_desactive_la_remise_correspondante()
    {
        var service = CatalogueReglesRemise.CreerService();
        Assert.Equal(10m, service.Evaluer(Cas("CMD-004").Commande).MontantRemise);

        service.Moteur.Retirer("CLIENT-FIDELE");

        Assert.Equal(0m, service.Evaluer(Cas("CMD-004").Commande).MontantRemise);
    }

    [Fact]
    public void Le_moteur_generique_sert_aussi_un_autre_cas_metier_la_validation_de_commande()
    {
        // Même moteur, autre effet : des messages de validation au lieu de remises.
        var validation = new MoteurDeRegles<Commande, string>()
            .Ajouter(Regle.Creer<Commande, string>("VAL-VIDE", "Commande sans ligne")
                .Quand("aucune ligne", c => c.Lignes.Count == 0)
                .Alors(_ => "La commande ne contient aucun article.")
                .ArreterSiAppliquee().AvecPriorite(0).Construire())
            .Ajouter(Regle.Creer<Commande, string>("VAL-QUANTITE", "Quantité excessive")
                .Quand("quantité > 100", c => c.Lignes.Any(l => l.Quantite > 100))
                .Alors(c => $"Quantité excessive sur {c.Lignes.First(l => l.Quantite > 100).Reference}.")
                .Construire());

        var erreurs = validation.Evaluer(Commande(500m, quantite: 250)).Effets.ToList();

        Assert.Equal(["Quantité excessive sur REF."], erreurs);
        Assert.Empty(validation.Evaluer(Commande(50m)).Effets);
    }
}
