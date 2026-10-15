using MoteurRegles.Core.Conditions;

namespace MoteurRegles.Tests.Core;

public class ConditionTests
{
    private static readonly ICondition<int> Positif = new Condition<int>("positif", x => x > 0);
    private static readonly ICondition<int> Pair = new Condition<int>("pair", x => x % 2 == 0);

    [Theory]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(-3, false)]
    public void Condition_simple_evalue_le_predicat(int valeur, bool attendu) =>
        Assert.Equal(attendu, Positif.EstSatisfaite(valeur));

    [Theory]
    [InlineData(4, true)]   // positif et pair
    [InlineData(3, false)]  // positif, impair
    [InlineData(-2, false)] // pair, négatif
    [InlineData(-1, false)]
    public void Et_est_vraie_seulement_si_les_deux_conditions_sont_vraies(int valeur, bool attendu) =>
        Assert.Equal(attendu, Positif.Et(Pair).EstSatisfaite(valeur));

    [Theory]
    [InlineData(4, true)]
    [InlineData(3, true)]
    [InlineData(-2, true)]
    [InlineData(-1, false)]
    public void Ou_est_vraie_si_au_moins_une_condition_est_vraie(int valeur, bool attendu) =>
        Assert.Equal(attendu, Positif.Ou(Pair).EstSatisfaite(valeur));

    [Theory]
    [InlineData(5, false)]
    [InlineData(-5, true)]
    public void Non_inverse_la_condition(int valeur, bool attendu) =>
        Assert.Equal(attendu, Positif.Non().EstSatisfaite(valeur));

    [Fact]
    public void Les_descriptions_se_composent_pour_expliquer_la_regle()
    {
        var condition = Positif.Et(Pair.Non()).Ou(Condition<int>.Toujours);

        Assert.Equal("((positif ET NON pair) OU toujours)", condition.Description);
    }

    [Fact]
    public void Et_court_circuite_la_seconde_condition()
    {
        var appelee = false;
        var espion = new Condition<int>("espion", _ => appelee = true);

        Positif.Et(espion).EstSatisfaite(-1);

        Assert.False(appelee);
    }

    [Fact]
    public void Toujours_est_vraie_pour_tout_contexte() =>
        Assert.True(Condition<string>.Toujours.EstSatisfaite("n'importe quoi"));

    [Fact]
    public void Une_description_vide_est_refusee() =>
        Assert.Throws<ArgumentException>(() => new Condition<int>(" ", _ => true));

    [Fact]
    public void Un_predicat_null_est_refuse() =>
        Assert.Throws<ArgumentNullException>(() => new Condition<int>("x", null!));

    [Fact]
    public void Composer_avec_null_est_refuse()
    {
        Assert.Throws<ArgumentNullException>(() => Positif.Et(null!));
        Assert.Throws<ArgumentNullException>(() => Positif.Ou(null!));
        Assert.Throws<ArgumentNullException>(() => ((ICondition<int>)null!).Non());
    }
}
