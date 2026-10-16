# Éclats du Seuil — Plan du simulateur de combat v0.1

> Mission Claude Code 2.1, première passe : audit, questions bloquantes, plan d'architecture.
> **Aucun code applicatif n'a été écrit.** Toutes les valeurs chiffrées citées sont celles, **provisoires et non équilibrées**, du cahier des charges.
> Date : 16 octobre 2026.

## Sommaire

0. [Audit de l'environnement](#0-audit-de-lenvironnement)
1. [Architecture de la solution](#1-architecture-de-la-solution)
2. [Contrats de données](#2-contrats-de-données)
3. [Stratégie déterministe](#3-stratégie-déterministe)
4. [Résolution des commandes](#4-résolution-des-commandes)
5. [Plan de tests](#5-plan-de-tests)
6. [Ambiguïtés à trancher](#6-ambiguïtés-à-trancher)
7. [Risques](#7-risques)
8. [Lots d'implémentation](#8-lots-dimplémentation)
9. [Outils nécessaires](#9-outils-nécessaires)
10. [Synthèse des arbitrages pour le directeur du jeu](#10-synthèse-des-arbitrages-pour-le-directeur-du-jeu)

---

## 0. Audit de l'environnement

| Élément | Constat | Conséquence |
|---|---|---|
| Dépôt du jeu | **Aucun.** Le seul dépôt GitHub accessible est `Bet-Tracker`, un projet sans rapport. | Créer un dépôt dédié (ex. `eclats-du-seuil`) avant le lot 1. Rien du jeu ne va dans `Bet-Tracker`. |
| Fichiers existants | Aucun fichier du jeu. Rien n'a été écrasé. | Pas de contrainte d'héritage. |
| Conteneur de développement cloud | Ubuntu 24.04, 4 cœurs, 15 Go, pas de GPU. Git 2.43, Git LFS 3.4, Docker, Python, Node présents. **.NET SDK absent, Unity absent.** | Le simulateur (sans Unity) peut être entièrement compilé et testé ici une fois le SDK .NET installé. L'éditeur Unity tournera plus tard sur le PC du directeur du jeu. |

---

## 1. Architecture de la solution

### 1.1 Principe

Un cœur C# **pur** (aucune dépendance Unity, réseau, fichier ou horloge système) qui prend en entrée des **définitions immuables** et une **liste de commandes horodatées**, et produit un **journal d'événements** déterministe. Tout le reste (JSON, CLI, plus tard Unity ou un serveur) est un adaptateur autour de ce cœur.

```
                ┌───────────────────────┐
  fichiers JSON │  EdS.Data             │  chargement + validation
  ─────────────►│  (System.Text.Json)   │──────┐
                └───────────────────────┘      │ définitions validées
                                               ▼
┌──────────────────┐   ┌──────────────────────────────────┐
│  EdS.Domain      │◄──│  EdS.Simulation                  │
│  définitions,    │   │  état de combat, horloge, file   │
│  identifiants,   │   │  d'événements, résolution, RNG   │
│  formules pures  │   └──────────────────────────────────┘
└──────────────────┘            ▲              │ journal
         ▲                      │ commandes    ▼
         │              ┌───────────────────────────┐
         └──────────────│  EdS.Cli                  │  scénarios, rapports
                        └───────────────────────────┘
   (plus tard : EdS.Unity / serveur = autres adaptateurs du même cœur)
```

### 1.2 Projets

| Projet | Cible | Rôle | Dépendances | Justification |
|---|---|---|---|---|
| `EdS.Domain` | `netstandard2.1` | Identifiants, définitions immuables (classes, arbres, talents, compétences, effets, altérations), types numériques (`Fixed`), formules pures (dégâts, critique, attributs), règles de construction de build (talents, emplacements). | **Aucune** | `netstandard2.1` est chargeable tel quel par Unity et par n'importe quel serveur .NET. Zéro dépendance = zéro friction d'intégration. |
| `EdS.Simulation` | `netstandard2.1` | État de combat mutable, horloge logique, file d'événements, résolveur de commandes, effets, déclencheurs, RNG, journal. | `EdS.Domain` | Le moteur ne lit ni fichier ni JSON : il reçoit des objets déjà validés. Réutilisable en Unity (client, prédiction) et serveur (autorité). |
| `EdS.Data` | `netstandard2.1` | DTO JSON, chargement, versions de schéma, validation structurelle et sémantique, conversion DTO → définitions du domaine. | `EdS.Domain`, `System.Text.Json` (paquet NuGet, MIT) | Isole la sérialisation : Unity pourra utiliser ce projet ou un autre chargeur (ScriptableObjects, Addressables) sans toucher au domaine. |
| `EdS.Cli` | `net10.0` | Exécuter un scénario, une série de combats, produire journal et statistiques (DPS, dégâts encaissés, temps de survie). | `EdS.Data`, `EdS.Simulation` | Outil d'équilibrage et de reproduction de bugs. Seul projet qui touche disque et console. |
| `EdS.Domain.Tests`, `EdS.Simulation.Tests`, `EdS.Data.Tests` | `net10.0` | Tests unitaires, de propriétés, de scénarios (golden files). | xUnit, FsCheck (libres, open source) | Standard de l'écosystème, intégration `dotnet test`. |

### 1.3 Compatibilité Unity et serveur

- **Langage** : C# 9 maximum dans les projets `netstandard2.1` (niveau supporté par Unity 2022/6). Pas de `required`, pas de types `file`, pas de collections expressions. Les `record` et `init` demandent un petit shim `IsExternalInit` : on l'ajoute dans `EdS.Domain` une fois pour toutes, ou on s'en passe (classes `sealed` avec constructeur et propriétés en lecture seule).
- **Pas de réflexion ni de génération de code au runtime** dans Domain/Simulation (IL2CPP sur mobile les supporte mal).
- **Pas d'allocation inutile dans la boucle de tick** : structures `struct` pour les valeurs, pools éventuels plus tard. On ne sur-optimise pas en v0.1, mais on n'empêche pas l'optimisation.
- **Déterminisme multiplateforme** (PC x64, mobile ARM, serveur Linux) : arithmétique entière à virgule fixe, RNG maison, pas de `float`/`double` dans le chemin de combat (voir §3).
- **Aucun singleton ni état statique mutable** : plusieurs combats peuvent tourner en parallèle (serveur) ou en prédiction (client).

### 1.4 Arborescence prévue

```
eclats-du-seuil/
├─ README.md
├─ PLAN_SIMULATEUR_V0_1.md
├─ global.json                 # version du SDK .NET figée
├─ Directory.Build.props       # LangVersion 9, Nullable, TreatWarningsAsErrors
├─ EdS.sln
├─ src/
│  ├─ EdS.Domain/
│  ├─ EdS.Simulation/
│  ├─ EdS.Data/
│  └─ EdS.Cli/
├─ data/
│  ├─ schemas/                 # JSON Schema (documentation + éditeurs)
│  └─ v0/                      # attributs.json, bastion.json, alterations.json, profils/*.json
├─ scenarios/                  # scénarios de référence (entrées) + journaux attendus
└─ tests/
   ├─ EdS.Domain.Tests/
   ├─ EdS.Simulation.Tests/
   └─ EdS.Data.Tests/
```

---

## 2. Contrats de données

### 2.1 Séparation définitions / état

| Couche | Mutabilité | Exemples | Durée de vie |
|---|---|---|---|
| **Définitions** (`*Def`) | Immuables, partagées | `ClassDef`, `TalentTreeDef`, `TalentDef`, `SkillDef`, `EffectDef`, `StatusDef`, `FormulaConfig` | Chargées une fois, valables tout le combat |
| **Build** (`CharacterBuild`) | Immuable une fois validé | niveau, attributs, talents choisis, compétences équipées par emplacement | Entrée d'un combat |
| **Stats dérivées** (`DerivedStats`) | Recalculées, pas stockées en dur | puissances, PV max, endurance max, armure, critique | Calculées depuis build + modificateurs actifs |
| **État de combat** (`CombatState`, `CombatantState`) | Mutable, **propriété exclusive du moteur** | PV, endurance, charges, résonance, recharges, altérations actives, garde | Un combat |

Règle : l'état ne contient **que des identifiants** vers les définitions, jamais de copie des valeurs de définition (sauf valeurs volontairement figées, ex. puissance d'un DOT à l'application).

### 2.2 Identifiants stables

- Format texte en minuscules, préfixé par domaine : `class.bastion`, `tree.bastion.rempart`, `talent.bastion.rempart.peau_de_fer`, `skill.bastion.frappe_du_rempart`, `status.brulure`, `effect.dot.brulure.standard`.
- Regex de validation : `^[a-z]+(\.[a-z0-9_]+)+$`.
- **Un identifiant publié n'est jamais réutilisé ni renommé** ; une suppression laisse une entrée `deprecated: true`. Les noms affichés (français, traductions) sont séparés de l'identifiant.
- En mémoire, le moteur peut convertir en entiers (`SkillIndex`) après chargement pour la performance, sans changer le format persistant.

### 2.3 Versions des configurations

Chaque fichier JSON porte un en-tête :

```json
{
  "schemaVersion": 1,
  "contentVersion": "0.1.0-provisoire",
  "kind": "classDefinition",
  "data": { "...": "..." }
}
```

- `schemaVersion` (entier) : forme du fichier. Un changement incompatible incrémente ce numéro et s'accompagne d'un migrateur `vN → vN+1` dans `EdS.Data`.
- `contentVersion` (SemVer) : valeurs d'équilibrage. Il est **écrit dans le journal de combat** pour qu'un combat soit rejouable avec les mêmes données.
- Une empreinte (hash SHA-256 du contenu normalisé) est aussi inscrite au journal.

### 2.4 Validation JSON

Deux niveaux, tous deux exécutés au chargement et en test :

1. **Structurelle** : désérialisation stricte (`System.Text.Json`, propriétés inconnues refusées, champs requis vérifiés). Les fichiers JSON Schema dans `data/schemas/` servent de documentation et d'aide dans l'éditeur, sans dépendance de validation à l'exécution en v0.1.
2. **Sémantique** (validateur maison, erreurs accumulées, pas d'arrêt à la première) :
   - identifiants uniques et références existantes ;
   - bornes (critique de base ≤ plafond, charges max ≥ 0, durées et intervalles > 0, intervalle de tick divise ou non la durée, à trancher A12) ;
   - talents : prérequis existants, pas de cycle de prérequis, seuil majeur cohérent ;
   - emplacements : niveaux de déblocage croissants ;
   - effets déclenchés : profondeur de chaîne bornée, pas de cycle statique évident (voir §4.4).

Le résultat est un objet `ValidationReport` (liste d'erreurs avec chemin JSON et code), affiché par la CLI.

### 2.5 Contrats principaux (esquisse)

```csharp
// Domaine — immuable
sealed class SkillDef {
    SkillId Id; SkillKind Kind;            // Active | Ultimate | Passive
    ResourceCost[] Costs;                  // endurance, charges, résonance
    Fixed Cooldown;                        // en ticks logiques
    TargetingRule Targeting;               // Self | Enemy | Ally | Area...
    PowerType PowerType;                   // Physique | Agile | Magique
    Fixed Coefficient;
    EffectDef[] OnCast; EffectDef[] OnHit;
    string[] Tags;                         // pour conditions et déclencheurs
}

sealed class TalentDef {
    TalentId Id; TreeId Tree; TalentTier Tier;   // Normal | Majeur
    int Cost;                                    // 1
    int MinPointsInTree;                         // 18 pour un majeur (avant achat)
    TalentId[] Prerequisites;
    ModifierDef[] Modifiers; TriggerDef[] Triggers;
}

sealed class CharacterBuild {
    ClassId Class; int Level;
    AttributeBlock Attributes;
    IReadOnlyDictionary<TalentId,int> Talents;
    SkillId[] ActiveSlots; SkillId? Ultimate; SkillId[] PassiveSlots;
}

// Simulation — entrées et sorties
readonly struct Command { long Tick; CombatantId Actor; CommandKind Kind; SkillId Skill; TargetSpec Target; }
readonly struct CombatEvent { long Tick; long Sequence; EventKind Kind; CombatantId Source; CombatantId Target; ... }

interface IRandomSource { uint NextUInt(); }     // injectable, graine fixe
interface ICombatLog { void Append(in CombatEvent e); }

sealed class CombatEngine {
    CombatEngine(GameRules rules, IRandomSource rng, ICombatLog log);
    void Setup(IEnumerable<CombatantSetup> combatants);
    void Enqueue(Command c);
    void RunUntil(long tick);                     // ou Step()
}
```

Le validateur de build (`BuildValidator`) est dans le domaine : il vérifie points disponibles (`niveau − 1`), seuil majeur, prérequis, nombre d'emplacements selon niveau, ultime seulement à partir du niveau 10.

---

## 3. Stratégie déterministe

### 3.1 Horloge logique

- Le temps est un **entier de ticks**. Proposition : **1 tick = 50 ms (20 ticks/s)**. Toutes les durées des définitions sont exprimées en millisecondes dans le JSON et converties en ticks au chargement, avec **refus** si la conversion n'est pas exacte (évite les arrondis cachés).
- Jamais de lecture de l'horloge système dans le moteur.

### 3.2 Ordre stable des événements

File de priorité ordonnée par la clé `(tick, phase, séquence)` :

| Phase (ordre dans un même tick) | Contenu |
|---|---|
| 1. Expirations | fin d'altérations, de buffs, de fenêtres |
| 2. Ticks périodiques | DOT, régénérations |
| 3. Commandes | commandes des acteurs, triées par (ordre d'initiative stable, id de combattant) |
| 4. Réactions | événements déclenchés, profondeur croissante |
| 5. Fin de tick | contrôle de mort, fin de combat |

`séquence` est un compteur monotone attribué à l'insertion : deux événements identiques en tick et phase gardent l'ordre d'insertion. **L'ordre des phases est lui-même une décision à valider (A10).**

### 3.3 RNG injectable

- Interface `IRandomSource`, implémentation maison **PCG32** ou **xoshiro128\*\*** (quelques lignes, spécifiées, identiques partout). On **n'utilise pas** `System.Random` dont l'algorithme a changé entre versions de .NET.
- Graine fixe en entrée du combat, inscrite au journal.
- Option : un flux RNG par combattant (`seed ⊕ id`) pour qu'ajouter un combattant ne décale pas les tirages des autres.
- Tirage de pourcentage : entier dans `[0, 10000)` comparé à une chance en points de base ; pas de flottant.

### 3.4 Calcul numérique

- Type `Fixed` : entier 64 bits à échelle **10 000** (4 décimales). Multiplications/divisions avec arrondi explicite.
- PV, dégâts finaux, charges, endurance : **entiers**.
- Formule d'armure `1000 / (1000 + armure)` calculée en `Fixed` avec la constante configurable.
- **Un seul arrondi final** des dégâts, mode à trancher (A9 : au plus proche, demi vers le haut, proposé). Les étapes intermédiaires gardent 4 décimales.
- Débordements : `checked` en mode test, plafonds explicites en production.

### 3.5 Journal reproductible

- En-tête : version moteur, `contentVersion`, empreinte des données, graine, liste des builds, liste des commandes.
- Corps : un événement par ligne (JSON Lines), champs ordonnés, nombres entiers uniquement → **le journal est comparable octet par octet**.
- Rejouer = recharger l'en-tête et les commandes → produire un journal identique. Ce test est la garantie centrale du déterminisme.

---

## 4. Résolution des commandes

### 4.1 Pipeline d'une commande « utiliser une compétence »

```
1. Validation   acteur vivant, non étourdi ; compétence équipée ; pas en recharge ;
                pas d'autre action en cours (A6) ; cible valide et à portée (A7)
2. Coûts        vérifier toutes les ressources, PUIS les débiter toutes (atomique)
                échec → événement CommandRejected(motif), aucun effet
3. Recharge     démarrer la recharge (au lancement, proposé, A8)
4. Ciblage      résoudre la liste de cibles de manière déterministe (tri par id)
5. Pour chaque cible :
   5a. Dégâts   puissance → × coefficient → × bonus additifs (somme) → × multiplicateurs
                (produit) → critique (tirage RNG, plafonds) → × 1000/(1000+armure effective)
                ou résistance → garde/protections → arrondi final → application aux PV
   5b. Effets   altérations, gains/consommations de charges, résonance, soins
6. Événements   SkillCast, DamageDealt, CriticalHit, StatusApplied, ChargeGained... publiés
7. Déclencheurs les talents/passifs abonnés réagissent (§4.4)
8. Expiration   durées et recharges avancent avec l'horloge, pas avec les commandes
```

### 4.2 Détail des règles déjà fixées

- Critique : chance = 5 % + bonus, **plafonnée à 60 %** ; multiplicateur = ×1,5 + bonus, **plafonné à ×2,5**.
- Armure effective = max(0, armure − réduction d'armure).
- Charges de rempart : maximum 5 ; un gain au-delà du maximum est **perdu** (proposé, à valider A4) ; une consommation impossible fait échouer la commande à l'étape 2.
- DOT : durée 6 s, tick toutes les 2 s (3 ticks), **puissance figée à l'application**, **résistances relues à chaque tick** ; réapplication = renouvellement de la durée, pas de cumul libre.

### 4.3 Altérations

Chaque `StatusDef` décrit : catégorie (DOT, contrôle, affaiblissement), durée, période, règle de cumul (`Refresh`, `RefreshKeepStronger`, `Stack(max)`, `Ignore`), résistance applicable (Volonté), immunité éventuelle après expiration (A14). Les six altérations du cahier (brûlure, saignement, gel, étourdissement, fragilité, rupture de garde) sont **définies en données**, pas codées en dur ; le code ne connaît que les catégories.

### 4.4 Limites contre les cycles de déclenchement

1. **Profondeur maximale** de chaîne : chaque événement porte `depth` ; un déclencheur ne s'exécute que si `depth < MaxTriggerDepth` (proposé : 4). Au-delà : événement `TriggerSuppressed` au journal.
2. **Un déclencheur ne réagit jamais à un événement issu de lui-même** (champ `originTriggerId`).
3. **Budget par tick** : nombre maximal d'événements traités par tick (ex. 1 000). Dépassement = erreur fatale en test, arrêt propre en production. Garantit la terminaison.
4. **Contrôle statique au chargement** : graphe « déclencheur → événements qu'il peut produire » ; un cycle est signalé en avertissement (ou en erreur selon décision A15).
5. Les gains de charges par déclencheur respectent le plafond de 5, ce qui borne aussi les boucles gain/consommation.

---

## 5. Plan de tests

| Domaine | Type | Exemples |
|---|---|---|
| Dégâts / armure | Unitaire + propriété | armure 0 → facteur 1 ; armure = constante → facteur 0,5 ; dégâts décroissants quand l'armure croît ; jamais négatifs ; armure négative traitée comme 0 |
| Ordre de résolution | Unitaire | cas chiffré pas à pas vérifiant chaque étape (puissance, coefficient, bonus, multiplicateurs, critique, armure, garde, arrondi) |
| Critiques | Propriété | chance effective ∈ [0 %, 60 %] et multiplicateur ∈ [×1,5, ×2,5] pour tout bonus ; fréquence observée ≈ chance sur 100 000 tirages à graine fixe |
| Attributs | Unitaire | +2 puissance par Force, +10 PV par Vitalité, etc., depuis la configuration |
| Coûts | Unitaire | commande refusée si une seule ressource manque, aucune ressource débitée ; recharge respectée au tick près |
| Charges | Propriété | pour toute séquence de gains/consommations : 0 ≤ charges ≤ 5 |
| Talents | Unitaire + propriété | 29 points au niveau 30, 0 au niveau 1 ; majeur refusé avec 17 points dans la branche, accepté avec 18 ; prérequis manquant refusé ; **au plus un talent majeur par build** (conséquence arithmétique : 19 + 19 > 29) |
| Emplacements | Unitaire | 2/3/4 actifs aux niveaux 1/5/10 ; ultime refusé avant 10 ; passifs 1/2/3/4 aux niveaux 1/5/20/30 |
| DOT | Unitaire | 3 ticks sur 6 s ; puissance figée même si l'attaquant change de stats ; résistance relue ; réapplication renouvelle sans cumuler ; expiration au bon tick |
| Ordre des événements | Unitaire | deux événements même tick → ordre phase puis séquence |
| Reproductibilité | Propriété | même graine + mêmes commandes → journal identique octet par octet (1 000 graines aléatoires) ; rejouer depuis le journal |
| Terminaison | Propriété | données générées avec déclencheurs mutuels → le combat termine, `TriggerSuppressed` présent, budget jamais dépassé |
| Données | Unitaire | fichier valide accepté ; référence cassée, id dupliqué, cycle de prérequis → erreurs listées |
| Scénarios des trois builds | Golden files | Rempart défensif, Contrecoup offensif, Gardien soutien/solo contre un mannequin de référence : journal comparé au fichier attendu ; tout changement de formule fait apparaître une différence à relire |

Commande unique : `dotnet test` à la racine. Les scénarios dorés se régénèrent volontairement avec `dotnet run --project src/EdS.Cli -- regenerate-golden` (jamais automatiquement).

---

## 6. Ambiguïtés à trancher

**B = bloquante** (empêche de coder le lot concerné), **R = reportable** (une valeur par défaut configurable suffit).

| # | Sujet | Question | Proposition par défaut | Classe |
|---|---|---|---|---|
| A1 | Garde | La garde est-elle une posture maintenue (réduction de dégâts, coût d'endurance continu) ou un bouclier absorbant ? Où s'applique-t-elle exactement après l'armure ? | Posture : réduction en % après armure, drain d'endurance par seconde | **B** |
| A2 | Parade parfaite | Fenêtre de timing (ms) ? Le simulateur sans UI la modélise comment : commande « parer » arrivée N ticks avant l'impact ? | Fenêtre de 200 ms (4 ticks) avant l'impact ; parade parfaite = annule les dégâts + 1 charge | **B** |
| A3 | Endurance | Valeur de base, régénération (par seconde, délai après dépense), effet à 0 (garde brisée ?) | Base 100, +10/s après 1 s sans dépense | **B** |
| A4 | Charges | Gain au-delà de 5 perdu ? Les charges décroissent-elles hors combat ou avec le temps ? | Perdu ; pas de décroissance en combat | R |
| A5 | Résonance | Comment se gagne-t-elle (dégâts subis, infligés, parades) ? Maximum ? Remise à zéro à l'utilisation de l'ultime ? | Max 100, gain sur dégâts subis et parades, vidée par l'ultime | **B** pour l'ultime |
| A6 | Temps d'incantation | Les compétences sont-elles instantanées, avec temps global de recharge (GCD), ou avec animation bloquante ? | Instantanées + GCD 1 s configurable | **B** |
| A7 | Ciblage et distance | Le simulateur modélise-t-il des positions/portées, ou uniquement des cibles logiques ? | Cibles logiques sans position en v0.1 ; zones = « toutes les cibles ennemies » | **B** (impacte Onde de rupture) |
| A8 | Recharge | Démarre au lancement ou à la fin de l'effet ? | Au lancement | R |
| A9 | Arrondi | Mode d'arrondi final ; dégâts minimum 1 ? | Au plus proche (demi vers le haut), minimum 1 si le coup touche | R |
| A10 | Ordre intra-tick | Ordre des phases (expirations avant commandes ?) ; deux combattants agissant au même tick | Celui du §3.2, ordre d'initiative puis id | R |
| A11 | Puissance de base | Puissance avant attributs ? Bastion utilise-t-il la puissance physique pour toutes ses compétences ? Valeurs d'armure de base ? | Puissance = attributs seulement ; Bastion = physique | **B** |
| A12 | DOT | Les DOT peuvent-ils critiquer ? Passent-ils par l'armure, la résistance ou rien ? Tick à l'application ou après 2 s ? Que faire si la durée n'est pas un multiple de la période ? | Pas de critique ; résistance aux altérations ; premier tick à +2 s ; durée multiple de la période imposée | **B** |
| A13 | Cumul | Deux brûlures de sources différentes : cumul ou remplacement ? Priorité au plus fort ? Bonus additifs entre eux et multiplicatifs entre catégories ? | Une instance par (altération, source), renouvelée ; bonus additifs intra-catégorie, multiplicatifs inter-catégories | **B** |
| A14 | Contrôles | Durées d'étourdissement/gel, rendements décroissants, immunité temporaire, effet de la Volonté (réduction de durée ou chance de résister ?) | Volonté réduit la durée ; immunité 3 s après un étourdissement | R (B pour les scénarios avec contrôles) |
| A15 | Cycles | Un cycle détecté statiquement dans les déclencheurs est-il une erreur de données ou toléré grâce à la limite de profondeur ? | Avertissement + limite de profondeur 4 | R |
| A16 | Talents | Le seuil « 18 points investis » compte-t-il tous les talents de la branche ? Plusieurs rangs par talent ? Respécialisation ? | Tous les points de la branche ; rangs possibles via `maxRank` | **B** |
| A17 | Passifs vs talents | Les emplacements passifs (1 à 4) sont-ils des compétences passives choisies à part, ou les talents eux-mêmes ? | Système distinct des talents | **B** |
| A18 | Ultime | L'ultime occupe-t-il un emplacement en plus des 4 actifs ? | Emplacement dédié en plus | R |
| A19 | Fragilité / rupture de garde | Effets chiffrés (± % dégâts subis, garde désactivée combien de temps ?) | Fragilité +10 % dégâts subis ; rupture : garde impossible 3 s | R |
| A20 | Compétences candidates | Définitions complètes des 7 compétences (coûts, coefficients, recharges, effets) | Valeurs factices clairement marquées « provisoire » | **B** pour le lot 6 |
| A21 | Adversaire de référence | Contre quoi mesure-t-on les trois profils (mannequin passif, attaquant scripté) ? | Mannequin à PV infinis + attaquant scripté à DPS fixe | R |
| A22 | Mort et fin de combat | Dégâts excédentaires, mort simultanée, durée maximale du combat | Fin au premier tick où un camp est mort ; égalité possible ; durée max 5 min | R |
| A23 | Altérations / attributs | La résistance aux altérations (+2 par Volonté) se convertit comment en réduction (formule type armure ?) | Même forme que l'armure avec sa propre constante | **B** pour les DOT |

---

## 7. Risques

| Risque | Impact | Parade |
|---|---|---|
| Non-déterminisme entre plateformes (flottants, `System.Random`, ordre de `Dictionary`) | Rejeu impossible, futur serveur faux | Virgule fixe, RNG maison, collections triées dans le chemin de combat, test de rejeu systématique |
| Sur-architecture (ECS, réseau, plugins) trop tôt | Lenteur pour un développeur solo | 4 projets seulement, pas d'injection de dépendances lourde, interfaces seulement aux frontières (RNG, journal, données) |
| Données incomplètes présentées comme équilibrées | Mauvaises décisions de design | `contentVersion` « provisoire », bannière dans les rapports CLI |
| Boucles de déclencheurs | Combat infini, gel | Limite de profondeur, budget par tick, contrôle statique |
| Compatibilité Unity (version C#, IL2CPP) | Réécriture lors de l'intégration | `netstandard2.1`, C# 9, pas de réflexion ; essai d'import dans Unity dès que le directeur du jeu a l'éditeur |
| Ambiguïtés de règles tranchées implicitement dans le code | Dette de design invisible | Toute valeur par défaut est dans la configuration et listée ici |
| Modèle du temps (ticks de 50 ms) trop grossier pour la parade | Fenêtres irréalistes | Pas configurable ; passer à 20 ms si A2 l'exige |

---

## 8. Lots d'implémentation

Chaque lot est petit, livrable seul, avec ses tests. Aucun commit sans accord sur la portée.

| Lot | Contenu | Critères d'acceptation | Commandes de test | Dépend des décisions |
|---|---|---|---|---|
| **0. Socle** | Dépôt dédié, `global.json`, solution, 4 projets + 3 projets de tests vides, `Directory.Build.props`, README, CI GitHub Actions (`dotnet test`) | `dotnet build` et `dotnet test` verts ; les projets domaine compilent en `netstandard2.1` | `dotnet build -warnaserror` · `dotnet test` | création du dépôt |
| **1. Numérique et RNG** | `Fixed`, arrondis, PCG32, `IRandomSource` | Opérations `Fixed` testées sur bornes ; séquence PCG32 identique aux vecteurs de référence | `dotnet test --filter Category=Numeric` | A9 |
| **2. Attributs et formules** | Attributs → stats dérivées, formule de dégâts complète, critique plafonnée | Cas chiffrés du §5 verts ; propriétés armure/critique vertes | `dotnet test --filter Category=Formula` | A11, A1 (place de la garde) |
| **3. Données et validation** | DTO, chargeur JSON, versions, validateur sémantique, fichiers `data/v0` provisoires | Données de référence chargées ; chaque erreur de validation a un test ; CLI `validate` affiche le rapport | `dotnet test --filter Category=Data` · `dotnet run --project src/EdS.Cli -- validate data/v0` | A16, A17 |
| **4. Builds** | Validateur de build : points, seuil majeur, prérequis, emplacements | Tests talents et emplacements du §5 verts ; les 3 profils sont des builds valides | `dotnet test --filter Category=Build` | A16, A17, A18 |
| **5. Moteur minimal** | Horloge, file d'événements, commandes, coûts, recharges, dégâts directs, journal JSON Lines, rejeu | Combat Bastion vs mannequin reproductible ; test de rejeu sur 1 000 graines vert | `dotnet test --filter Category=Engine` · `dotnet run --project src/EdS.Cli -- simulate scenarios/mannequin.json` | A3, A6, A7, A10 |
| **6. Ressources Bastion** | Endurance, charges (plafond 5), résonance, garde, parade parfaite | Propriété « 0 ≤ charges ≤ 5 » verte ; parade parfaite donne +1 charge dans la fenêtre seulement | `dotnet test --filter Category=Resources` | A1, A2, A4, A5 |
| **7. Altérations et déclencheurs** | DOT, contrôles, cumul, déclencheurs avec limites de cycle | Tests DOT et terminaison verts ; `TriggerSuppressed` journalisé | `dotnet test --filter Category=Status` | A12, A13, A14, A15, A23 |
| **8. Compétences et profils** | Les 7 compétences candidates en données, 3 profils, scénarios dorés, rapport CLI (DPS, dégâts évités, survie) | Les 3 scénarios dorés passent ; rapport lisible avec bannière « valeurs provisoires » | `dotnet test --filter Category=Scenario` · `dotnet run --project src/EdS.Cli -- report scenarios/` | A19, A20, A21, A22 |

---

## 9. Outils nécessaires

**Maintenant (pour le lot 0) :**

| Outil | Pourquoi | Coût | Où |
|---|---|---|---|
| **SDK .NET 10 (LTS)** | Compiler et tester toute la solution | Gratuit | Je peux l'installer dans le conteneur cloud ; sur ton PC seulement si tu veux compiler localement |
| **Git** (+ Git LFS plus tard pour les assets Unity) | Versionner | Gratuit | Déjà présent dans le conteneur |
| **Un dépôt GitHub dédié** | Stocker le projet, faire tourner la CI | Gratuit (privé possible) | À créer par toi, puis à ajouter au projet |

**Optionnel :** un éditeur pour lire le code sur ton PC : Visual Studio Code + extension C# Dev Kit (gratuits), ou Visual Studio Community (gratuit pour un développeur indépendant). Pas indispensable pour la phase simulateur.

**Pas maintenant :** Unity (seulement à l'intégration, après le lot 8), Docker, base de données, serveur, service cloud, logiciel payant (Rider, etc.).

Bibliothèques NuGet prévues, toutes libres : `System.Text.Json`, `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `FsCheck.Xunit`.

---

## 10. Synthèse des arbitrages pour le directeur du jeu

**À trancher avant tout code de combat (bloquants) :**

1. **Garde et parade (A1, A2)** : nature de la garde, place dans la formule, fenêtre de la parade parfaite.
2. **Endurance et résonance (A3, A5)** : valeurs de base, régénération, gains de résonance.
3. **Rythme d'action (A6)** : instantané + temps global de recharge, ou animations bloquantes.
4. **Positions (A7)** : simulateur sans positions en v0.1, oui ou non.
5. **Puissance de base et type de puissance du Bastion (A11)**.
6. **DOT et cumul (A12, A13, A23)** : critique, armure ou résistance, cumul entre sources, formule de résistance aux altérations.
7. **Talents et passifs (A16, A17)** : comptage du seuil de 18 points, passifs distincts des talents ou non.
8. **Fiches complètes des 7 compétences (A20)** avant le lot 8.

**Choix techniques proposés, à valider :**

- C# `netstandard2.1` / C# 9 pour le cœur, .NET 10 pour la CLI et les tests.
- Arithmétique à virgule fixe (échelle 10 000) et RNG PCG32 maison, aucun flottant dans le combat.
- Tick logique de 50 ms.
- Limite de profondeur des déclencheurs à 4 et budget de 1 000 événements par tick.
- Un dépôt GitHub dédié, aucun commit sans accord sur sa portée.

**Constat à confirmer :** avec 29 points et un seuil de 19 points par talent majeur, **un build ne peut avoir qu'un seul talent majeur**. Est-ce voulu ?

Dès validation, je commence par le lot 0 (socle) puis le lot 1, qui ne dépendent d'aucune décision bloquante de gameplay.
