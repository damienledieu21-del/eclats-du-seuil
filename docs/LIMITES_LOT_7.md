# Contrat de limites du lot 7 (correctifs 6.1 et 6.1.1, sous-lots 7.1 à 7.3, correctifs 7.2 et 7.3.1)

Statut : **contrat en partie implémenté**. Le sous-lot 7.1 a mis en place l'admission des scénarios, les limites des lecteurs, les budgets de travail et de file et la mesure des pics ; ces éléments sont marqués « en place ». Le sous-lot 7.2 a ajouté les altérations génériques (DOT, contrôles, immunités, résistances), leurs coûts réservés et leur précontrôle numérique, sans déclencheur. Le sous-lot 7.3 a mis en place la provenance des effets (journal au format 4), les déclencheurs, la profondeur, la règle de cycle, le report et l'annulation des réactions, et le majorant de file non garanti avec déclencheurs ; ces éléments sont marqués « en place depuis l'étape 7.3.x ». Le correctif 7.3.1 (audit Astra, A73-M01 à M04) a corrigé le crédit de file, le précontrôle des DOT, l'écriture canonique des empreintes et l'analyse statique des cycles (section « Correctif 7.3.1 »). Les sections qui suivent fixent les bornes et les règles de terminaison du moteur général des états. Toutes les valeurs configurables sont **provisoires** et se trouvent dans `rules.json`, bloc `combat.limits` (`schemaVersion` 5 ; voir [DONNEES.md](DONNEES.md)).

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
| **Capacité mesurée** | Ce qui a réellement été simulé et observé ; seule base pour parler de charge. | voir « Capacités mesurées » plus bas (sous-lot 7.1) | une mesure, datée, figée par `Lot71CapacityTests` |

Aucune capacité au-delà des mesures de la section « Capacités mesurées » n'est revendiquée.

## Limites

Les cinq premières lignes sont **en place depuis le sous-lot 7.1** ; chaque valeur configurée est bornée par un plafond technique codé en dur (`CombatRules.Absolute*`), et une configuration hors plafond est refusée au chargement de `rules.json`, à son chemin JSON.

| Limite | Valeur initiale | Champ | Dépassement | Justification |
|---|---|---|---|---|
| Combattants par combat | 64 (plafond [2, 256]) | `combat.limits.maxCombatants` | refus à la validation, `LimitExceeded` sur `combatants` (**en place**) | Les scénarios prévus (lot 8 : 1 contre 1, petits groupes) en ont moins de 10. 64 laisse de la marge aux essais de zone tout en gardant les ciblages de zone (parcours de tous les ennemis à chaque impact) et le journal de petite taille. |
| Commandes par scénario | 20 000 (plafond [0, 10⁶]) | `combat.limits.maxCommands` | refus à la validation, `LimitExceeded` sur `commands` (**en place**) | Un scénario de 10 min (durée maximale actuelle, 12 000 ticks) avec une commande à chaque tick en a 12 000. Au-delà, le fichier est très probablement généré par erreur. |
| Commandes par acteur et par tick | 4 (plafond [1, 64]) | `combat.limits.maxCommandsPerActorPerTick` | refus à la validation, `LimitExceeded` sur `commands[i].atMs` de la première commande en excès, une erreur par couple (acteur, tick) (**en place**) | Un joueur peut au plus combiner garde, parade et une compétence au même tick ; 4 garde une marge. Empêche qu'un scénario sature la phase 2 d'un seul tick. |
| Événements en attente dans la file | 100 000 (plafond [1, 10⁷]) | `combat.limits.maxPendingEvents` | événements initiaux (commandes, premiers tours) au-delà : refus à la validation, `LimitExceeded` sur `commands`, avant tout journal ; en simulation : arrêt `queueLimitExceeded` (**en place**) | Aucune contrainte statique « file ≥ commandes + combattants » : la capacité est comparée aux événements initiaux réels du scénario. Le pire cas mesuré avec le contenu actuel atteint 57 615 (voir plus bas). |
| Budget de travail par tick | 10 000 unités (plafond [1, 10⁷]) | `combat.limits.maxWorkPerTick` | arrêt `workBudgetExceeded` (**en place**) | Voir ci-dessous : distinct du budget d'événements journalisés. 10 × le budget journalisé, car une unité de travail ne produit pas toujours de ligne. |
| Événements de simulation comptés par tick | 1 000 (au plus `maxWorkPerTick`) | `combat.limits.maxEventsPerTick` | arrêt `eventBudgetExceeded` (**en place**) | Valeur du plan (§4.4). Compte les événements émis par les phases 1 à 6 d'un tick, pas toutes les lignes du journal (voir ci-dessous). Chaque ligne coûte aussi une unité de travail, d'où la cohérence vérifiée au chargement. |
| Lignes d'initialisation | 1 + nombre de combattants (≤ 65) | dérivée de `maxCombatants` | sans objet : bornée par construction | En-tête + une ligne `combatant` par participant, au tick 0, avant tout événement compté. |
| Lignes d'arrêt et de finalisation | 2 + nombre de combattants (≤ 66) | dérivée de `maxCombatants` | sans objet : bornée par construction | Au plus une ligne `simulationAborted`, une `combatantSummary` par participant, une `combatEnded`. Jamais d'autre ligne après l'arrêt. |
| Profondeur de chaîne de déclencheurs | 4 | `combat.limits.maxTriggerDepth` | effet non créé, `triggerSuppressed`, le combat continue | Valeur du plan (§4.4, A15) : quatre réactions successives au plus depuis une racine (règles P1 à P8) ; « parade → gain de Charge → passif → effet » n'en utilise qu'une. |

### Limites des lecteurs (en place, sous-lot 7.1)

Constantes techniques de `InputLimits`, non configurables. Chacune est vérifiée **avant** le chargement complet de ce qu'elle protège.

| Limite | Valeur | Contrôle | Refus |
|---|---|---|---|
| Taille d'un fichier de données (règles, classe, scénario) | 16 Mio | longueur du fichier avant lecture, puis lecture bornée (un fichier qui grandit entre-temps est refusé de même) | `FileTooLarge` sur `$` ; un fichier creux de 1 Gio est refusé sans être lu |
| Taille d'un texte JSON fourni en mémoire | 16 Mio d'octets UTF-8 | longueur en caractères (refus immédiat au-delà), sinon compte des octets avant encodage | `FileTooLarge` sur `$` |
| Taille d'un journal rejoué | 256 Mio | longueur du fichier avant lecture, puis lecture bornée | rejeu `Invalid`, « journal … trop volumineux » ; la ligne d'en-tête est en outre bornée à 16 Mio |
| Profondeur JSON | 32 | analyseur (`MaxDepth`) | `InvalidJson`, avec la ligne |
| Tableaux `combatants` et `commands` d'un scénario (fichier ou en-tête de journal) | valeurs **configurées** `maxCombatants` et `maxCommands` | longueur du tableau lue avant tout élément | une seule erreur `LimitExceeded` sur le tableau, même message que l'admission ; aucun élément construit ni analysé |
| Erreurs conservées par rapport | 200 | `ValidationReport` | les suivantes sont comptées ; dernière ligne « … et N autres erreurs » |

L'index des lignes (qui associe chaque chemin JSON à sa ligne pour les diagnostics) n'est construit qu'à la première erreur localisée ; l'erreur d'un tableau trop long utilise une recherche ciblée qui s'arrête au nom du tableau. Le coût d'analyse reste donc proportionnel à un fichier d'au plus 16 Mio.

### Enveloppes séparées : simulation, initialisation, finalisation

Le budget `maxEventsPerTick` (en place) compte les **événements de simulation** émis pendant les phases d'un tick ; il ne compte ni l'en-tête et les lignes `combatant` écrites à la construction du combat, ni `simulationAborted`, `combatantSummary` et `combatEnded` écrites à l'arrêt ou à la fin. Ces lignes ont leur propre enveloppe, bornée explicitement par le nombre de combattants (tableau ci-dessus) : un journal complet compte au plus (1 + N) + durée en ticks × `maxEventsPerTick` + (2 + N) lignes. Le lot 7 ne doit ajouter aucune ligne à l'initialisation ou à la finalisation qui ne soit bornée par N ; un total à signaler en fin de combat (ex. événements reportés perdus, voir plus bas) devient un champ de `combatEnded`, jamais une ligne par élément.

### Budget de travail distinct du budget d'événements

Le budget `maxEventsPerTick` compte les **événements écrits pendant la simulation**. Or une partie du travail du moteur n'écrit rien : échéances périmées ignorées (M02), passages de combattants sans effet, et plus tard conditions de déclencheur évaluées à faux ou déclencheurs supprimés. Une boucle de travail silencieux ne serait jamais arrêtée par le budget d'événements. Le compteur de **travail** (**en place** depuis le sous-lot 7.1) est remis à zéro à chaque tick ; les deux budgets coexistent et le premier atteint arrête le combat avec sa raison. Le test `Travail_silencieux_consomme_le_budget_sans_ecrire_de_ligne` arrête un combat de 64 combattants inactifs au tick 0 (192 unités, aucune ligne).

Modèle de coût en place (une unité par opération) :

| Opération | Coût |
|---|---|
| Mise en file pendant un tick (prochain tour, recharge, fin de parade, échéance d'état) | 1 unité et 1 place de file |
| Retrait d'un élément de la file, y compris une échéance périmée ou une commande refusée | 1 |
| Impact sur une cible (N pour une action de zone) | 1 |
| Application ou retrait d'un état (expiration, mort) | 1 |
| Passage d'un combattant dans une phase balayée : effets périodiques, régénération, fin de tick | 1 par phase |
| Ligne de simulation écrite | 1 unité et 1 ligne comptée |

Ne sont pas décomptés : la construction du combat (en-tête, lignes `combatant`, mise en file initiale, bornée par l'admission) et les lignes d'arrêt et de finalisation (enveloppe séparée). Depuis le sous-lot 7.3 : chaque évaluation de déclencheur et chaque mise en file coûtent une unité de plus (section « Consommation des budgets »).

## Provenance et prévention des cycles

### Identité causale des racines

Une **racine** est un événement qui n'est produit par aucun déclencheur. Son identité est un n-uplet stable, calculé sans compteur global ni ordre d'insertion :

| Racine | Identité (`root`) |
|---|---|
| Commande du scénario | `command:<index dans le scénario>` |
| Tour d'un assaillant | `assailantTurn:<id>:<numéro du tour, 0, 1, 2…>` |
| Fin de recharge | `cooldown:<acteur>:<compétence>:<tick d'échéance>` |
| Effet périodique sans instance d'état (entretien de garde) | `periodic:<porteur>:<source>:<tick>` |
| Régénération | `regeneration:<porteur>:<tick>` |

`rootSeq` (numéro `seq` de la première ligne journalisée de la racine) est seulement une aide à la lecture du journal ; l'identité qui fait foi est `root`, indépendante de la présence ou non de lignes.

**En place depuis l'étape 7.3.2 (moteur 0.9.0, journal au format 4).** `root` est écrit sur l'effet racine (`cause: null`) ; chaque effet porte `eid` et `cause`. Précisions de mise en œuvre :
- `assailantTurn:<id>:<n>` : n compte tous les tours programmés, sauts compris (`attackSkipped`) ;
- `periodic:<porteur>:<source>:<tick>` : pour l'entretien de garde, la source est le porteur lui-même ;
- `cooldown:…` reste une racine (P1, P3), comme ce tableau l'indique. La ligne « recharge » du tableau de propagation du plan 7.3 (§3.2), qui la faisait hériter de la compétence, est écartée au profit de ce contrat ; une fin de fenêtre de parade hérite bien de la commande `parry` ;
- racine supplémentaire `endOfTick:<combattant>:<tick>` : mort constatée sans effet mortel (points de vie maximaux nuls dès le départ), seul cas où `died` n'hérite pas d'un impact ou d'un tick de DOT (A6).

Correctif 7.2 : l'échéance d'un état et le tick d'un DOT **ne sont plus des racines**. Ils héritent de la provenance de l'instance qui les a programmés (voir « Provenance causale des altérations » ci-dessous). Les lignes `stateExpiry` et « effet sur la durée » de la version précédente de ce tableau sont remplacées par cette règle.

### Identité d'un déclencheur

Un déclencheur est identifié par **(définition, porteur, instance)** :

- `definition` : identifiant stable du talent, de la compétence passive ou de l'état qui le définit ;
- `bearer` : combattant qui le porte ;
- `instance` : identifiant d'instance de l'état qui le porte (contrat des états du correctif 6.1), ou `0` pour un déclencheur permanent (talent, passif).

Deux porteurs de la même définition sont deux déclencheurs distincts. La **génération** n'entre pas dans l'identité utilisée pour les cycles (renouveler un état ne permet pas de se redéclencher dans la même chaîne) ; elle sert seulement à écarter les événements périmés.

### Chaîne causale et héritage entre branches sœurs

Chaque événement porte `root`, `depth`, `trigger` (identité ci-dessus, `null` pour une racine) et `chain`, l'ensemble des identités de déclencheurs traversés depuis la racine. `chain` est une **valeur immuable** : un événement produit par le déclencheur D reçoit `chain(parent) ∪ {D}`, construite pour lui seul. Si un événement provoque deux déclencheurs B et C (branches sœurs), la chaîne de C ne contient jamais B, et inversement : ce que fait une branche n'influence ni la profondeur ni la détection de cycle de l'autre. Les sœurs partagent seulement le budget de travail et le budget d'événements du tick.

### Calcul exact de la profondeur (règle normative, clarifiée après le contre-audit Astra)

La profondeur est une propriété de chaque **effet** (événement de simulation). Les règles P1 à P8 font foi ; toute autre section de ce document qui en parle s'y réfère.

- **P1. Racine.** Une racine (tableau « Identité causale des racines ») a `depth = 0`.
- **P2. Seule une réaction augmente la profondeur.** Une *réaction* est un déclencheur dont la condition est vraie pour un effet E et qui produit un **nouvel effet** F : `depth(F) = depth(E) + 1`. Rien d'autre n'augmente la profondeur.
- **P3. Conséquences directes.** Ce que la résolution d'un effet calcule elle-même, sans passer par un déclencheur, garde la profondeur de cet effet : impacts d'une même attaque (y compris d'une zone), état appliqué par l'attaque elle-même, ressource gagnée par la règle de base (Résonance, Charge de parade), mort et `stateRemoved` qui en découlent. (Une fin de recharge est une racine, P1 ; une fin d'état ou un tick de DOT hérite de la provenance de son instance, voir « Provenance causale des altérations ».)
- **P4. File et reports neutres.** L'insertion dans la file, l'ajout en fin de phase courante, le passage à une phase ultérieure du même tick et le report au tick suivant ne changent **pas** la profondeur (ni aucun autre champ de provenance).
- **P5. Profondeur maximale.** `maxTriggerDepth` vaut initialement **4** : un effet peut avoir une profondeur de 0 à 4.
- **P6. Suppression.** Si une réaction produirait un effet de profondeur 5 (plus généralement `> maxTriggerDepth`), cet effet n'est pas créé : rien n'est programmé, aucune ressource ni aucun état ne change. La suppression est journalisée par une ligne `triggerSuppressed` (`trigger`, `root`, `depth` = profondeur qu'aurait eue l'effet, `reason: "depth"`), et le combat continue. La condition du déclencheur est évaluée d'abord (une unité de travail) : on ne journalise une suppression que pour un effet qui se serait réellement produit.
- **P7. Branches sœurs.** Quand un même effet E provoque plusieurs réactions, chaque effet produit reçoit sa propre chaîne (`chain(E) ∪ {déclencheur}`) et sa propre profondeur `depth(E) + 1` ; une branche n'allonge jamais la chaîne ni la profondeur d'une autre.
- **P8. Diagnostics inertes.** `triggerSuppressed` et tout autre diagnostic (arrêt, budget, échéance périmée) ne sont pas des effets de jeu : ils n'ont pas de profondeur propre (le champ `depth` de `triggerSuppressed` décrit l'effet refusé), ne sont présentés à aucun abonné et ne peuvent déclencher aucun effet.

Exemple, avec `maxTriggerDepth = 4` :

| Effet | Produit par | Profondeur |
|---|---|---|
| Parade parfaite | commande (racine `command:3`) | 0 |
| Gain de Charge | conséquence directe de la parade (P3) | 0 |
| Effet du passif « à chaque Charge » | réaction 1 | 1 |
| Effet d'un talent qui réagit au passif | réaction 2 | 2 |
| … même effet reporté au tick suivant | report (P4) | 2 |
| Réaction 3, puis réaction 4 | réactions | 3, puis 4 |
| Réaction 5 | réaction (P6) | non créé : `triggerSuppressed`, `depth: 5` |

### Provenance causale des altérations (préparation du sous-lot 7.3, correctif 7.2 ; en place depuis l'étape 7.3.2)

Règles normatives pour les altérations, compatibles avec P1 à P8. **En place depuis l'étape 7.3.2** pour A1 à A7 et A9 : l'instance mémorise la provenance de sa dernière application acceptée (`cause` = son `stateApplied`, avec instance et génération), les ticks de DOT, l'expiration et l'état accordé la lisent au moment où ils agissent, et la mort hérite du premier effet qui a amené les points de vie à 0. A8 (reports) est en place depuis l'étape 7.3.5. La **provenance** d'un effet est le quadruplet (`root`, `depth`, `chain`, `trigger`) ; sa **source** est le combattant crédité (champ `source` du journal), qui n'entre pas dans la provenance mais est conservée avec elle.

- **A1. Application.** L'application d'une altération par une attaque ou une compétence est une conséquence directe de l'impact (P3) : même provenance que l'impact, source = auteur de l'impact. Appliquée par une réaction, elle a la provenance de cet effet (profondeur déjà augmentée par la réaction, P2). L'instance mémorise la provenance et la source de l'application qui l'a créée.
- **A2. Renouvellement.** Une application acceptée sur une instance existante (nouvelle génération) remplace la provenance et la source mémorisées par les siennes (comme le journal actuel : un gel renouvelé par « c » expire au nom de « c » ; pour un cumul par source, la source ne change pas). Une application refusée (`stateBlocked`) ne change rien à l'instance.
- **A3. Tick de DOT.** Un tick périodique est un effet dont la provenance est celle de l'instance au moment du tick (`root`, `depth`, `chain` et `trigger` hérités, **sans** augmentation de profondeur) ; source = source de l'instance, même morte. `dotDamage`, la mort qu'il provoque et les retraits qui en découlent partagent cette provenance (P3).
- **A4. Expiration.** `stateExpired` a la provenance de l'instance (génération courante) ; il ne crée pas de racine et n'augmente pas la profondeur. Une échéance périmée n'est pas un effet (« Échéances périmées »).
- **A5. État accordé.** Un état accordé à l'expiration (immunité après l'étourdissement) est une conséquence directe de cette expiration : même provenance, même source, même étape.
- **A6. Retrait.** `stateRemoved` pour décès a la provenance de l'effet mortel (impact, tick de DOT ou réaction) ; la rupture d'entretien garde la racine `periodic:<porteur>:<source>:<tick>` de l'entretien.
- **A7. Seule une réaction augmente la profondeur** (P2). Ni un tick périodique, ni une expiration, ni un état accordé, ni un renouvellement, ni un report n'augmentent `depth` ou n'étendent `chain`.
- **A8. Reports.** Un événement reporté (« Événement programmé dans une phase déjà passée ») conserve racine, source, chaîne, profondeur, déclencheur, instance et génération ; seul `deferredFromTick` s'ajoute (P4).
- **A9. Diagnostics inertes.** Les diagnostics de suppression (`triggerSuppressed`, échéances périmées, `deferredDropped`, arrêts et dépassements de budget) ne déclenchent jamais d'effet (P8). Proposition à confirmer en 7.3 : `stateBlocked` (immunité, résistance, `active`, `notLonger`, `parried`) est aussi inerte au premier palier, faute de cas d'usage arrêté.

Conséquence voulue : une boucle étalée dans le temps reste bornée par la provenance. Si un déclencheur R réagit à `dotDamage` en réappliquant le même DOT, le renouvellement porte `chain ∪ {R}` et `depth + 1` (A2) ; le tick suivant en hérite (A3), et R ne peut plus réagir à ce tick (règle de cycle : R est dans la chaîne). Sans réaction, un DOT appliqué à la profondeur d, renouvelé ou non, garde la profondeur d jusqu'à son expiration.

| Effet | Provenance | Profondeur |
|---|---|---|
| Impact de l'assaillant (racine `assailantTurn:a:3`) | racine | 0 |
| Brûlure appliquée par l'impact (instance 7) | conséquence directe (A1) | 0 |
| Ticks de la brûlure, puis expiration | hérités de l'instance 7 (A3, A4) | 0 |
| Étourdissement appliqué par un talent qui réagit à l'impact | réaction | 1 |
| Expiration de l'étourdissement, immunité accordée | héritée, conséquence directe (A4, A5) | 1, 1 |
| Brûlure renouvelée par une réaction à un tick de la brûlure | réaction (A2) | 1 ; ticks suivants 1 |

### Cohérence vérifiée (après le contre-audit Astra)

Les sections suivantes ont été relues contre P1 à P8 (et, au correctif 7.2, contre A1 à A9) : tableau des limites (profondeur 4, suppression sans arrêt) ; identité des racines (P1) ; chaîne et branches sœurs (P7) ; règles de cycle (la chaîne n'est étendue que par une réaction, comme la profondeur) ; diagnostics non déclencheurs (P8) ; échéances périmées (une échéance annulée ne produit aucun effet, donc aucune profondeur) ; événement programmé dans une phase passée (Q = P et Q avant P ne changent pas la profondeur, P4 ; la version précédente indiquait à tort `depth + 1` pour Q = P, corrigé) ; report depuis le dernier tick (effet non exécuté, profondeur sans objet) ; consommation des budgets (l'évaluation de la condition coûte une unité de travail avant la décision de suppression, P6).

### Règles

1. Profondeur : règles P1 à P8 ci-dessus.
2. Cycle : un déclencheur ne réagit jamais à un événement dont la `chain` contient déjà son identité (généralise `originTriggerId` du plan : empêche A → B → A avant même la limite de profondeur) ; `triggerSuppressed` avec `reason: "cycle"`.
3. Un événement issu d'une instance d'état retirée ou d'une génération périmée n'est jamais exécuté (voir « Échéances périmées »).
4. Contrôle statique au chargement des données : graphe « déclencheur → événements qu'il peut produire ». Proposition par défaut (A15, à trancher) : un cycle est un **avertissement** dans le rapport de validation, la limite de profondeur garantissant la terminaison.
5. Les gains de ressources par déclencheur restent plafonnés (Charges 5, Résonance 100), ce qui borne aussi les boucles gain/consommation.

### Diagnostics non déclencheurs

`triggerSuppressed`, `simulationAborted`, les dépassements de budget et toute ligne de diagnostic future ne sont **jamais** observables par un déclencheur : ils n'appartiennent à aucun type d'événement auquel on peut s'abonner, ne sont pas présentés aux abonnés et ne consomment pas d'évaluation de déclencheur (ils consomment une unité de travail, comme toute écriture). Une suppression ne peut donc pas en provoquer une autre.

### Échéances périmées

Tout événement programmé pour le compte d'un état porte `sourceInstance` et `sourceGeneration`. À son tour d'exécution, il est **annulé** si l'instance n'existe plus ou si sa génération a changé (renouvellement, retrait, mort du porteur) : il est retiré de la file, compte une unité de travail, n'est pas journalisé et ne déclenche rien. C'est la règle des fins d'état du correctif 6.1 (M02), étendue à tous les effets d'état (périodiques, retardés). **En place pour les altérations depuis le sous-lot 7.2** : échéances (instance, génération) et ticks de DOT (instance), périmés sans ligne, une unité de travail chacun. Le retrait d'une instance ne parcourt pas la file : l'annulation se fait paresseusement à l'échéance, ce qui garde un coût borné et un ordre déterministe.

### Ordre stable des abonnés

Pour un même événement, les déclencheurs abonnés sont évalués dans un ordre total indépendant de l'ordre d'inscription et de toute structure non ordonnée : initiative du porteur décroissante, puis identifiant du porteur (ordinal), puis identifiant de définition (ordinal), puis identifiant d'instance croissant. Les événements qu'ils produisent sont ajoutés à la file dans cet ordre (numéros `seq` croissants). Parcourir un dictionnaire ou un ensemble de hachage pour trouver les abonnés est interdit.

## Événement programmé dans une phase déjà passée

Ordre d'un tick : expirations → commandes → résolution → périodiques → régénération → fin de tick. Un effet résolu en phase P du tick T peut vouloir programmer un événement en (T, Q).

- **Q après P** (même tick) : exécuté normalement dans la phase Q du tick T.
- **Q = P** : ajouté à la fin de la phase en cours (ordre d'insertion), dans le même tick, **sans changer sa profondeur** (P4 ; la profondeur a déjà été fixée par la réaction qui l'a produit, P2) et compté dans le budget de travail. C'est déjà le comportement de la file pour la phase en cours.
- **Q avant P** : la phase est passée. Règle proposée : l'événement est reporté au tick T + 1, même phase Q, avec un champ `deferredFromTick: T` au journal. Jamais exécuté « en arrière » dans le tick courant.
- **Provenance conservée** : un événement reporté garde exactement `root`, `depth`, `trigger`, `chain`, `sourceInstance` et `sourceGeneration`. Le report n'augmente pas la profondeur et ne crée pas de nouvelle racine (P4) ; seul `deferredFromTick` s'ajoute. À l'exécution, au tick T + 1, on vérifie de nouveau l'instance et la génération ; la profondeur, inchangée, a déjà été admise.
- **Report depuis le dernier tick** : si T est le dernier tick du combat (T + 1 ≥ durée en ticks), ou si le combat se termine pendant T (équipe vaincue, arrêt), l'événement n'est pas exécuté. Il n'écrit pas de ligne par événement ; leur nombre est donné par un champ `deferredDropped` de `combatEnded` (enveloppe de finalisation bornée). Un combat ne se prolonge jamais pour vider la file.

**En place depuis l'étape 7.3.5** pour les réactions des déclencheurs : une réaction acceptée dont la phase d'exécution (résolution si elle porte une frappe, sinon la phase courante) est déjà passée est reportée en (T + 1, même phase) ; toutes les lignes qu'elle écrit portent `deferredFromTick: T`, et sa provenance (racine, profondeur, chaîne, cause, déclencheur) est celle fixée à l'acceptation. Les réactions reportées encore en file à la fin du combat (dernier tick, équipe vaincue, arrêt) sont comptées dans `combatEnded.deferredDropped`, sans ligne par réaction ; ce champ vaut 0 sans déclencheur.

### Annulation des réactions à l'exécution (Q5, en place depuis l'étape 7.3.5)

Une réaction acceptée n'est pas exécutée, et écrit à la place un diagnostic `reactionCancelled` (non observable, sans `eid`, citant `trigger`, `root`, `cause`, `depth`, `reason` et, s'il y a lieu, `deferredFromTick`), dans les cas suivants, vérifiés dans cet ordre au moment de l'exécution :

| Raison | Condition | Politique |
|---|---|---|
| `bearerDead` | le porteur est mort entre l'acceptation et l'exécution | une réaction est un acte de son porteur ; morte avec lui |
| `originGone` | la réaction est portée par une altération dont l'instance n'existe plus (expirée, retirée, retrait à la mort) | la réaction dépend de l'état actif qui la porte |
| `noTarget` | après calcul des cibles, le premier effet n'a aucune cible vivante | aucune ligne partielle ; les effets suivants ne sont pas tentés |

Politique aux trois moments sensibles :
- **Expiration** : seule une réaction **portée** par l'altération expirée est annulée (`originGone`). Une réaction qui **observe** un effet d'altération (tick de DOT, application) est autonome : l'expiration ou le retrait de l'altération observée ne l'annule pas. La raison `staleSource` envisagée par le plan n'est **pas** retenue : un effet déjà commis ne se défait pas parce que son origine a disparu.
- **Renouvellement** : il conserve l'instance (seule la génération change) ; une réaction portée par l'altération renouvelée survit et s'exécute.
- **Mort** : les réactions du mort en attente sont annulées (`bearerDead`) ; celles des autres combattants qui observent la mort (`onDeath`) s'exécutent normalement si elles ont une cible.

Ces annulations sont du gameplay (une réaction n'a plus de sens) et ne coûtent qu'une ligne réservée ; elles se distinguent des arrêts techniques (`simulationAborted`, budgets), qui interrompent le combat sans exécuter partiellement une réaction (réservation de la borne complète avant toute mutation, étape 7.3.4). L'étourdissement du porteur n'empêche pas ses réactions (Q12).

**En place depuis le correctif 6.1** : `EventQueue` refuse (exception de programmation) tout événement programmé avant la position déjà atteinte. Avant, un tel événement restait en tête du tas et bloquait silencieusement toute la file ; le moteur actuel n'en programme jamais (toutes ses échéances sont au moins au tick suivant), et le lot 7 devra appliquer la règle de report ci-dessus avant d'appeler la file.

## Consommation des budgets

**En place depuis le sous-lot 7.1 : réservation avant mutation.** Décompter chaque opération juste avant de l'exécuter ne suffit pas à rendre atomique une action composée (un impact qui brise la garde, inflige des dégâts et applique la Fragilité) : le budget pourrait manquer au milieu. Le moteur découpe donc le tick en **traitements** et, pour chacun, calcule le coût exact (travail, lignes, places de file) à partir de l'état courant, avec les mêmes conditions que l'exécution, puis le réserve **avant toute mutation** :

| Traitement | Réservé avant |
|---|---|
| Une échéance (recharge, fin de parade, état) | son retrait de la file (lecture de la tête sans la retirer) |
| Le retrait d'une commande ou d'un tour d'assaillant | ce retrait |
| Une commande ou un tour d'assaillant | ses contrôles et ses effets (refus compris) |
| Une action en résolution, **toutes cibles comprises** | le premier impact |
| Le passage d'un combattant dans une phase balayée (entretien de garde et rupture éventuelle, régénération, mort et retrait de ses états) | ce passage |
| Un tick de DOT (sous-lot 7.2) : dégâts et ligne, puis tick suivant en file, ou fin de l'instance et état accordé | son retrait de la file |

Correctif 7.3.1 (A73-M04) : le contrôle de file porte sur l'**occupation nette**, pas sur le nombre d'insertions. Un événement programmé (échéance, commande ou tour retiré, tick de DOT, notification, réaction) est réservé puis retiré par un seul point du moteur, `ReserveAndDequeue` ; son retrait, certain dès que la réserve passe, est crédité : le contrôle est `file − 1 + insertions ≤ maxPendingEvents`. Les insertions restent comptées telles quelles pour le travail et pour la vérification exacte des réserves.

Sous-lot 7.2 (décision 14, atomicité par étape) : une altération appliquée par un impact fait partie de la réserve de l'action (rupture, dégâts, puis chaque altération de `applies` avec ses interruptions, calculées sur l'état laissé par les étapes précédentes) ; un état accordé à l'expiration fait partie de la réserve de cette expiration ; une application refusée (`stateBlocked`) coûte sa ligne. Testé par `Impact_compose_degats_rupture_dot_et_etourdissement_refuse_en_entier` et `Dernier_tick_de_dot_atomique_aux_frontieres`.

Si la réserve dépasse un budget, le combat s'arrête avant le traitement, qui n'a donc rien modifié : ni ressource dépensée, ni état appliqué, ni élément en file, ni ligne écrite. Ordre de vérification fixe : **travail, puis file, puis lignes** ; l'arrêt porte la raison du premier qui échoue. Pendant le traitement, toute consommation au-delà de la réserve lève une erreur de programmation ; en Debug, une réserve non entièrement consommée en lève une aussi, ce qui vérifie l'exactitude des coûts sur toute la suite de tests (Debug et Release tournent en CI). Les coûts réservés étant exacts, un tick qui coûte exactement `maxWorkPerTick` passe et un tick qui coûte une unité de plus s'arrête (`Budget_de_travail_aux_frontieres`).

**Limites d'atomicité restantes** (documentées, non corrigées en 7.1) :
- L'atomicité vaut **par traitement**, pas par commande de bout en bout : une compétence acceptée en phase 2 (coûts payés, recharge programmée) dont la résolution en phase 3 est refusée garde ses coûts payés, sans impact. C'est le découpage en phases existant (A10), testé par `Action_de_zone_refusee_en_entier_avant_le_premier_impact`.
- Le débordement numérique (`overflow`, filet des opérations `checked`) n'est pas couvert par la réservation : il peut survenir au milieu d'un traitement. Le précontrôle numérique de `CombatSetup.Validate` (correctif 6.1.1, DOT par leur formule périodique depuis le correctif 7.3.1) le rend inatteignable pour les configurations qu'il recense ; seul un dépassement tardif hors de ce recensement l'atteint. Depuis le correctif 7.3.1, un impact ou un tick de DOT prépare PV, dégâts subis, dégâts infligés et coups dans des variables vérifiées avant toute affectation : un débordement à cette étape n'applique rien à moitié (mais une parade, un blocage ou une rupture déjà journalisés dans le même impact restent).
- La file n'est pas vidée à l'arrêt : les éléments restants sont abandonnés avec le combat ; aucun n'est exécuté.

## Contrôle prudent de saturation de la file (correctif 7.2)

**En place.** Les budgets restent à 1000 événements comptés par tick et 100 000 éléments en file. En plus de l'admission des événements initiaux, `CombatSetup.Validate` compare à `maxPendingEvents` un majorant prudent de la taille de la file, `CombatSetup.ComputeQueueBound()`, et refuse le scénario avant tout journal s'il le dépasse (`LimitExceeded` sur `combatants`, message « majorant prudent de la file … », code de sortie 1 de la CLI sans fichier journal). Le majorant est la somme de trois termes, chacun justifié par une propriété du moteur :

1. **Événements initiaux** (commandes, premiers tours avant la fin) : chaque commande retirée en programme au plus un (fin de recharge, fin de fenêtre de parade) et chaque tour d'assaillant programme le suivant ; ce nombre ne croît pas.
2. **Ticks de DOT** : un seul tick programmé par instance, une instance par (source, cible, altération) : pour chaque porteur et chaque DOT qu'il peut appliquer, le nombre de ses cibles possibles.
3. **Échéances d'altérations vivantes** : une échéance vit au plus la durée de base D de l'altération (la résistance ne fait que la raccourcir). En D ticks, un assaillant fait au plus ⌊ (D − 1) / intervalle ⌋ + 1 tours, répartis sur son cycle d'attaques (seules comptent celles qui portent l'altération), chacun sur une cible ou sur toutes les cibles ennemies (zone) ; un personnage applique au plus min(commandes de la compétence, ⌊ (D − 1) / max(GCD, recharge) ⌋ + 1) fois par cible possible. Selon le cumul : `ignore`, une échéance par cible ; `keepLonger`, au plus le nombre d'applications et au plus une par cible et par tick ; `refresh`, le nombre d'applications. États accordés à l'expiration : une échéance par cible en `ignore`, D par cible sinon. Rupture de garde en `ignore` : rien, car elle occupe la place d'une commande `guardStart` déjà retirée, qui ne programme rien.

Une altération jamais appliquée (aucune commande de la compétence, aucune attaque qui la porte) ne compte pas : les scénarios valides ne sont pas pénalisés par les compétences non utilisées. L'arrêt `queueLimitExceeded` en cours de combat reste un **filet de sécurité**, inatteignable pour un combat validé ; ses tests passent par un constructeur interne qui omet le majorant.

Mesures (pic réel de la file / majorant), figées par `Lot71CapacityTests` et `Correctif72QueueBoundTests` ; le pic n'a jamais dépassé le majorant :

| Scénario | File max | Majorant |
|---|---|---|
| `mannequin` | 18 | 18 |
| `entrainement_garde_parade` | 19 | 20 |
| `entrainement_rupture` | 7 | 7 |
| `entrainement_alterations` | 14 | 17 |
| Commandes au plafond (63 × 4 par tick) | 19 908 | 19 908 |
| 32 assaillants à cible unique, Fragilité | 3 872 | 3 904 |
| 15 assaillants de zone, Fragilité (arrêt `eventBudgetExceeded`) | 57 615 | 57 615 |
| 32 assaillants à cible unique, DOT puis contrôles | 1 376 | 1 440 |
| 32 assaillants à cible unique, 2 DOT + gel + étourdissement | 2 624 | 2 720 |
| 10 assaillants de zone, 2 DOT (arrêt `eventBudgetExceeded`) | 650 | 650 |

Le majorant est atteint ou presque dans tous les cas : il ne rejette pas de scénario valide sans raison. Il reste un majorant de pire cas : un scénario dont le pic réel tiendrait mais dont le majorant dépasse la capacité est refusé (limite connue, aucun cas avec le contenu livré).

### Avec déclencheurs : majorant non garanti (sous-lot 7.3, C12, Q11 ; en place depuis l'étape 7.3.6)

Dès qu'un déclencheur existe (talent, compétence équipée ou passif d'un personnage, assaillant de scénario, ou altération du catalogue qui en porte un), `ComputeQueueBound()` ajoute un **terme transitoire** et renvoie `Guaranteed = false` :
- terme transitoire = 2 × `maxWorkPerTick` : chaque mise en file coûte une unité de travail ; les notifications et réactions d'un tick sont toutes retirées avant la fin de leur phase, sauf les réactions reportées au tick suivant ; à tout instant la file en contient donc au plus celles mises en file pendant le tick courant et celles reportées du tick précédent ;
- les échéances et ticks de DOT créés par des **réactions** ne sont pas majorés (une réaction peut appliquer un état à chaque événement observable ; un tel majorant serait inutilisable).

Le contrôle d'admission reste appliqué au total (événements initiaux, DOT, échéances, terme transitoire) : un combat à déclencheurs dont ce total dépasse `maxPendingEvents` est refusé, et le message cite le terme transitoire. Un combat admis peut en revanche atteindre l'arrêt `queueLimitExceeded`, qui redevient un filet atteignable, réservé avant mutation : une réaction refusée n'a aucun effet (test `Lot73BudgetTests.Saturation_par_reactions_persistantes…`). `eds validate` (scénario) et `eds simulate` écrivent une ligne `file` : « majorant prudent N/capacité, garanti » sans déclencheur, « … dont T transitoires, NON garanti … » avec.

Budgets des déclencheurs : la notification réserve son coût exact (retrait, une évaluation par candidat vivant, une ligne par suppression, une mise en file par acceptation), la réaction réserve son majorant entier (retrait et pire coût de chaque effet sur chaque cible) avant la première mutation. Chacune de ces réservations est testée en N − 1, N et N + 1 pour les trois budgets (`Lot73BudgetTests`) : à N − 1 l'arrêt a lieu sur cette réservation, au même tick, sans aucune ligne de réaction, et le journal est identique jusque-là.

## Correctif 7.3.1 (audit Astra de la PR #2)

- **A73-M04, crédit de file** : voir « Consommation des budgets ». Le scénario minimal de l'audit (un DOT, un tour d'assaillant, capacité 2, majorant garanti 2) va au bout avec un pic de 2 ; à la capacité N − 1 il est refusé avant tout journal. Un combat garanti ne s'arrête jamais sur `queueLimitExceeded` (propriété `M04QueueCapacityTests`).
- **A73-M01, précontrôle des DOT** : le pire tick d'un DOT (appliqué par une compétence, une attaque ou une réaction) est calculé par la formule périodique du moteur : puissance figée (puissance du type × coefficient), défense de la cible seulement si l'altération y est sensible, sans bonus de catégorie ni critique. La formule directe n'en était pas un majorant (multiplicateur de catégorie maximal inférieur à 1, jusqu'à 0). Il est vérifié contre chaque défenseur ennemi réel et sans défense, puis entre dans le cumul : pire impact × durée en ticks × `maxEventsPerTick` (chaque impact écrit une ligne comptée, quel que soit le nombre de cibles ; les dégâts subis d'une cible et infligés d'une source sont donc bornés par ce produit). Le pire tick étant exact, le précontrôle est un peu moins strict qu'avant pour les DOT avec les règles livrées (la Fragilité n'est plus comptée à tort).
- **A73-M02, empreintes** : écriture canonique injective, contrat 2 (voir [DONNEES.md](DONNEES.md#empreinte)).
- **A73-M03, cycles statiques** : composantes fortement connexes (Tarjan itératif) du graphe biparti « déclencheur → type d'événement → déclencheur », linéaire ; mêmes avertissements, au plus un par type d'événement, chacun citant au plus 8 libellés. Plafonds techniques provisoires vérifiés avant tout autre contrôle : 256 talents, 256 compétences et 512 déclencheurs par classe ; 4 096 déclencheurs actifs par combat (personnages, assaillants, catalogue) ; le catalogue reste borné à 64 altérations × 8 déclencheurs. Mesures (Release, conteneur de développement) : graphe dense de 4 096 déclencheurs analysé en quelques dizaines de millisecondes (1 min 55 s avant) ; classes de l'audit (1 024 à 4 096 déclencheurs) refusées en moins d'une demi-seconde.

## Capacités mesurées (sous-lot 7.1, 16 octobre 2026)

Règles livrées (`data/v0`, contenu `0.3.0-provisoire`), moteur 0.7.0. Pics relevés par `CombatResult.Peaks` et figés par `Lot71CapacityTests` ; ils ne figurent pas dans le journal.

| Scénario | Fin | Travail max / tick | Lignes max / tick | File max |
|---|---|---|---|---|
| `mannequin` (1 personnage, 1 mannequin, 60 s) | durée atteinte | 14 | 4 | 18 |
| `entrainement_garde_parade` (1 contre 1 assaillant, 30 s) | durée atteinte | 20 | 7 | 19 |
| `entrainement_rupture` (1 contre 1 assaillant, 16 s) | durée atteinte | 20 | 7 | 7 |
| Plafond d'admission en commandes : 63 personnages, 4 commandes chacun par tick (frappe, parade, garde, fin de garde), 19 908 commandes | durée atteinte | 1 011 (10 %) | 378 (38 %) | 19 908 (20 %) |
| 32 personnages en garde contre 32 assaillants à cible unique avec Fragilité, une attaque par tick | équipe vaincue | 608 (6 %) | 192 (19 %) | 3 872 (4 %) |
| 32 personnages en garde contre 32 assaillants de zone, une attaque par tick | arrêt `eventBudgetExceeded` au tick 0 | 1 312 | 928 avant l'arrêt | 64 |
| Pire cas pour la file : 15 assaillants de zone avec Fragilité contre 32 personnages sans garde (vitalité 200) | arrêt `eventBudgetExceeded` au tick 139 (mort simultanée des 32) | 3 082 (31 %) | 999 | 57 615 (58 %) |

Lecture : avec le contenu actuel, **le budget de lignes est la première limite atteinte** par les charges extrêmes. Le budget de travail garde une marge d'au moins 3 fois et la file d'au moins 1,7 fois sur ces mesures. Borne théorique de la file : la Fragilité, seul état à échéance, écrit au moins deux lignes par application, donc au plus 500 applications par tick pendant les 120 ticks de sa durée, soit environ 60 000 échéances, plus au plus 20 000 commandes initiales et un tour par assaillant : environ 80 000, sous les 100 000 configurés. Ces marges sont à réévaluer quand le lot 7 ajoutera des états, des DOT et des déclencheurs.

## Capacités mesurées (sous-lot 7.2, 16 octobre 2026)

Règles livrées (contenu `0.4.0-provisoire`, schéma 4), moteur 0.8.0 ; mesures mises à jour au correctif 7.2 (contenu `0.4.1-provisoire`, moteur 0.8.1). Les sept mesures du sous-lot 7.1 sont inchangées (journaux de référence identiques hors en-tête et champs du format 3). Nouvelles mesures, figées par `Lot71CapacityTests` (32 personnages de vitalité 200, sans garde, assaillants de 1 dégât brut qui frappent à chaque tick) :

| Scénario | Fin | Travail max / tick | Lignes max / tick | File max |
|---|---|---|---|---|
| `entrainement_alterations` (1 contre 2 assaillants, 20 s : DOT, étourdissement, immunité, gel) | durée atteinte | 26 | 8 | 14 |
| 32 assaillants à cible unique : brûlure et saignement, puis étourdissement et gel, en alternance | durée atteinte | 928 (9 %) ; 768 avant le correctif | 256 (26 %) ; 192 avant | 1 376 (1 %) ; 1 824 avant |
| 32 assaillants à cible unique : brûlure, saignement, gel et étourdissement à chaque attaque | durée atteinte | 896 (9 %) ; 992 avant | 256 (26 %) | 2 624 (3 %) ; 3 520 avant |
| 10 assaillants de zone : brûlure et saignement | arrêt `eventBudgetExceeded` au tick 40 | 2 716 | 1 000 | 650 |
| 5 assaillants de zone : brûlure et saignement | arrêt `eventBudgetExceeded` au tick 400 (17 morts simultanées) | 2 708 | 992 | 325 |

Lecture :

- **Le budget de lignes reste la première limite atteinte.** Les DOT appliqués au même tick gardent la même cadence (décision 11) : leurs ticks tombent tous ensemble toutes les 2 s. Dix assaillants de zone écrivent 970 lignes par tick d'application ; au tick 40, les 640 instances s'ajoutent d'un coup. La charge des DOT n'est donc pas lissée.
- Un DOT n'occupe qu'une place de file par instance (un seul tick programmé à la fois ; un renouvellement n'en ajoute pas) : au plus 64 cibles × 63 sources × 2 DOT livrés = 8 064 places.
- Avant le correctif 7.2, un étourdissement renouvelé à chaque tick (fin plus tardive d'un tick) ne finissait jamais : aucune immunité n'était accordée et chaque renouvellement remettait une échéance en file (3 520). Depuis le correctif, l'étourdissement actif refuse les nouvelles applications (`active`, une ligne sans échéance) et expire au bout de 29 ticks, suivi de son immunité : plus de lignes par tick, moins d'éléments en file.
- Borne prudente de la file avec le catalogue livré : une application qui met une échéance en file écrit sa ligne, et chaque impact en écrit une de plus ; seuls la Fragilité (chaque application), le gel et l'étourdissement (au plus un renouvellement par cible et par tick) en mettent. D'où au plus (1000 + 2 × 64) / 2 = 564 échéances par tick pendant au plus 120 ticks (Fragilité), soit environ 67 700, plus 8 064 DOT, 20 000 commandes et 64 tours : environ 96 000, sous les 100 000 configurés mais sans grande marge. Depuis le correctif 7.2, un catalogue ou un scénario qui pourrait dépasser la capacité est refusé avant le premier tick par le contrôle prudent de saturation (section précédente), scénario par scénario.

## Capacités mesurées avec déclencheurs (sous-lot 7.3, 16 octobre 2026)

Moteur 0.9.0, règles provisoires (budgets 10 000 / 1 000 / 100 000, profondeur 4), personnages de vitalité 200, ripostes de coefficient 0,1 à l'impact subi, sans entretien de garde. Figées par `Lot73CapacityTests` ; les mesures sans déclencheur (sections précédentes, `Lot71CapacityTests`) sont inchangées.

| Scénario | Fin | Travail max / tick | Lignes max / tick | File max | Réactions | Suppressions | Profondeur max |
|---|---|---|---|---|---|---|---|
| Arbre 2 contre 2, ripostes de zone, une commande | durée atteinte | 113 | 25 | 5 | 7 | 8 | 4 |
| Arbre 3 contre 3 | durée atteinte | 446 | 114 | 37 | 22 | 45 | 4 |
| Arbre 4 contre 4 | durée atteinte | 1 359 | 375 | 145 | 53 | 160 | 4 |
| Arbre 6 contre 6 | arrêt `eventBudgetExceeded` au tick 0 | 3 621 | 969 | 741 | 155 | 36 | 4 |
| Ping-pong entre deux porteurs (riposte ciblée) | durée atteinte | 30 | 6 | 2 | 2 | 1 (`cycle`, profondeur 3) | 2 |
| 64 porteurs (32 personnages, 32 assaillants frappant à chaque tick), ripostes ciblées, 30 s | durée atteinte | 928 (9 %) | 160 (16 %) | 64 | 38 400 | 19 200 | 2 |
| 64 porteurs, ripostes de zone | arrêt `eventBudgetExceeded` au tick 0 | 2 616 | 832 | 808 | 24 | 0 | 1 |

Lecture :
- **Le budget de lignes reste la première limite atteinte**, comme sans déclencheur. Une riposte de zone multiplie les lignes par le nombre d'ennemis à chaque niveau : dès 6 contre 6, l'arbre d'une seule commande dépasse 1 000 lignes dans le tick. L'arrêt est propre, au tick exact, sans réaction partielle.
- **Le cycle coupe tôt** : un porteur ne réagit pas deux fois dans une même chaîne. Entre deux porteurs seulement, le ping-pong s'arrête à la profondeur 3 par `cycle`, jamais par `depth` ; atteindre la profondeur 4 demande au moins trois porteurs distincts dans la chaîne (ici les arbres de zone). Le plan (§10.2, « chaîne 1-2-3-4 puis depth à 5 » pour deux porteurs de la même définition) est corrigé en ce sens : l'identité d'un déclencheur inclut son porteur, mais la chaîne de deux porteurs se referme sur le premier au troisième maillon.
- La file reste petite : notifications et réactions sont transitoires ; seules les altérations posées par des réactions s'accumulent (non majorées, voir la section précédente).

## Ce qui reste après le sous-lot 7.3

- Aucun déclencheur dans les données livrées : les déclencheurs de gameplay (talents et compétences du Bastion) arrivent au lot 8 ; aucun scénario d'entraînement à déclencheurs n'est livré (Q14).
- Équipement porteur de déclencheurs : réservé (origine `equipment`), non implémenté (Q9).
- Pas de chance de déclenchement ni de critique sur les frappes de réaction (Q7) ; pas de raison d'annulation `staleSource` (voir « Annulation des réactions »).
- Le majorant de file n'est pas une garantie avec déclencheurs (C12, Q11) : les altérations posées par des réactions ne sont pas majorées.
- Une exception arithmétique tardive reste théoriquement possible pendant une réaction malgré le précontrôle numérique : l'atomicité est garantie pour toutes les limites prévisibles (budgets, plafonds), pas en général. Depuis le correctif 7.3.1, les compteurs d'un impact ou d'un tick de DOT ne sont jamais modifiés à moitié.
- Coût CPU réel des parcours de candidats et des listes de filtres non mesuré au-delà des budgets logiques (risque R73-01 de l'audit, prévu au lot 8) ; mémoire des journaux complets en CLI et au rejeu non bornée globalement (R73-02).

## Historique : ce qui restait hors des sous-lots 7.1 et 7.2

- Aucun déclencheur, aucun report, aucune provenance, aucune profondeur : tout ce qui précède, hors mentions « en place », reste un contrat pour la suite du lot 7 (sous-lot 7.3). Les altérations génériques (sous-lot 7.2) sont en place ; aucune n'est observable par un déclencheur.
- Décisions à prendre : A15 (déclencheurs) ; points ouverts du sous-lot 7.2 (voir `RAPPORT_LOT_7_2.md`, arbitrés au correctif 7.2) et du correctif 7.2 (voir `RAPPORT_CORRECTIF_7_2.md`) ; validation définitive des plafonds techniques et des valeurs initiales, approuvés provisoirement pour 7.1 et 7.2.
