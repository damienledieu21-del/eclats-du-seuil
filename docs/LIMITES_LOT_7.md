# Contrat de limites du lot 7 (proposition, correctifs 6.1 et 6.1.1)

Statut : **contrat documentaire**. Rien de ce qui suit n'est implémenté, sauf ce qui est marqué « en place ». Il fixe à l'avance les bornes et les règles de terminaison que le lot 7 (altérations, déclencheurs) devra respecter, pour que le moteur général des états ne soit pas conçu sans garde-fous. Toutes les valeurs sont **provisoires et configurables** dans `rules.json`, bloc `combat.limits` (nouveaux champs au lot 7, donc `schemaVersion` 3 ; voir [DONNEES.md](DONNEES.md)).

## Principes

1. **Refuser tôt** ce qui peut l'être : une limite qui dépend seulement du scénario (nombre de combattants, de commandes) est vérifiée par `CombatSetup.Validate`, avant le premier tick, avec un diagnostic sur le bon chemin, comme les limites numériques du correctif 6.1.
2. **Arrêter proprement** ce qui ne se voit qu'en simulation (cascade de déclencheurs, file qui gonfle) : `simulationAborted` avec une raison stable, résultat `aborted`, code de sortie 2 et bandeau `DIAGNOSTIC` dans la CLI. Jamais d'exception, jamais de boucle infinie.
3. **Supprimer sans arrêter** ce qui relève du jeu (chaîne trop profonde, déclencheur qui se redéclencherait) : l'action est abandonnée et journalisée (`triggerSuppressed`), le combat continue.
4. Le résultat d'une limite est **déterministe** : même scénario, même journal, octet par octet.
5. Un précontrôle ne vaut que pour les calculs qu'il recense. Chaque calcul ajouté au lot 7 (effet, déclencheur, état) doit être ajouté au précontrôle numérique de `CombatSetup.Validate` ou prouvé borné par les limites ci-dessous ; à défaut, seul le filet `overflow` le protège.

## Trois natures de valeurs

Les nombres de ce document et des règles ne se valent pas ; chaque valeur citée indique sa nature.

| Nature | Rôle | Exemples actuels | Qui la change |
|---|---|---|---|
| **Limite technique** (enveloppe) | Garantit que les calculs restent représentables et que la simulation se termine. Provisoire en pré-alpha, choisie très au-delà du jeu prévu ; ce n'est ni un objectif ni une promesse de performance. | dégâts bruts ≤ 10⁹, défenses effectives ≤ 10⁹, constantes de défense ≤ 10⁹, attributs ≤ 10⁶, coefficient ≤ 100, durées ≤ 86 400 000 ms, 1 000 événements comptés par tick | le code, avec un test aux bornes |
| **Paramètre d'équilibrage** | Règle du jeu, provisoire, dans `rules.json` ou les classes ; doit rester à l'intérieur des limites techniques. | recharge globale 1 000 ms, délai de régénération 1 000 ms, constantes de défense 1 000, garde −60 % | les données, validées par Dam |
| **Capacité mesurée** | Ce qui a réellement été simulé et observé ; seule base pour parler de charge. | 3 scénarios de référence (1 personnage contre 1 mannequin ou 1 assaillant, 15 à 20 s) : au plus **7 événements comptés par tick**, 131 à 260 lignes de journal (correctif 6.1.1) | une mesure, datée |

Aucune capacité au-delà de ces mesures n'est revendiquée : 64 combattants ou 100 000 événements en attente sont des enveloppes proposées, pas des volumes testés.

## Limites proposées

| Limite | Valeur initiale | Champ prévu | Dépassement | Justification |
|---|---|---|---|---|
| Combattants par combat | 64 | `combat.limits.maxCombatants` | refus à la validation (`combatants`) | Les scénarios prévus (lot 8 : 1 contre 1, petits groupes) en ont moins de 10. 64 laisse de la marge aux essais de zone tout en gardant les ciblages de zone (parcours de tous les ennemis à chaque impact) et le journal de petite taille. |
| Commandes par scénario | 20 000 | `combat.limits.maxCommands` | refus à la validation (`commands`) | Un scénario de 10 min (durée maximale actuelle, 12 000 ticks) avec une commande à chaque tick en a 12 000. Au-delà, le fichier est très probablement généré par erreur. |
| Commandes par acteur et par tick | 4 | `combat.limits.maxCommandsPerActorPerTick` | refus à la validation (`commands[i].atMs`) | Un joueur peut au plus combiner garde, parade et une compétence au même tick ; 4 garde une marge. Empêche qu'un scénario sature la phase 2 d'un seul tick. |
| Événements en attente dans la file | 100 000 | `combat.limits.maxPendingEvents` | arrêt `queueLimitExceeded` | 64 combattants × (recharges, états, tours) restent sous 10 000 en usage normal ; un facteur 10 de marge. Une file qui dépasse signale un état qui se reprogramme sans fin. |
| Budget de travail par tick | 10 000 unités | `combat.limits.maxWorkPerTick` | arrêt `workBudgetExceeded` | Voir ci-dessous : distinct du budget d'événements journalisés. 10 × le budget journalisé, car une unité de travail ne produit pas toujours de ligne. |
| Événements de simulation comptés par tick | 1 000 (**en place**) | `combat.limits.maxEventsPerTick` | arrêt `eventBudgetExceeded` (**en place**) | Valeur du plan (§4.4). Compte les événements émis par les phases 1 à 6 d'un tick, pas toutes les lignes du journal (voir ci-dessous). |
| Lignes d'initialisation | 1 + nombre de combattants (≤ 65) | dérivée de `maxCombatants` | sans objet : bornée par construction | En-tête + une ligne `combatant` par participant, au tick 0, avant tout événement compté. |
| Lignes d'arrêt et de finalisation | 2 + nombre de combattants (≤ 66) | dérivée de `maxCombatants` | sans objet : bornée par construction | Au plus une ligne `simulationAborted`, une `combatantSummary` par participant, une `combatEnded`. Jamais d'autre ligne après l'arrêt. |
| Profondeur de chaîne de déclencheurs | 4 | `combat.limits.maxTriggerDepth` | `triggerSuppressed`, le combat continue | Valeur du plan (§4.4, A15) : permet « parade → gain de Charge → passif → effet » sans permettre de cascade longue. |

### Enveloppes séparées : simulation, initialisation, finalisation

Le budget `maxEventsPerTick` (en place) compte les **événements de simulation** émis pendant les phases d'un tick ; il ne compte ni l'en-tête et les lignes `combatant` écrites à la construction du combat, ni `simulationAborted`, `combatantSummary` et `combatEnded` écrites à l'arrêt ou à la fin. Ces lignes ont leur propre enveloppe, bornée explicitement par le nombre de combattants (tableau ci-dessus) : un journal complet compte au plus (1 + N) + durée en ticks × `maxEventsPerTick` + (2 + N) lignes. Le lot 7 ne doit ajouter aucune ligne à l'initialisation ou à la finalisation qui ne soit bornée par N ; un total à signaler en fin de combat (ex. événements reportés perdus, voir plus bas) devient un champ de `combatEnded`, jamais une ligne par élément.

### Budget de travail distinct du budget d'événements

Le budget actuel (`maxEventsPerTick`) compte les **événements écrits pendant la simulation**. Or une partie du travail du moteur n'écrit rien : échéances périmées ignorées (M02), conditions de déclencheur évaluées à faux, déclencheurs supprimés, effets sans changement visible. Une boucle de travail silencieux ne serait jamais arrêtée par le budget d'événements. Le lot 7 ajoute donc un compteur de **travail**, remis à zéro à chaque tick, incrémenté d'une unité pour : chaque élément retiré de la file (y compris périmé), chaque évaluation de déclencheur, chaque application d'effet. Les deux budgets coexistent ; le premier atteint arrête le combat avec sa raison.

## Provenance et prévention des cycles

### Identité causale des racines

Une **racine** est un événement qui n'est produit par aucun déclencheur. Son identité est un n-uplet stable, calculé sans compteur global ni ordre d'insertion :

| Racine | Identité (`root`) |
|---|---|
| Commande du scénario | `command:<index dans le scénario>` |
| Tour d'un assaillant | `assailantTurn:<id>:<numéro du tour, 0, 1, 2…>` |
| Fin de recharge | `cooldown:<acteur>:<compétence>:<tick d'échéance>` |
| Échéance d'état | `stateExpiry:<porteur>:<instance>:<génération>` |
| Effet périodique (entretien, futurs effets sur la durée) | `periodic:<porteur>:<source>:<tick>` |
| Régénération | `regeneration:<porteur>:<tick>` |

`rootSeq` (numéro `seq` de la première ligne journalisée de la racine) est seulement une aide à la lecture du journal ; l'identité qui fait foi est `root`, indépendante de la présence ou non de lignes.

### Identité d'un déclencheur

Un déclencheur est identifié par **(définition, porteur, instance)** :

- `definition` : identifiant stable du talent, de la compétence passive ou de l'état qui le définit ;
- `bearer` : combattant qui le porte ;
- `instance` : identifiant d'instance de l'état qui le porte (contrat des états du correctif 6.1), ou `0` pour un déclencheur permanent (talent, passif).

Deux porteurs de la même définition sont deux déclencheurs distincts. La **génération** n'entre pas dans l'identité utilisée pour les cycles (renouveler un état ne permet pas de se redéclencher dans la même chaîne) ; elle sert seulement à écarter les événements périmés.

### Chaîne causale et héritage entre branches sœurs

Chaque événement porte `root`, `depth`, `trigger` (identité ci-dessus, `null` pour une racine) et `chain`, l'ensemble des identités de déclencheurs traversés depuis la racine. `chain` est une **valeur immuable** : un événement produit par le déclencheur D reçoit `chain(parent) ∪ {D}`, construite pour lui seul. Si un événement provoque deux déclencheurs B et C (branches sœurs), la chaîne de C ne contient jamais B, et inversement : ce que fait une branche n'influence ni la profondeur ni la détection de cycle de l'autre. Les sœurs partagent seulement le budget de travail et le budget d'événements du tick.

### Calcul exact de la profondeur

- `depth(racine) = 0`.
- Les **conséquences directes** d'une action gardent la profondeur de l'action : impacts d'une même attaque (y compris d'une zone), état appliqué par l'attaque elle-même, ressource gagnée par la règle de base (Résonance, Charge de parade), mort et `stateRemoved` qui en découlent.
- Un événement produit par un déclencheur a `depth = depth(événement déclencheur) + 1`. Seul le passage par un déclencheur augmente la profondeur.
- Un déclencheur ne s'exécute que si `depth(événement déclencheur) < maxTriggerDepth` ; ses événements ont donc une profondeur ≤ `maxTriggerDepth` (4).

Exemple : parade parfaite (racine `command:3`, profondeur 0) → gain de Charge (conséquence directe, 0) → passif « à chaque Charge » (déclencheur, 1) → effet du passif (1) → déclencheur réagissant à cet effet (2)… La cinquième réaction, qui aurait la profondeur 5, est supprimée (`triggerSuppressed`, `reason: "depth"`).

### Règles

1. Profondeur : voir ci-dessus ; sinon `triggerSuppressed` (`trigger`, `depth`, `root`, `reason: "depth"`).
2. Cycle : un déclencheur ne réagit jamais à un événement dont la `chain` contient déjà son identité (généralise `originTriggerId` du plan : empêche A → B → A avant même la limite de profondeur) ; `triggerSuppressed` avec `reason: "cycle"`.
3. Un événement issu d'une instance d'état retirée ou d'une génération périmée n'est jamais exécuté (voir « Échéances périmées »).
4. Contrôle statique au chargement des données : graphe « déclencheur → événements qu'il peut produire ». Proposition par défaut (A15, à trancher) : un cycle est un **avertissement** dans le rapport de validation, la limite de profondeur garantissant la terminaison.
5. Les gains de ressources par déclencheur restent plafonnés (Charges 5, Résonance 100), ce qui borne aussi les boucles gain/consommation.

### Diagnostics non déclencheurs

`triggerSuppressed`, `simulationAborted`, les dépassements de budget et toute ligne de diagnostic future ne sont **jamais** observables par un déclencheur : ils n'appartiennent à aucun type d'événement auquel on peut s'abonner, ne sont pas présentés aux abonnés et ne consomment pas d'évaluation de déclencheur (ils consomment une unité de travail, comme toute écriture). Une suppression ne peut donc pas en provoquer une autre.

### Échéances périmées

Tout événement programmé pour le compte d'un état porte `sourceInstance` et `sourceGeneration`. À son tour d'exécution, il est **annulé** si l'instance n'existe plus ou si sa génération a changé (renouvellement, retrait, mort du porteur) : il est retiré de la file, compte une unité de travail, n'est pas journalisé et ne déclenche rien. C'est la règle des fins d'état du correctif 6.1 (M02), étendue à tous les effets d'état (périodiques, retardés). Le retrait d'une instance ne parcourt pas la file : l'annulation se fait paresseusement à l'échéance, ce qui garde un coût borné et un ordre déterministe.

### Ordre stable des abonnés

Pour un même événement, les déclencheurs abonnés sont évalués dans un ordre total indépendant de l'ordre d'inscription et de toute structure non ordonnée : initiative du porteur décroissante, puis identifiant du porteur (ordinal), puis identifiant de définition (ordinal), puis identifiant d'instance croissant. Les événements qu'ils produisent sont ajoutés à la file dans cet ordre (numéros `seq` croissants). Parcourir un dictionnaire ou un ensemble de hachage pour trouver les abonnés est interdit.

## Événement programmé dans une phase déjà passée

Ordre d'un tick : expirations → commandes → résolution → périodiques → régénération → fin de tick. Un effet résolu en phase P du tick T peut vouloir programmer un événement en (T, Q).

- **Q après P** (même tick) : exécuté normalement dans la phase Q du tick T.
- **Q = P** : ajouté à la fin de la phase en cours (ordre d'insertion), dans le même tick, avec `depth + 1` et compté dans le budget de travail. C'est déjà le comportement de la file pour la phase en cours.
- **Q avant P** : la phase est passée. Règle proposée : l'événement est reporté au tick T + 1, même phase Q, avec un champ `deferredFromTick: T` au journal. Jamais exécuté « en arrière » dans le tick courant.
- **Provenance conservée** : un événement reporté garde exactement `root`, `depth`, `trigger`, `chain`, `sourceInstance` et `sourceGeneration`. Le report n'augmente pas la profondeur et ne crée pas de nouvelle racine ; seul `deferredFromTick` s'ajoute. Ses contrôles (instance périmée, cycle, profondeur) sont refaits au moment de l'exécution, sur l'état du tick T + 1.
- **Report depuis le dernier tick** : si T est le dernier tick du combat (T + 1 ≥ durée en ticks), ou si le combat se termine pendant T (équipe vaincue, arrêt), l'événement n'est pas exécuté. Il n'écrit pas de ligne par événement ; leur nombre est donné par un champ `deferredDropped` de `combatEnded` (enveloppe de finalisation bornée). Un combat ne se prolonge jamais pour vider la file.

**En place depuis le correctif 6.1** : `EventQueue` refuse (exception de programmation) tout événement programmé avant la position déjà atteinte. Avant, un tel événement restait en tête du tas et bloquait silencieusement toute la file ; le moteur actuel n'en programme jamais (toutes ses échéances sont au moins au tick suivant), et le lot 7 devra appliquer la règle de report ci-dessus avant d'appeler la file.

## Consommation des budgets

Les budgets se consomment **avant** l'opération qu'ils autorisent, jamais après :

1. **Programmation** : avant d'ajouter un événement à la file, le moteur vérifie `maxPendingEvents` et décompte une unité de travail ; si l'une des limites serait dépassée, l'événement n'est pas ajouté et le combat s'arrête (`queueLimitExceeded` ou `workBudgetExceeded`).
2. **Exécution** : avant de traiter un événement retiré de la file (y compris périmé ou supprimé), le moteur décompte une unité de travail ; avant chaque évaluation de déclencheur et chaque application d'effet, une unité de plus.
3. **Écriture** : avant d'écrire une ligne de simulation, le moteur incrémente le compteur d'événements du tick (comportement **en place** : la ligne qui dépasserait le budget n'est pas écrite).

Un dépassement ne laisse donc jamais d'effet à moitié appliqué : l'opération refusée n'a rien modifié, et seules les lignes d'arrêt et de finalisation (enveloppe séparée) suivent.

## Ce qui reste hors des correctifs 6.1 et 6.1.1

- Aucun moteur général d'états, aucun déclencheur, aucun budget de travail, aucun report, aucune provenance : tout ce qui précède, hors mentions « en place », est un contrat à implémenter au lot 7. Aucune altération nouvelle : Fragilité et rupture de garde restent les deux états codés du lot 6, désormais avec instance et génération.
- Les nouveaux champs `combat.limits.*` ci-dessus ne sont pas encore lus ; seuls `maxDurationMs` et `maxEventsPerTick` existent.
- Décisions à prendre avant le lot 7 : A12 à A15 et A23 (voir le rapport du lot 6), et la validation des valeurs proposées ici.
