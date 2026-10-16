# Moteur de simulation (lots 5 et 6, correctif 6.1, sous-lots 7.1 et 7.2, correctif 7.2)

Moteur de combat déterministe, sans Unity, sans réseau et sans horloge système. Code : `src/EdS.Simulation/Engine`. Toutes les valeurs sont **provisoires**.

## Temps et file d'événements

- Horloge logique en ticks (`rules.json` → `combat.tickMs`, 50 ms). Les durées en ms sont converties par `TickRate` ; elles doivent être des multiples du tick (sinon le fichier est refusé).
- Un combat de `durationMs` dure `durationMs / tickMs` ticks, numérotés à partir de 0. Une commande à `atMs` s'exécute au tick `atMs / tickMs`.
- `EventQueue<T>` : tas binaire ordonné par (tick, phase, numéro d'insertion). Deux événements identiques en tick et en phase sortent toujours dans leur ordre d'insertion.

## Ordre intra-tick (A10)

| Phase | Contenu |
|---|---|
| `setup` | tick 0 seulement : une ligne `combatant` par combattant (statistiques calculées) |
| 1. `expirations` | fins de recharge (`cooldownReady`), fins de fenêtre de parade (`parryEnded`), fins d'altération (`stateExpired`) ; un état accordé à l'expiration (immunité après l'étourdissement) est appliqué dans la même étape |
| 2. `commands` | commandes du tick et tours des assaillants, triés par initiative décroissante, puis identifiant de l'acteur (ordinal), puis ordre dans le scénario (le tour d'un assaillant avant ses commandes) ; validation puis application (`skillCast`, `guardStarted`, `guardStopped`, `parryStarted`, `attackStarted` ou `commandRejected`) |
| 3. `resolution` | impacts des actions lancées pendant ce tick, dans l'ordre de la phase 2 ; pour chaque cible : défense (parade, garde), dégâts, récompenses, puis altérations portées dans l'ordre de la liste `applies` |
| 4. `periodic` | ticks de DOT (`dotDamage`), dans l'ordre de programmation, le dernier expirant l'instance ; puis entretien de la garde (`guardUpkeep`, ou rupture `upkeep`) |
| 5. `regeneration` | régénération d'endurance (`enduranceRegenerated`), sauf pendant la garde ou sous une altération qui la bloque (rupture) |
| 6. `endOfTick` | morts (`died`, puis `stateRemoved` pour chaque état du mort), fin de combat, arrêt sur limite |

Conséquences testées (`TimingTests`, `OrderingTests`) :

- un temps de recharge qui se termine au tick T est disponible pour une commande du même tick T (l'expiration passe avant les commandes) ;
- toutes les commandes d'un tick sont validées avant toute résolution : un combattant tué au tick T peut encore agir au tick T ; il meurt en fin de tick ;
- l'ordre des lignes ne dépend que du scénario, jamais de l'ordre d'insertion en mémoire ni d'un dictionnaire.

### Actions simultanées et morts en fin de tick

- Toutes les commandes d'un tick sont validées et appliquées (phase 2) avant le premier impact (phase 3). Une garde ou une parade commandée au tick T protège donc contre un impact du même tick T, quelle que soit l'initiative de l'attaquant.
- Les impacts se résolvent dans l'ordre de la phase 2 (initiative, puis identifiant). Un état appliqué par un impact (Fragilité) vaut pour les impacts suivants du même tick, pas pour celui qui l'applique (`FragilityTests`). Un étourdissement appliqué au tick T n'annule pas une action déjà acceptée au tick T : elle se résout (D13).
- Les PV peuvent tomber à 0 pendant la phase 3 sans que le combattant meure : il reste ciblable et ses actions déjà lancées se résolvent. La mort est constatée en phase 6, dans l'ordre des identifiants ; la garde et la fenêtre de parade du mort s'arrêtent alors sans ligne supplémentaire, et ses états actifs sont retirés (`stateRemoved`, raison `death`, ordre ordinal des états) : leurs échéances deviennent périmées.
- Si toutes les équipes sauf une (ou toutes) sont mortes en phase 6, le combat se termine sur ce tick (`teamDefeated`, vainqueur ou `null`). Mannequins et assaillants ne meurent jamais.

## Commandes et refus atomiques

Une commande (`atMs`, `actor`, `skill`, `target` facultative) est d'abord entièrement validée ; l'état n'est modifié qu'ensuite. Une commande refusée ne consomme ni endurance, ni recharge, ni GCD, ni tirage aléatoire. Premier motif rencontré, dans cet ordre :

- compétence : `actorDead`, `actorCannotAct` (mannequin, assaillant), `stunned` (altération qui bloque l'action, sous-lot 7.2), `skillNotEquipped`, `skillNotImplemented` (compétence sans bloc `combat`), `guardActive` (pas de compétence pendant la garde), `globalCooldown`, `cooldown`, puis ciblage : `targetRequired`, `targetNotEnemy`, `targetDead` (cible unique) ou `targetNotAllowed` (zone), puis `insufficientEndurance`, `insufficientResonance` (ultime), `insufficientRampartCharges` ;
- `guardStart` : `actorDead`, `actorCannotAct`, `stunned` ou `guardBroken`, `guardAlreadyActive` ;
- `guardStop` : `actorDead`, `actorCannotAct`, `guardNotActive` ;
- `parry` : `actorDead`, `actorCannotAct`, `stunned` ou `guardBroken`, `parryActive` (fenêtre déjà ouverte), `parryRecovery`.

Le motif d'une altération bloquante (`blockReason` de sa définition) vient juste après les motifs de mort ; si plusieurs altérations actives bloquent l'action, la première dans l'ordre du catalogue l'emporte (étourdissement avant rupture). Chaque refus est journalisé (`commandRejected` avec `action`, `skill`, `target`, `command`, `reason`). Garde et parade n'ont ni coût d'activation ni GCD.

## Rythme (A6)

Compétences instantanées. GCD de `combat.globalCooldownMs` (1000 ms) déclenché par chaque lancer ; recharge individuelle démarrant au lancer. Lancer au tick T avec une recharge de D ms : disponible à T + D / tickMs. Pas d'animation ni de temps d'incantation.

## Ciblage (A7)

Logique uniquement : pas de position, de distance ni de collision. `enemy` = une cible ennemie vivante désignée ; `allEnemies` = tous les ennemis vivants, dans l'ordre ordinal de leurs identifiants (et non dans l'ordre du scénario, comme l'indiquait à tort la version précédente de ce document). L'énumération `SkillTargeting` est le point d'extension pour un ciblage spatial futur.

## Endurance (A3)

Coût payé au lancer, maximum respecté. Les coûts de garde (entretien, blocage) sont des dépenses comme les autres. Régénération de `combat.endurance.regenPerSecond` (10/s) après `regenDelayMs` (1000 ms) sans dépense. Elle est calculée sur le total depuis la dernière dépense, pour éviter toute dérive d'arrondi :

```
ticksRégén = t − dernièreDépense − délaiTicks + 1   (si > 0)
gagné(t)   = min(max − enduranceAprèsDépense, ⌊régénParSeconde × ticksRégén × tickMs / 1000⌋)
```

Avec 50 ms : dépense au tick 20, rien jusqu'au tick 39, +0,5 cumulé au tick 40 (arrondi à 0), premier point au tick 41, puis +1 tous les 2 ticks. Pleine endurance : le compteur s'arrête.

Suspension (A1) : aucune régénération pendant la garde ni pendant la rupture. À la fin de l'une ou de l'autre (tick E), la régénération reprend dès qu'une seconde complète s'est écoulée depuis la dernière dépense, mais les ticks suspendus ne rapportent rien : le compteur repart de max(dernière dépense + délai, E). Exemples testés : dépense au tick 0, garde du tick 0 au tick 100, premier point au tick 101 ; blocage au tick 20, arrêt au tick 25, premier point au tick 41 ; rupture du tick 20 au tick 80, premier point au tick 81.

## Garde (A1)

Posture tenue de `guardStart` à `guardStop`. Pendant la garde :

- entretien de `combat.guard.upkeepPerSecond` (2/s), cumulé sur **tout le combat** (correctif 6.1, M01) : dû = ⌊ 2 × ticks gardés depuis le début du combat × 50 / 1000 ⌋, payé au fil des ticks, soit 1 point tous les 10 ticks gardés. Un tick est gardé si la garde est active en phase 4 : une garde prise puis quittée dans le même tick ne compte pas ; prise, quittée et reprise, elle compte une fois. Le coût ne dépend donc que du nombre de ticks réellement gardés, quelle que soit leur répartition (avant le correctif, le reliquat était perdu à chaque reprise : des gardes de 9 ticks ne coûtaient rien). S'il ne peut être payé, la garde est brisée (`upkeep`) et ce tick n'est pas compté : le reliquat reste dû à la garde suivante ;
- chaque impact direct bloquable coûte `costPerBlock` (10) puis est réduit de `damageReduction` (60 %) après armure (`blocked`, puis `damage` avec `defense: "guard"`). Si l'endurance manque, la garde est brisée **avant** la réduction : l'impact passe en entier (`defense: "guardBroken"`) ;
- lancer une compétence est refusé (`guardActive`) : il faut d'abord `guardStop`.

Rupture de garde : altération désignée par `combat.guard.breakStatus` (`guardBreak`, 3000 ms), intervalle [tick de rupture, +60 ticks[. Ni garde ni parade (`guardBroken`), pas de régénération, aucune hausse des dégâts reçus. Une fenêtre de parade ouverte est interrompue (`parryEnded` `interrupted`). Fin journalisée par `stateExpired` (`guardBreak`) ; la garde peut reprendre au tick même de la fin.

## Parade parfaite (A2)

`parry` au tick P ouvre la fenêtre semi-ouverte [P, P + 3[ (150 ms à 50 ms par tick). Un impact résolu aux ticks P, P + 1 ou P + 2 est paré ; au tick P + 3 la fenêtre est déjà fermée (phase 1). Testé pour les décalages −1 à 3.

- Impact paré : −90 % après armure (`defense: "parry"`). Le premier impact paré de la tentative rapporte +1 Charge et +8 Résonance (`parrySucceeded`) ; les impacts suivants de la même fenêtre sont aussi réduits, sans nouvelle récompense.
- Fin de fenêtre (`parryEnded`) : `success`, ou `failed` sans aucune Charge ; un échec impose une récupération de 500 ms comptée depuis la fin de la fenêtre, [P + 3, P + 13[ (`parryRecovery`).
- Pendant une garde : la parade est permise et prioritaire. Un impact dans la fenêtre est seulement paré (−90 %), sans coût de blocage ni Résonance de blocage ; la garde reste active et reprend pour les impacts hors fenêtre. Jamais de double réduction.

Attaque **imparable** (`unblockable`) : ignore la garde et la parade (`defense: "unblockable"`), aucun coût ni récompense ; une fenêtre traversée par un seul impact imparable se termine en échec.

Altérations portées par l'impact (correctif 7.2) : un impact **paré** (parade parfaite réussie, `defense: "parry"`) n'applique aucune de ses altérations ; chacune écrit `stateBlocked` avec `reason: "parried"` et la source de l'impact. Un impact bloqué par la garde normale et un impact imparable appliquent toutes leurs altérations.

## Ressources (A4, A5)

Propres aux personnages, à zéro au début de chaque combat, remises à zéro à la fin (après le résumé, qui donne la valeur finale). Tout gain passe par `resourceGained` (`resource`, `source`, `amount` réellement gagné, `wasted` perdu au plafond, `value`). Un gain ne déclenche jamais un autre gain.

- **Charges de rempart** : 0 à 5, +1 par parade parfaite réussie, aucun gain passif ni décroissance, excédent perdu. Sources et consommateurs configurables par compétence (`rampartChargeGain`, `rampartChargeCost` dans le bloc `combat` ; refus `insufficientRampartCharges`).
- **Résonance** : 0 à 100, départ à 0 ; +3 par attaque directe réussie, **une fois par action** quel que soit le nombre de cibles (`directHit`) ; +8 par parade parfaite ; +2 par blocage normal (par défenseur et par impact bloqué). L'ultime coûte 100 (`insufficientResonance`) et une action qui dépense de la Résonance n'en rapporte pas (pas de génération récursive).
- « Réussie » : avec un ciblage logique et sans esquive, toute attaque qui touche au moins une cible réussit, même parée ou bloquée.

## Altérations (A12 à A15, A19, A23 ; sous-lot 7.2)

Les altérations sont des **données** : bloc `statuses` de `rules.json` (voir [DONNEES.md](DONNEES.md#altérations-sous-lot-72)). Le moteur n'en connaît aucune par son nom, sauf l'altération de rupture désignée par `combat.guard.breakStatus`. L'ordre du catalogue est significatif : il fixe la priorité des motifs de refus. Valeurs livrées (provisoires) :

| Altération | Catégorie | Effet | Durée | Cumul | Résistance |
|---|---|---|---|---|---|
| `burn` | `dot` | dégâts magiques toutes les 2 s | 6 s | par source, plus forte puissance | résistance magique (chaque tick) |
| `bleed` | `dot` | dégâts physiques toutes les 2 s | 6 s | par source, plus forte puissance | armure (chaque tick) |
| `stun` | `control` | bloque compétences, garde, parade et attaques d'assaillant ; interrompt garde et parade ; accorde `stunImmunity` à l'expiration | 1,5 s | ignoré si actif (`active`) | durée |
| `stunImmunity` | `immunity` | immunité à `stun` | 3 s | ignorée si active | aucune |
| `freeze` | `control` | −30 % de vitesse de déplacement, enregistrée sans simulation du déplacement ; ne bloque rien | 4 s | garde la fin la plus tardive | durée |
| `fragility` | `modifier` | +10 % de dégâts directs reçus (catégorie « altération » de la formule) | 6 s | renouvelle | aucune |
| `guardBreak` | `control` | ni garde ni parade, pas de régénération | 3 s | ignorée si active | aucune |

Application, dans cet ordre (une issue refusée écrit `stateBlocked` avec `reason`) :

1. immunité active contre l'altération : `immune` ;
2. durée réduite à 0 tick par la résistance : `resisted` ;
3. aucune instance pour la clé (altération, et source pour un cumul par source) : nouvelle instance ;
4. sinon, selon le cumul : `refresh` et `perSourceKeepStronger` renouvellent ; `keepLonger` renouvelle seulement si la nouvelle fin est plus tardive (`notLonger` sinon) ; `ignore` refuse (`active`).

Un impact paré (parade parfaite) refuse toutes ses applications avant ces étapes (`parried`, correctif 7.2).

Étourdissement (correctif 7.2) : en cumul `ignore`, un étourdissement actif refuse toute nouvelle application (`stateBlocked`, `active`), quelle qu'en soit la source ; il n'est jamais prolongé. Son expiration accorde donc toujours `stunImmunity` (même source, même étape), sauf s'il est retiré par la mort de sa cible (`stateRemoved`, `death`), qui n'accorde rien. Un étourdissement tenté au tick même de l'expiration trouve l'immunité déjà active (phase 1) : `immune`.

Durée d'un contrôle à résistance `duration` (A14, A23) : r = K / (K + résistance aux altérations), K = `stats.statusResistanceConstant` (1000), calculé en virgule fixe arrondie vers le bas, puis ⌊ ticks de base × r ⌋. Les personnages tirent leur résistance de la Volonté (2 par point : 20 pour 10 de Volonté, r = 0,9803, étourdissement de 29 ticks) ; mannequins et assaillants ont `statusResistance` (0 par défaut). La résistance aux altérations ne réduit ni les dégâts des DOT ni la durée des altérations sans résistance (immunité, Fragilité, rupture).

DOT (A12, décisions 1, 2, 4, 11) :

- puissance figée à l'application : puissance du type de l'altération de la source (dégâts bruts pour un assaillant) × coefficient de l'application (`applies[].coefficient`, obligatoire pour un DOT et interdit sinon) ;
- dégâts de chaque tick : puissance réduite par la défense **actuelle** de la cible pour le type de l'altération, relue à chaque tick ; ni critique, ni Fragilité (dégâts directs seulement), ni garde, ni parade, ni Résonance, ni compteur de coups ; crédités à la source, même morte ;
- premier tick à application + période ; le dernier est le premier tick dont le suivant dépasserait la fin : il inflige ses dégâts puis expire l'instance dans la même étape (6 s toutes les 2 s : exactement 3 ticks) ;
- renouvellement par la même source : même instance, plus forte des deux puissances, **cadence d'origine conservée** ;
- fin effective (correctif 7.2) : à chaque application, la fin demandée (tick + durée) est alignée sur la cadence, prochain tick + ⌊ (fin demandée − prochain tick) / période ⌋ × période. C'est elle que journalise `untilTick`, avec `nextTick` (prochain tick de dégâts) ; elle est égale au tick du dernier tick de dégâts et de `stateExpired`. Exemple : brûlure au tick 20 (ticks 60, 100, 140), renouvelée au tick 50 : fin demandée 170, fin effective 140 ; renouvelée au tick 70 : fin demandée 190, prochain tick 100, fin effective 180. Une prolongation qui ne couvre pas un tick de plus ne change donc pas la fin ;
- renouvellement au tick même du dernier tick (résolution avant la phase périodique) : l'instance continue ; un tick plus tard, elle a expiré et l'application crée une nouvelle instance ;
- deux sources : deux instances indépendantes ; une instance expirée puis réappliquée est une nouvelle instance.

Interruptions : un contrôle qui interrompt la garde ou la parade les termine après sa ligne `stateApplied` (`guardStopped` avec `command: null`, `reason: "interrupted"`, `state` ; `parryEnded` `success` si la parade venait de réussir, `interrupted` sinon). Un assaillant sous une altération qui bloque ses attaques saute son tour (`attackSkipped` avec le motif) ; l'attaque suivante de sa liste et sa cadence ne changent pas.

Journal (format 3) : `stateApplied` (`combatant`, `state`, `category`, `source`, `instance`, `generation`, `untilTick` exclu — pour un DOT, la fin effective —, `renewed`, `power` et `nextTick` pour un DOT, `movementSpeedModifier` s'il est non nul), `stateExpired` et `stateRemoved` (mêmes six premiers champs ; `reason` pour le retrait), `stateBlocked` (`combatant`, `state`, `category`, `source`, `reason` : `immune`, `resisted`, `active`, `notLonger`, `parried`), `dotDamage` (`source`, `target`, `state`, `instance`, `amount`, `overkill`, `health`, `damageTaken`). La source de la rupture d'entretien est `null`.

### Contrat des états temporaires (correctif 6.1, M02 ; étendu au sous-lot 7.2)

- **Instance** : chaque application d'un état absent crée une instance au numéro unique dans le combat (1, 2, 3… dans l'ordre d'application, tous combattants confondus), jamais réutilisé.
- **Génération** : un renouvellement garde l'instance, repousse la fin et incrémente la génération (`renewed: true`).
- **Échéance** : chaque application d'un état non périodique programme une expiration portant le couple (instance, génération). À l'échéance, l'expiration n'agit que si ce couple est encore celui de l'état actif ; sinon elle est périmée et ignorée sans aucune ligne ni transition. Un DOT n'a pas d'échéance séparée : un seul tick programmé à la fois, valide tant que son instance est active.
- **Même tick** : l'expiration (phase 1) passe avant une réapplication du même tick (phase 3), qui crée alors une nouvelle instance (`renewed: false`).
- **Retrait** : la mort retire les états (`stateRemoved`, `death`, ordre ordinal de l'altération puis de la source) ; leurs échéances et ticks deviennent périmés ; aucun état n'est accordé sur un retrait.
- **Pour la suite du lot 7 (non implémenté)** : un état remplacé ou dissipé sera retiré par `stateRemoved` avec une autre raison (`replaced`, `dispelled`).

## Dégâts

Formule du lot 2 (`DamageCalculator`) : puissance du type de la compétence × coefficient, bonus (somme des `damageTakenIncrease` des altérations actives de la cible, chaque définition comptée une fois : Fragilité +10 %), critique (un tirage par coup, sur le flux PCG32 de l'attaquant), défense de la cible, puis réduction de garde ou de parade (lot 6), arrondi final. Exemple : 100 bruts sans armure → garde 40, parade 10 ; sous Fragilité 110, 44 et 11. Ticks de DOT : `DamageCalculator.ComputePeriodic` (puissance figée, défense actuelle, arrondi final, minimum de dégâts). Chaque combattant a son propre flux, dérivé de la graine du scénario et du hachage FNV-1a 64 de son identifiant : ajouter un combattant ne change pas les tirages des autres.

## Mannequin

PV infinis (`health: null`), armure, résistance magique et résistance aux altérations configurables, ne riposte jamais (toute commande à son nom est refusée `actorCannotAct`). Les dégâts reçus sont comptés dans son résumé.

## Assaillant d'entraînement (A21)

PV infinis, défenses configurables, n'obéit à aucune commande. Il attaque à `firstAttackMs` puis toutes les `intervalMs` (référence : 2000), en jouant ses `attacks` dans l'ordre puis en boucle. Chaque attaque a des dégâts **bruts** (avant armure ; référence 100), un type de dégâts, un ciblage logique (`enemy` : sa `target` ; `allEnemies` : tous les ennemis vivants par identifiant), `unblockable` et `applies` (altérations portées, avec un coefficient pour un DOT ; remplace `appliesFragility` au sous-lot 7.2). Jamais de critique, donc aucun tirage aléatoire. Journal : `attackStarted` (avec `applies`), ou `attackSkipped` si sa cible est morte ou s'il est sous une altération qui bloque ses attaques, puis les impacts.

## Limites

- `combat.limits.maxDurationMs` (600 000) : durée maximale d'un scénario ; plafond absolu du format 86 400 000.
- Admission (sous-lot 7.1), par `CombatSetup.Validate`, avant tout journal : au plus `combat.limits.maxCombatants` combattants (64), `maxCommands` commandes (20 000), `maxCommandsPerActorPerTick` commandes d'un même acteur au même tick (4) ; les événements initiaux (commandes et premiers tours d'assaillant avant la fin) doivent tenir dans `maxPendingEvents` (100 000). Code `LimitExceeded`.
- Contrôle prudent de saturation (correctif 7.2), par `CombatSetup.Validate`, après l'admission : le majorant `CombatSetup.ComputeQueueBound()` (événements initiaux + ticks de DOT programmés + échéances d'altérations vivantes) doit tenir dans `maxPendingEvents` ; sinon refus `LimitExceeded` sur `combatants`, avant tout journal. Le majorant tient compte de la cadence réelle des assaillants, des commandes et recharges des personnages, et du cumul de chaque altération ; une altération jamais appliquée ne compte pas. L'arrêt `queueLimitExceeded` reste un filet de sécurité, inatteignable pour un combat validé. Détail et mesures : [LIMITES_LOT_7.md](LIMITES_LOT_7.md#contrôle-prudent-de-saturation-de-la-file-correctif-72).
- Budgets d'exécution (sous-lot 7.1) : `combat.limits.maxWorkPerTick` (10 000 unités de travail par tick) et `maxPendingEvents` (taille de la file). Chaque traitement (échéance, retrait, commande, action entière avec toutes ses cibles, passage d'un combattant dans une phase balayée) réserve son coût exact **avant toute mutation** ; si un budget serait dépassé, arrêt propre `simulationAborted` (`workBudgetExceeded` ou `queueLimitExceeded`, champ `limit`) sans effet partiel du traitement refusé. Ordre de vérification : travail, file, lignes. Modèle de coût et limites d'atomicité restantes : [LIMITES_LOT_7.md](LIMITES_LOT_7.md#consommation-des-budgets).
- Pics de charge (sous-lot 7.1) : `CombatResult.Peaks` (travail et lignes maximaux en un tick, taille maximale de la file) et la ligne `charge` du résumé de `eds simulate`. Ils ne sont pas écrits dans le journal (format inchangé). `CombatEngine.LastTickWork` et `LastTickEvents` donnent le coût du dernier tick simulé, pour le diagnostic.
- `combat.limits.maxEventsPerTick` (1000, au plus `maxWorkPerTick`) : budget des **événements de simulation comptés** dans un tick (phases 1 à 6) ; au-delà, arrêt propre `simulationAborted` (`eventBudgetExceeded`). Ce n'est pas un plafond de lignes du journal : l'en-tête et les lignes `combatant` (initialisation), `simulationAborted` (arrêt), `combatantSummary` et `combatEnded` (finalisation) ne sont pas comptés. Elles restent bornées par le nombre de combattants (1 + N à l'initialisation, au plus 2 + N à la fin) ; leur enveloppe propre est fixée dans le contrat du lot 7.
- Précontrôle numérique **avant le premier tick** par `CombatSetup.Validate` (correctif 6.1, M04 ; complété au correctif 6.1.1, CA-M01), pour tous les types de combattants :
  - bornes des valeurs saisies : dégâts bruts d'un assaillant dans [0, 10⁹], armure et résistance magique des mannequins et assaillants dans [0, 10⁹], attributs d'un personnage dans [0, 10⁶], coefficient de compétence dans [0, 100], coûts et gains dans [0, 10⁶] ;
  - bornes des règles : constantes de défense dans ]0, 10⁹] ; toutes les durées (dont `globalCooldownMs` et `endurance.regenDelayMs` depuis 6.1.1, et les recharges de compétences) dans [0, 86 400 000] ms ;
  - valeurs **calculées** avec le même code que le moteur (`CombatantStats`) : statistiques de chaque combattant ; défense effective réelle de chaque participant, **personnages compris**, pour les deux types de dégâts, au plus 10⁹, et dénominateur constante × 10 000 + défense brute représentable ; pire impact de chaque attaque contre chaque défenseur ennemi réel ; pire impact sans défense (majorant de tous les autres) cumulé sur toute la durée au rythme maximal d'événements comptés. Sous-lot 7.2 : chaque DOT porté entre dans le pire impact comme une attaque sans critique de puissance × coefficient ; le bonus de dégâts reçus est majoré par la somme du catalogue ; la résistance aux altérations est bornée à 10⁹ et son dénominateur K × 10 000 + résistance doit être représentable.
  - Tout dépassement est refusé avec un diagnostic localisé (code `OutOfRange` pour les valeurs calculées, rattaché au combattant) ; rien n'est journalisé, et la CLI ne crée pas le fichier journal.
- Ce que le précontrôle garantit : les calculs **connus et recensés** du moteur à la date du correctif 6.1.1 restent représentables pour un combat validé. Il ne garantit pas qu'aucun débordement n'est possible : tout calcul ajouté plus tard (lot 7 et suivants) doit être recensé et borné à son tour. Tout dépassement arithmétique non anticipé pendant la simulation (`checked`, virgule fixe) arrête le combat proprement (`simulationAborted`, `overflow`) au lieu de planter ; un test vérifie ce filet de sécurité séparément.
- Échéances (cas C et D de CA-M01) : avec un tick de 1 ms, dernier tick possible (< 86 400 000) + délai en ticks (≤ 86 400 000) < 2 × 10⁸ ; produits de la régénération et de l'entretien de garde : (régénération ou coût par seconde ≤ 2³¹) × (ticks × durée du tick ≤ 86 400 000 ms) < 2 × 10¹⁷, loin de 2⁶³.
- Un combat interrompu (`aborted`) n'est pas un résultat d'équilibrage : `eds simulate` renvoie le code de sortie 2 et affiche un bandeau `DIAGNOSTIC` au-dessus des statistiques partielles.
- Natures des valeurs : les plafonds ci-dessus sont des **limites techniques** provisoires de pré-alpha, pas des paramètres d'équilibrage (qui sont dans `rules.json`) ni des **capacités mesurées** (voir [LIMITES_LOT_7.md](LIMITES_LOT_7.md#capacités-mesurées-sous-lot-71-16-octobre-2026)). Détail : [LIMITES_LOT_7.md](LIMITES_LOT_7.md#trois-natures-de-valeurs).
- Contrat des limites du lot 7 : [LIMITES_LOT_7.md](LIMITES_LOT_7.md).

Fin de combat : `durationReached`, `teamDefeated` (une seule équipe vivante, ou aucune) ou `aborted`. Chaque fin écrit un `combatantSummary` par combattant puis `combatEnded`.

## Journal JSON Lines

Une ligne JSON par événement, UTF-8 sans BOM, séparateur `\n`, champs dans un ordre fixe, nombres en virgule fixe écrits exactement (`1.5`, jamais `1.5000001`). Écrit par `JsonLineWriter` (cœur, sans bibliothèque).

Première ligne, l'en-tête :

```json
{"type":"header","format":"eds-combat-log","formatVersion":4,"engineVersion":"0.9.0","tickMs":50,
 "data":{"rules":{"contentVersion":"…","fingerprint":"sha256:…"},"classes":[{"id":"class.bastion","contentVersion":"…","fingerprint":"sha256:…"}]},
 "scenario":{…scénario complet…}}
```

Puis les événements : `{"tick":0,"seq":2,"phase":"commands","event":"skillCast","eid":1,"cause":null,"root":"command:0",…}`. `seq` est le numéro global de la ligne d'événement.

**Provenance (format 4, sous-lot 7.3).** Deux familles de lignes :
- **effets de jeu** : chaque ligne porte `eid` (identité de l'effet, compteur du combat dans l'ordre de création, distinct de `seq`) et `cause` (eid de l'effet parent immédiat, `null` pour l'effet racine). L'effet racine porte aussi `root`. Un effet de profondeur ≥ 1 (produit par une réaction, à partir de l'étape 7.3.4) porte `depth` et `trigger` ;
- **diagnostics et enveloppe** (`combatant`, `commandRejected`, `attackSkipped`, `stateBlocked`, `simulationAborted`, `combatantSummary`, `combatEnded`) : aucun de ces champs. Ils sont écrits par une méthode distincte et ne sont jamais observables.

La chaîne des déclencheurs n'est pas écrite : racine, profondeur et chaîne se reconstruisent en remontant `cause`. Racines : `command:<index>`, `assailantTurn:<id>:<n>` (n compte aussi les tours sautés), `cooldown:<acteur>:<compétence>:<tick>`, `periodic:<porteur>:<porteur>:<tick>` (entretien de garde), `regeneration:<porteur>:<tick>`, et `endOfTick:<combattant>:<tick>` pour une mort sans effet mortel (points de vie maximaux nuls). Parenté : les conséquences d'une compétence ou d'une attaque (impacts, gains, recharge exceptée) ont pour cause `skillCast` ou `attackStarted` ; une altération appliquée par un impact a pour cause la ligne `damage` ; les gains d'une parade ou d'un blocage ont pour cause `parrySucceeded` ou `blocked` ; la fin d'une fenêtre de parade a pour cause `parryStarted`. Une instance d'altération mémorise la provenance de sa dernière application acceptée (un renouvellement la remplace, un refus n'y touche pas) : ses ticks de DOT et son expiration ont pour cause ce `stateApplied`, l'état accordé a pour cause `stateExpired`. La mort a pour cause le premier impact ou tick de DOT qui a amené les points de vie à 0, et les retraits (`stateRemoved`) ont pour cause `died`.

Événements : `combatant`, `cooldownReady`, `skillCast`, `commandRejected`, `guardStarted`, `guardStopped`, `guardUpkeep`, `guardBroken`, `blocked`, `parryStarted`, `parrySucceeded`, `parryEnded`, `attackStarted`, `attackSkipped`, `damage` (avec `fragile` et `defense` : `none`, `guard`, `guardBroken`, `parry`, `unblockable`), `resourceGained`, `stateApplied`, `stateExpired`, `stateRemoved`, `stateBlocked`, `dotDamage`, `enduranceRegenerated`, `died`, `simulationAborted`, `combatantSummary` (avec blocages, parades, ruptures, Résonance et Charges), `combatEnded`. Version du moteur : `0.9.0` (sous-lot 7.3 : provenance des effets). Format du journal : 4 depuis le sous-lot 7.3 (`eid`, `cause`, `root`, `depth`, `trigger`, voir ci-dessus) ; les journaux des quatre scénarios livrés produits par le moteur 0.8.1 (format 3) restent identiques une fois ces champs retirés (`Lot73MigrationTests`, référence figée dans `tests/EdS.Data.Tests/Fixtures/logs-0.8.1`). Format 3 depuis le sous-lot 7.2 (`category` et `source` sur les lignes d'état, `applies` sur `attackStarted` à la place de `appliesFragility`, `stateBlocked`, `dotDamage`) ; les journaux de référence 0.7.0 restent identiques une fois ces champs retirés (`Lot72MigrationTests`). Un journal d'un format antérieur est refusé au rejeu, sans migration (pré-alpha), avant toute lecture du scénario de l'en-tête.

## Rejeu

`eds replay journal.jsonl` (ou `CombatLogReplayer`) :

1. lit l'en-tête et vérifie format, version du moteur, tick, puis l'empreinte **et** la `contentVersion` des règles et de chaque classe utilisée face aux données actuelles. Toute différence = rejeu **refusé** avant simulation : un journal n'est pas réputé reproductible si les fichiers de configuration ont changé ;
2. reconstruit le scénario depuis l'en-tête, le rejoue et compare ligne par ligne. Première différence signalée avec son numéro de ligne ; journal tronqué ou ligne modifiée = `Diverged`.

## Scénarios

Fichier `kind: "scenario"` (schéma `data/schemas/scenario.schema.json`) : graine, durée, combattants (`character` avec classe et build complet, `dummy` avec défenses, `assailant` avec défenses, cible, rythme et attaques), commandes horodatées : `{"atMs", "actor", "skill", "target"}` pour une compétence, `{"atMs", "actor", "action"}` avec `guardStart`, `guardStop` ou `parry`. Le build est vérifié par le validateur de builds du lot 4.

Scénarios livrés : `mannequin.json` (lot 5), `entrainement_garde_parade.json` (30 s contre un assaillant : garde, 8 parades réussies et 1 ratée, Fragilité, attaques imparables) `entrainement_rupture.json` (16 s : garde tenue jusqu'à la rupture, commandes refusées pendant la rupture, parades ensuite) et `entrainement_alterations.json` (sous-lot 7.2, 20 s : saignement, brûlure, étourdissement qui interrompt la garde puis une parade réussie, refus `stunned`, immunité qui bloque l'étourdissement d'un second assaillant, gel).

```bash
dotnet run --project src/EdS.Cli -- simulate scenarios/mannequin.json --out combat.jsonl
dotnet run --project src/EdS.Cli -- replay combat.jsonl
dotnet run --project src/EdS.Cli -- validate scenarios/mannequin.json
```

Résultat de référence du mannequin : 60 s, 16 compétences lancées, 2 refus voulus (Provocation non implémentée, Frappe en recharge), 441 dégâts, DPS 7,35, 48 de Résonance.
