using System.Globalization;
using System.Text;
using MoteurRegles.Remises;
using MoteurRegles.Remises.Configuration;

// Démonstration console du moteur de règles « éligibilité à une remise ».
// Usage : dotnet run --project src/MoteurRegles.App -- [regles.json] [commandes.json] [--trace]

Console.OutputEncoding = Encoding.UTF8;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var afficherTrace = args.Contains("--trace");
var chemins = args.Where(a => !a.StartsWith("--")).ToArray();
var cheminRegles = chemins.ElementAtOrDefault(0) ?? Path.Combine(AppContext.BaseDirectory, "donnees", "regles.json");
var cheminCommandes = chemins.ElementAtOrDefault(1) ?? Path.Combine(AppContext.BaseDirectory, "donnees", "commandes.json");

ConfigurationRemises configuration;
IReadOnlyList<CasCommande> cas;
try
{
    configuration = new FabriqueRegles().ChargerFichier(cheminRegles);
    cas = ChargeurJeuDeDonnees.ChargerFichier(cheminCommandes);
}
catch (Exception ex) when (ex is ConfigurationInvalideException or IOException)
{
    Console.Error.WriteLine($"Erreur de chargement : {ex.Message}");
    return 2;
}

var service = configuration.CreerService();

Console.WriteLine("══════════════════════════════════════════════════════════════════════");
Console.WriteLine(" MOTEUR DE RÈGLES — Éligibilité à une remise");
Console.WriteLine("══════════════════════════════════════════════════════════════════════");
Console.WriteLine($" Règles : {Path.GetFileName(cheminRegles)}   Commandes : {Path.GetFileName(cheminCommandes)}");
Console.WriteLine($" Politique : taux cumulé max {configuration.Politique.TauxMaximalCumule} %, " +
                  $"arrondi 5 ct : {(configuration.Politique.ArrondiCinqCentimes ? "oui" : "non")}");
Console.WriteLine();
Console.WriteLine(" Règles actives (ordre d'évaluation) :");
foreach (var regle in service.Moteur.Regles)
    Console.WriteLine($"   {regle.Priorite,3}  {regle.Code,-24} {regle.Libelle}{(regle.ArreteEvaluation ? "  [arrêt]" : "")}");
Console.WriteLine();

var conformes = 0;
var verifies = 0;

foreach (var (commande, attendu) in cas)
{
    var decision = service.Evaluer(commande);

    Console.WriteLine("──────────────────────────────────────────────────────────────────────");
    Console.WriteLine($" {commande.Numero}  {commande.Date:dd.MM.yyyy}  {commande.Client.Nom} ({commande.Client.Categorie}, " +
                      $"{commande.Client.NombreCommandesPassees} cmd){(commande.CodePromo is null ? "" : $"  code « {commande.CodePromo} »")}");
    Console.WriteLine($"   Montant brut      : {decision.MontantBrut,10:0.00} CHF");

    if (!decision.Eligible)
        Console.WriteLine("   Décision          : NON ÉLIGIBLE");
    foreach (var remise in decision.RemisesRetenues)
        Console.WriteLine($"   ✔ retenue         : {remise}");
    foreach (var remise in decision.RemisesEcartees)
        Console.WriteLine($"   ✘ écartée         : {remise}");
    foreach (var explication in decision.Explications)
        Console.WriteLine($"   ℹ {explication}");

    Console.WriteLine($"   Remise            : {decision.MontantRemise,10:0.00} CHF ({decision.TauxEffectif:0.##} %)");
    Console.WriteLine($"   Montant net       : {decision.MontantNet,10:0.00} CHF");

    if (afficherTrace)
    {
        Console.WriteLine("   Trace :");
        foreach (var trace in decision.Rapport.Traces)
            Console.WriteLine($"     {trace}");
    }

    if (attendu is not null)
    {
        verifies++;
        var ok = attendu.Eligible == decision.Eligible && attendu.MontantRemise == decision.MontantRemise;
        if (ok) conformes++;
        Console.WriteLine(ok
            ? $"   Attendu           : OK ({attendu.Commentaire})"
            : $"   Attendu           : ÉCART ! attendu éligible={attendu.Eligible}, remise={attendu.MontantRemise:0.00}");
    }
}

Console.WriteLine("══════════════════════════════════════════════════════════════════════");
Console.WriteLine($" {cas.Count} commandes évaluées — {conformes}/{verifies} conformes au résultat attendu");
Console.WriteLine("══════════════════════════════════════════════════════════════════════");

return conformes == verifies ? 0 : 1;
