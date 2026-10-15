# Extensibilité — Ajouter ou modifier une règle

> Projet 5 — Moteur de règles · Ianis Gueppe · E1B

Ce document présente les trois façons d'ajouter une règle au moteur de remises, avec des exemples et les vérifications à effectuer après une modification.

---

## 1. Le principe : le moteur ne connaît que des interfaces

```
           ┌──────────────────────────────┐
           │  MoteurDeRegles<TCtx, TEff>  │   ← reste inchangé lors de l'ajout
           │  (trie, évalue, trace)       │
           └──────────────┬───────────────┘
                          │ ne dépend que de
                          ▼
              IRegle<TContexte, TEffet>
          ┌───────────────┼──────────────────┬───────────────────┐
          ▼               ▼                  ▼                   ▼
   RegleClientPremium  RegleCodePromo   RegleBlackFriday    Regle (fluente)
      (existante)       (existante)     (nouvelle classe)  (nouvelle, sans classe)
```

Le moteur (`MoteurRegles.Core`) utilise l'interface `IRegle`. La logique de remise reste dans les classes du projet `MoteurRegles.Remises`. Le moteur se charge de :

1. trier les règles par `Priorite` ;
2. appeler `EstApplicable(...)` puis `ProduireEffet(...)` ;
3. s'arrêter si une règle appliquée a `ArreteEvaluation = true` ;
4. tracer le résultat de chaque règle (et isoler les exceptions).

Cette séparation suit le principe ouvert/fermé : une nouvelle règle peut être ajoutée en utilisant l'interface existante. Tant qu'elle produit les mêmes types de remises, le moteur et le calculateur restent inchangés.

---

## 2. Trois façons d'ajouter une règle

| Méthode | Quand l'utiliser | Recompiler ? | Code à écrire |
|---|---|---|---|
| **A. Configuration JSON** | Le type de règle existe déjà ; seuls ses paramètres changent | **Non** | Une entrée dans le tableau `regles` |
| **B. Règle fluente** | Une condition et un effet suffisent à décrire la règle | Oui | Définition de la condition et de l'effet |
| **C. Nouvelle classe** | La logique mérite une classe dédiée | Oui | Une classe et son ajout au moteur |

### Méthode A — Par configuration (sans recompiler)

Fichier : `src/MoteurRegles.App/donnees/regles.json`

**Exemple : lancer une campagne « NOEL20 » (20 %, exclusive, du 1er au 24 décembre).**
On ajoute une ligne dans le tableau `regles` :

```json
{ "type": "CodePromo", "code": "NOEL20", "taux": 20, "cumulable": false, "debut": "2026-12-01", "fin": "2026-12-24" }
```

**Exemple : remise volume sur le mobilier.** Le type `QuantiteCategorie` peut être instancié plusieurs fois (son code contient la catégorie) :

```json
{ "type": "QuantiteCategorie", "categorie": "Mobilier", "quantiteMin": 2, "taux": 6 }
```

**Activer / désactiver** une règle : `"actif": false` (la ligne reste dans le fichier, documentée, mais n'est pas chargée).

Types disponibles et paramètres :

| `type` | Paramètres (valeur par défaut) |
|---|---|
| `ClientAvecImpayes` | — |
| `MontantMinimum` | `seuil` (obligatoire) |
| `Employe` | `taux` (20) |
| `ClientPremium` | `taux` (10) |
| `ClientFidele` | `nombreCommandesMin` (10), `taux` (5) |
| `Anciennete` | `annees` (3), `taux` (3) |
| `GrosseCommande` | `seuil` (500), `taux` (5) |
| `QuantiteCategorie` | `categorie`, `quantiteMin`, `taux` (obligatoires) |
| `CodePromo` | `code` + (`taux` **ou** `montant`), `cumulable` (true), `debut`/`fin` (yyyy-MM-dd, optionnels), `premiereCommandeUniquement` (false) |

Un type inconnu, un paramètre obligatoire manquant, un taux supérieur à 100 ou une date mal formée provoquent une `ConfigurationInvalideException` au chargement. Le message précise le problème rencontré.

> Test associé : `AjoutRegleTests.Methode_3_une_regle_parametrable_ajoutee_par_configuration_sans_recompiler`

### Méthode B — Règle fluente (sans nouvelle classe)

```csharp
var panierMixte = Regle.Creer<Commande, Remise>("CMD-PANIER-MIXTE", "Au moins 3 catégories différentes")
    .Quand("≥ 3 catégories", c => c.Lignes.Select(l => l.Categorie).Distinct().Count() >= 3)
    .Alors(_ => Remise.Pourcentage(2, "panier mixte"))
    .AvecPriorite(RegleRemise.PrioriteCommande)
    .Construire();

service.Moteur.Ajouter(panierMixte);
```

Les conditions se composent avec `Et`, `Ou`, `Non` :

```csharp
var weekEnd  = new Condition<Commande>("week-end", c => c.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
var premium  = new Condition<Commande>("premium",  c => c.Client.Categorie == CategorieClient.Premium);

Regle.Creer<Commande, Remise>("PREMIUM-WEEKEND", "Premium le week-end")
    .Quand(weekEnd.Et(premium))          // description : "(week-end ET premium)"
    .Alors(_ => Remise.Pourcentage(3, "week-end premium"))
    .Construire();
```

> Test associé : `AjoutRegleTests.Methode_2_une_regle_fluente_sans_classe_dediee`

### Méthode C — Nouvelle classe de règle

**Exemple : remise « Black Friday » de 25 %, exclusive, le 27 novembre 2026.**

**Étape 1 — Créer la classe** dans `src/MoteurRegles.Remises/Regles/` en héritant de `RegleRemise` :

```csharp
public sealed class RegleBlackFriday : RegleRemise
{
    public static readonly DateOnly Jour = new(2026, 11, 27);

    public override string Code => "EVT-BLACK-FRIDAY";          // unique dans le moteur
    public override string Libelle => "Black Friday 25 % (exclusif)";
    public override int Priorite => PrioriteCodePromo + 10;      // voir tableau des priorités

    public override bool EstApplicable(Commande commande) => commande.Date == Jour;

    public override Remise ProduireEffet(Commande commande) =>
        Remise.Pourcentage(25, "Black Friday", cumulable: false);
}
```

**Étape 2 — L'enregistrer**, au choix :

```csharp
// a) dans le code : catalogue par défaut (CatalogueReglesRemise.ReglesParDefaut) ou à la volée
service.Moteur.Ajouter(new RegleBlackFriday());

// b) la rendre configurable en JSON (une ligne dans le constructeur de FabriqueRegles, ou depuis l'extérieur)
fabrique.Enregistrer("BlackFriday", _ => new RegleBlackFriday());
//    puis dans regles.json :  { "type": "BlackFriday" }
```

**Étape 3 — La tester** (voir § 4).

Cette règle utilise l'interface et les effets déjà disponibles. Son ajout ne demande donc pas de modifier le moteur, le calculateur ou les autres règles.

> Tests associés : `AjoutRegleTests.Methode_1_*`, `Methode_3_une_nouvelle_classe_rendue_configurable_par_enregistrement`
> Dans le projet fourni, `RegleBlackFriday` est définie dans les tests pour vérifier l'ajout d'une classe extérieure au moteur.

---

## 3. Règles à respecter pour une nouvelle règle

| Point | Convention | Pourquoi |
|---|---|---|
| **Code** | Unique, en MAJUSCULES, préfixé : `EXCL-`, `CLIENT-`, `CMD-`, `PROMO-`, `EVT-` | Le moteur refuse un doublon (`InvalidOperationException`) ; le code sert dans les traces |
| **Priorité** | `0–10` exclusions · `40–50` profil client · `60` contenu commande · `70+` promotions / événements (constantes dans `RegleRemise`) | Les exclusions doivent passer en premier pour pouvoir arrêter l'évaluation |
| **ArreteEvaluation** | `true` uniquement pour les exclusions | Une règle « stop » masque toutes les suivantes |
| **Effet** | Utiliser `Remise.Pourcentage`, `Remise.MontantFixe` ou `Remise.Exclusion` | Les fabriques valident les valeurs (taux 0–100, montant > 0) |
| **Cumulable** | `false` = offre exclusive, mise en concurrence avec le cumul des autres | La politique retient automatiquement l'offre la plus avantageuse pour le client |
| **Pas d'horloge système** | Lire `commande.Date`, jamais `DateTime.Now` | Évaluation déterministe et testable |
| **Pas d'effet de bord** | `EstApplicable` et `ProduireEffet` ne modifient rien | Le moteur peut être réutilisé et les règles évaluées dans n'importe quel ordre de test |
| **Paramètres validés au constructeur** | utiliser `ValiderTaux(...)` | Une mauvaise configuration échoue au chargement, pas pendant le calcul |

---

## 4. Vérifier une nouvelle règle

1. **Test unitaire de la règle seule** : cas applicable, cas non applicable, **valeurs limites** (le seuil exact, la veille/le lendemain d'une date…).
   ```csharp
   [Theory]
   [InlineData("2026-11-26", false)]
   [InlineData("2026-11-27", true)]
   [InlineData("2026-11-28", false)]
   public void Black_friday_uniquement_le_jour_J(string date, bool attendu) =>
       Assert.Equal(attendu, new RegleBlackFriday().EstApplicable(Commande(date: DateOnly.Parse(date))));
   ```
2. **Test d'intégration** : ajouter la règle au catalogue et vérifier la décision finale (cumul / exclusivité / plafond).
3. **Test de non-régression** : rejouer le jeu de référence `commandes.json` et vérifier que les résultats **n'ont pas changé** pour les commandes non concernées :
   ```csharp
   foreach (var cas in CasDeReference)
       Assert.Equal(reference.Evaluer(cas.Commande).MontantRemise, etendu.Evaluer(cas.Commande).MontantRemise);
   ```
4. **Ajouter un cas au jeu de données** (`commandes.json`, avec son bloc `"attendu"`) : il est automatiquement repris par les tests `JeuxDeDonneesTests` et par l'application console.
5. Lancer `dotnet test` et vérifier que tous les tests passent.

---

## 5. Retirer ou modifier une règle

- **Retirer** : supprimer la ligne du JSON, la passer en `"actif": false`, ou `moteur.Retirer("CODE")` dans le code.
- **Changer un paramètre** (taux, seuil, dates) : modifier le JSON. Aucune recompilation.
- **Changer la politique** (plafond 30 %, arrondi aux 5 centimes) : section `"politique"` du JSON ou `new PolitiqueRemise { ... }`.

---

## 6. Réutiliser le moteur pour un autre cas métier

Le moteur est générique (`MoteurDeRegles<TContexte, TEffet>`). Pour un autre besoin — par exemple la **validation de commande** — il suffit de choisir d'autres types :

```csharp
var validation = new MoteurDeRegles<Commande, string>()
    .Ajouter(Regle.Creer<Commande, string>("VAL-VIDE", "Commande sans ligne")
        .Quand("aucune ligne", c => c.Lignes.Count == 0)
        .Alors(_ => "La commande ne contient aucun article.")
        .ArreterSiAppliquee().AvecPriorite(0).Construire());

var erreurs = validation.Evaluer(commande).Effets;   // messages d'erreur
```

> Test associé : `AjoutRegleTests.Le_moteur_generique_sert_aussi_un_autre_cas_metier_la_validation_de_commande`
