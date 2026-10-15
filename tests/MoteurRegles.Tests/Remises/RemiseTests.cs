using MoteurRegles.Remises.Modele;

namespace MoteurRegles.Tests.Remises;

public class RemiseTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100.01)]
    public void Un_taux_hors_de_l_intervalle_0_100_est_refuse(decimal taux) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Remise.Pourcentage(taux, "test"));

    [Theory]
    [InlineData(0.5)]
    [InlineData(100)]
    public void Un_taux_valide_est_accepte(decimal taux) =>
        Assert.Equal(taux, Remise.Pourcentage(taux, "test").Valeur);

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Un_montant_fixe_nul_ou_negatif_est_refuse(decimal montant) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Remise.MontantFixe(montant, "test"));

    [Fact]
    public void Un_motif_est_obligatoire()
    {
        Assert.Throws<ArgumentException>(() => Remise.Pourcentage(5, ""));
        Assert.Throws<ArgumentException>(() => Remise.MontantFixe(5, " "));
        Assert.Throws<ArgumentException>(() => Remise.Exclusion(""));
    }

    [Fact]
    public void Une_exclusion_n_est_jamais_cumulable()
    {
        var exclusion = Remise.Exclusion("impayés");

        Assert.Equal(TypeRemise.Exclusion, exclusion.Type);
        Assert.False(exclusion.Cumulable);
        Assert.Equal(0m, exclusion.Valeur);
    }

    [Fact]
    public void Une_remise_est_cumulable_par_defaut() =>
        Assert.True(Remise.Pourcentage(5, "x").Cumulable);

    [Fact]
    public void ToString_decrit_la_remise()
    {
        Assert.Equal("-10 % : client Premium", Remise.Pourcentage(10, "client Premium").ToString());
        Assert.Equal("-15 % [exclusive] : code", Remise.Pourcentage(15, "code", cumulable: false).ToString());
        Assert.Equal("EXCLUSION : impayés", Remise.Exclusion("impayés").ToString());
        Assert.StartsWith("-10", Remise.MontantFixe(10, "bienvenue").ToString());
        Assert.Contains("CHF", Remise.MontantFixe(10, "bienvenue").ToString());
    }

    [Fact]
    public void Deux_remises_identiques_sont_egales() =>
        Assert.Equal(Remise.Pourcentage(5, "x"), Remise.Pourcentage(5, "x"));
}
