using MoteurRegles.Core;

namespace MoteurRegles.Tests.Core;

/// <summary>
/// Tests du moteur générique, indépendamment du domaine des remises :
/// le contexte est un simple entier et l'effet une chaîne.
/// </summary>
public class MoteurDeReglesTests
{
    private static Regle<int, string> R(string code, int priorite = 100, Func<int, bool>? quand = null, bool arret = false)
    {
        var constructeur = Regle.Creer<int, string>(code, $"règle {code}")
            .Quand($"cond {code}", quand ?? (_ => true))
            .Alors(_ => $"effet {code}")
            .AvecPriorite(priorite);
        return (arret ? constructeur.ArreterSiAppliquee() : constructeur).Construire();
    }

    private sealed class RegleQuiPlante(string code, bool danslaCondition) : RegleBase<int, string>
    {
        public override string Code => code;
        public override string Libelle => "règle défaillante";
        public override bool EstApplicable(int contexte) =>
            danslaCondition ? throw new InvalidOperationException("condition cassée") : true;
        public override string ProduireEffet(int contexte) => throw new FormatException("effet cassé");
    }

    [Fact]
    public void Un_moteur_sans_regle_produit_un_rapport_vide()
    {
        var rapport = new MoteurDeRegles<int, string>().Evaluer(1);

        Assert.Empty(rapport.Traces);
        Assert.Empty(rapport.Effets);
        Assert.False(rapport.ContientErreurs);
    }

    [Fact]
    public void Les_regles_sont_evaluees_par_priorite_croissante_quel_que_soit_l_ordre_d_ajout()
    {
        var moteur = new MoteurDeRegles<int, string>()
            .Ajouter(R("C", 30))
            .Ajouter(R("A", 10))
            .Ajouter(R("B", 20));

        var rapport = moteur.Evaluer(0);

        Assert.Equal(["A", "B", "C"], rapport.Traces.Select(t => t.Code));
        Assert.Equal(["effet A", "effet B", "effet C"], rapport.Effets);
    }

    [Fact]
    public void A_priorite_egale_l_ordre_d_ajout_est_conserve()
    {
        var moteur = new MoteurDeRegles<int, string>([R("Z", 5), R("Y", 5), R("X", 5)]);

        Assert.Equal(["Z", "Y", "X"], moteur.Evaluer(0).Traces.Select(t => t.Code));
    }

    [Fact]
    public void Une_regle_non_applicable_est_tracee_sans_effet()
    {
        var moteur = new MoteurDeRegles<int, string>()
            .Ajouter(R("POSITIF", quand: x => x > 0))
            .Ajouter(R("NEGATIF", quand: x => x < 0));

        var rapport = moteur.Evaluer(-4);

        Assert.Equal(StatutRegle.NonApplicable, rapport.Trace("POSITIF")!.Statut);
        Assert.Null(rapport.Trace("POSITIF")!.Effet);
        Assert.Equal(StatutRegle.Appliquee, rapport.Trace("NEGATIF")!.Statut);
        Assert.Equal(["NEGATIF"], rapport.CodesAppliques);
    }

    [Fact]
    public void Une_regle_d_arret_appliquee_fait_ignorer_les_regles_suivantes()
    {
        var moteur = new MoteurDeRegles<int, string>()
            .Ajouter(R("AVANT", 1))
            .Ajouter(R("STOP", 2, arret: true))
            .Ajouter(R("APRES-1", 3))
            .Ajouter(R("APRES-2", 4));

        var rapport = moteur.Evaluer(0);

        Assert.True(rapport.EvaluationInterrompue);
        Assert.Equal(["AVANT", "STOP"], rapport.CodesAppliques);
        Assert.All(rapport.Traces.Skip(2), t =>
        {
            Assert.Equal(StatutRegle.Ignoree, t.Statut);
            Assert.Contains("STOP", t.Message);
        });
    }

    [Fact]
    public void Une_regle_d_arret_non_applicable_n_interrompt_pas_l_evaluation()
    {
        var moteur = new MoteurDeRegles<int, string>()
            .Ajouter(R("STOP", 1, quand: x => x > 100, arret: true))
            .Ajouter(R("SUIVANTE", 2));

        var rapport = moteur.Evaluer(5);

        Assert.False(rapport.EvaluationInterrompue);
        Assert.Equal(["SUIVANTE"], rapport.CodesAppliques);
    }

    [Theory]
    [InlineData(true, "InvalidOperationException")]
    [InlineData(false, "FormatException")]
    public void Une_exception_dans_une_regle_est_isolee_et_les_autres_regles_sont_evaluees(bool danslaCondition, string typeAttendu)
    {
        var moteur = new MoteurDeRegles<int, string>()
            .Ajouter(R("OK-1", 1))
            .Ajouter(new RegleQuiPlante("KO", danslaCondition))
            .Ajouter(R("OK-2", 200));

        var rapport = moteur.Evaluer(0);

        Assert.True(rapport.ContientErreurs);
        var trace = rapport.Trace("KO")!;
        Assert.Equal(StatutRegle.Erreur, trace.Statut);
        Assert.Contains(typeAttendu, trace.Message);
        Assert.Equal(["OK-1", "OK-2"], rapport.CodesAppliques);
    }

    [Theory]
    [InlineData("REGLE")]
    [InlineData("regle")]
    public void Ajouter_deux_regles_avec_le_meme_code_est_refuse(string codeDoublon)
    {
        var moteur = new MoteurDeRegles<int, string>().Ajouter(R("REGLE"));

        var ex = Assert.Throws<InvalidOperationException>(() => moteur.Ajouter(R(codeDoublon)));
        Assert.Contains(codeDoublon, ex.Message);
        Assert.Equal(1, moteur.Nombre);
    }

    [Fact]
    public void Ajouter_une_regle_null_est_refuse() =>
        Assert.Throws<ArgumentNullException>(() => new MoteurDeRegles<int, string>().Ajouter(null!));

    [Fact]
    public void Retirer_supprime_la_regle_par_code()
    {
        var moteur = new MoteurDeRegles<int, string>([R("A"), R("B")]);

        Assert.True(moteur.Retirer("a"));
        Assert.False(moteur.Retirer("INEXISTANTE"));
        Assert.False(moteur.Contient("A"));
        Assert.Equal(["B"], moteur.Evaluer(0).CodesAppliques);
    }

    [Fact]
    public void Evaluer_un_contexte_null_est_refuse() =>
        Assert.Throws<ArgumentNullException>(() => new MoteurDeRegles<string, string>().Evaluer(null!));

    [Fact]
    public void Le_moteur_est_reutilisable_et_chaque_evaluation_est_independante()
    {
        var moteur = new MoteurDeRegles<int, string>()
            .Ajouter(R("STOP", 1, quand: x => x == 0, arret: true))
            .Ajouter(R("SUITE", 2));

        var premier = moteur.Evaluer(0);
        var second = moteur.Evaluer(1);

        Assert.True(premier.EvaluationInterrompue);
        Assert.False(second.EvaluationInterrompue);
        Assert.Equal(["SUITE"], second.CodesAppliques);
    }

    [Fact]
    public void Le_rapport_formate_une_ligne_par_regle()
    {
        var moteur = new MoteurDeRegles<int, string>()
            .Ajouter(R("OUI"))
            .Ajouter(R("NON", quand: _ => false));

        var texte = moteur.Evaluer(0).Formater();

        Assert.Contains("[Appliquee    ] OUI", texte);
        Assert.Contains("→ effet OUI", texte);
        Assert.Contains("[NonApplicable] NON", texte);
        Assert.Equal(2, texte.Trim().Split('\n').Length);
    }

    [Fact]
    public void Le_rapport_formate_aussi_les_regles_ignorees_et_en_erreur()
    {
        var moteur = new MoteurDeRegles<int, string>()
            .Ajouter(new RegleQuiPlante("KO", danslaCondition: false))
            .Ajouter(R("STOP", 101, arret: true))
            .Ajouter(R("APRES", 102));

        var lignes = moteur.Evaluer(0).Formater().Trim().Split('\n');

        Assert.StartsWith("[Erreur       ] KO", lignes[0]);
        Assert.Contains("(FormatException : effet cassé)", lignes[0]);
        Assert.Contains("(évaluation arrêtée par STOP)", lignes[2]);
    }

    [Fact]
    public void Regles_expose_l_ordre_d_evaluation()
    {
        var moteur = new MoteurDeRegles<int, string>([R("B", 2), R("A", 1)]);

        Assert.Equal(["A", "B"], moteur.Regles.Select(r => r.Code));
    }
}
