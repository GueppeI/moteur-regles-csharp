using MoteurRegles.Core;
using MoteurRegles.Core.Conditions;

namespace MoteurRegles.Tests.Core;

public class RegleFluenteTests
{
    [Fact]
    public void Une_regle_construite_reprend_tous_ses_parametres()
    {
        var regle = Regle.Creer<int, string>("GRAND", "Grand nombre")
            .Quand("> 100", x => x > 100)
            .Alors(x => $"{x} est grand")
            .AvecPriorite(7)
            .ArreterSiAppliquee()
            .Construire();

        Assert.Equal("GRAND", regle.Code);
        Assert.Equal("Grand nombre", regle.Libelle);
        Assert.Equal(7, regle.Priorite);
        Assert.True(regle.ArreteEvaluation);
        Assert.True(regle.EstApplicable(150));
        Assert.False(regle.EstApplicable(50));
        Assert.Equal("150 est grand", regle.ProduireEffet(150));
        Assert.Equal("[GRAND] Grand nombre (priorité 7)", regle.ToString());
    }

    [Fact]
    public void Les_valeurs_par_defaut_sont_priorite_100_sans_arret()
    {
        var regle = Regle.Creer<int, string>("X", "x").Toujours().Alors(_ => "x").Construire();

        Assert.Equal(RegleBase<int, string>.PrioriteParDefaut, regle.Priorite);
        Assert.False(regle.ArreteEvaluation);
    }

    [Fact]
    public void Plusieurs_appels_a_Quand_se_combinent_en_ET()
    {
        var regle = Regle.Creer<int, string>("X", "x")
            .Quand("positif", x => x > 0)
            .Quand(new Condition<int>("pair", x => x % 2 == 0))
            .Alors(_ => "ok")
            .Construire();

        Assert.True(regle.EstApplicable(4));
        Assert.False(regle.EstApplicable(3));
        Assert.Equal("(positif ET pair)", regle.Condition.Description);
    }

    [Fact]
    public void Construire_sans_condition_est_refuse()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Regle.Creer<int, string>("SANS-COND", "x").Alors(_ => "x").Construire());
        Assert.Contains("SANS-COND", ex.Message);
    }

    [Fact]
    public void Construire_sans_effet_est_refuse() =>
        Assert.Throws<InvalidOperationException>(() =>
            Regle.Creer<int, string>("SANS-EFFET", "x").Toujours().Construire());

    [Theory]
    [InlineData("", "libellé")]
    [InlineData("CODE", " ")]
    public void Code_et_libelle_sont_obligatoires(string code, string libelle) =>
        Assert.Throws<ArgumentException>(() => Regle.Creer<int, string>(code, libelle));
}
