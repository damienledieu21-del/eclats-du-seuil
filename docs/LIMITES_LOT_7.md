# Contrat de limites du lot 7 (proposition, correctif 6.1)

Statut : **contrat documentaire**. Rien de ce qui suit n'est implémenté, sauf ce qui est marqué « en place ». Il fixe à l'avance les bornes et les règles de terminaison que le lot 7 (altérations, déclencheurs) devra respecter, pour que le moteur général des états ne soit pas conçu sans garde-fous. Toutes les valeurs sont **provisoires et configurables** dans `rules.json`, bloc `combat.limits` (nouveaux champs au lot 7, donc `schemaVersion` 3 ; voir [DONNEES.md](DONNEES.md)).

## Principes

1. **Refuser tôt** ce qui peut l'être : une limite qui dépend seulement du scénario (nombre de combattants, de commandes) est vérifiée par `CombatSetup.Validate`, avant le premier tick, avec un diagnostic sur le bon chemin, comme les limites numériques du correctif 6.1.
2. **Arrêter proprement** ce qui ne se voit qu'en simulation (cascade de déclencheurs, file qui gonfle) : `simulationAborted` avec une raison stable, résultat `aborted`, code de sortie 2 et bandeau `DIAGNOSTIC` dans la CLI. Jamais d'exception, jamais de boucle infinie.
3. **Supprimer sans arrêter** ce qui relève du jeu (chaîne trop profonde, déclencheur qui se redéclencherait) : l'action est abandonnée et journalisée (`triggerSuppressed`), le combat continue.
4. Le résultat d'une limite est **déterministe** : même scénario, même journal, octet par octet.

## Limites proposées

| Limite | Valeur initiale | Champ prévu | Dépassement | Justification |
|---|---|---|---|---|
| Combattants par combat | 64 | `combat.limits.maxCombatants` | refus à la validation (`combatants`) | Les scénarios prévus (lot 8 : 1 contre 1, petits groupes) en ont moins de 10. 64 laisse de la marge aux essais de zone tout en gardant les ciblages de zone (parcours de tous les ennemis à chaque impact) et le journal de petite taille. |
| Commandes par scénario | 20 000 | `combat.limits.maxCommands` | refus à la validation (`commands`) | Un scénario de 10 min (durée maximale actuelle, 12 000 ticks) avec une commande à chaque tick en a 12 000. Au-delà, le fichier est très probablement généré par erreur. |
| Commandes par acteur et par tick | 4 | `combat.limits.maxCommandsPerActorPerTick` | refus à la validation (`commands[i].atMs`) | Un joueur peut au plus combiner garde, parade et une compétence au même tick ; 4 garde une marge. Empêche qu'un scénario sature la phase 2 d'un seul tick. |
| Événements en attente dans la file | 100 000 | `combat.limits.maxPendingEvents` | arrêt `queueLimitExceeded` | 64 combattants × (recharges, états, tours) restent sous 10 000 en usage normal ; un facteur 10 de marge. Une file qui dépasse signale un état qui se reprogramme sans fin. |
| Budget de travail par tick | 10 000 unités | `combat.limits.maxWorkPerTick` | arrêt `workBudgetExceeded` | Voir ci-dessous : distinct du budget d'événements journalisés. 10 × le budget journalisé, car une unité de travail ne produit pas toujours de ligne. |
| Événements journalisés par tick | 1 000 (**en place**) | `combat.limits.maxEventsPerTick` | arrêt `eventBudgetExceeded` (**en place**) | Valeur du plan (§4.4). Borne la taille du journal d'un tick. |
| Profondeur de chaîne de déclencheurs | 4 | `combat.limits.maxTriggerDepth` | `triggerSuppressed`, le combat continue | Valeur du plan (§4.4, A15) : permet « parade → gain de Charge → passif → effet » sans permettre de cascade longue. |

### Budget de travail distinct du budget d'événements

Le budget actuel (`maxEventsPerTick`) compte les **lignes écrites**. Or une partie du travail du moteur n'écrit rien : échéances périmées ignorées (M02), conditions de déclencheur évaluées à faux, déclencheurs supprimés, effets sans changement visible. Une boucle de travail silencieux ne serait jamais arrêtée par le budget d'événements. Le lot 7 ajoute donc un compteur de **travail**, remis à zéro à chaque tick, incrémenté d'une unité pour : chaque élément retiré de la file (y compris périmé), chaque évaluation de déclencheur, chaque application d'effet. Les deux budgets coexistent ; le premier atteint arrête le combat avec sa raison.

## Provenance et prévention des cycles

Chaque événement produit pendant la résolution porte une **provenance** :

- `rootSeq` : numéro `seq` de l'événement racine (commande, tour d'assaillant, échéance, effet périodique) ;
- `depth` : 0 pour une racine, profondeur de l'événement déclencheur + 1 sinon ;
- `trigger` : identifiant du déclencheur qui l'a produit (null pour une racine) ;
- `chain` : ensemble des déclencheurs de la chaîne depuis la racine (interne, non journalisé en entier) ;
- `sourceInstance` / `sourceGeneration` : instance et génération de l'état à l'origine de l'événement, s'il y en a un (contrat des états, [MOTEUR.md](MOTEUR.md)).

Règles :

1. Un déclencheur ne s'exécute que si `depth < maxTriggerDepth` ; sinon `triggerSuppressed` (`trigger`, `depth`, `rootSeq`, `reason: "depth"`).
2. Un déclencheur ne réagit jamais à un événement dont la chaîne le contient déjà (généralise `originTriggerId` du plan : empêche A → B → A avant même la limite de profondeur) ; `triggerSuppressed` avec `reason: "cycle"`.
3. Un événement issu d'une instance d'état retirée ou d'une génération périmée n'est jamais exécuté (même règle que les échéances du correctif 6.1).
4. Contrôle statique au chargement des données : graphe « déclencheur → événements qu'il peut produire ». Proposition par défaut (A15, à trancher) : un cycle est un **avertissement** dans le rapport de validation, la limite de profondeur garantissant la terminaison.
5. Les gains de ressources par déclencheur restent plafonnés (Charges 5, Résonance 100), ce qui borne aussi les boucles gain/consommation.

## Événement programmé dans une phase déjà passée

Ordre d'un tick : expirations → commandes → résolution → périodiques → régénération → fin de tick. Un effet résolu en phase P du tick T peut vouloir programmer un événement en (T, Q).

- **Q après P** (même tick) : exécuté normalement dans la phase Q du tick T.
- **Q = P** : ajouté à la fin de la phase en cours (ordre d'insertion), dans le même tick, avec `depth + 1` et compté dans le budget de travail. C'est déjà le comportement de la file pour la phase en cours.
- **Q avant P** : la phase est passée. Règle proposée : l'événement est reporté au tick T + 1, même phase Q, avec un champ `deferredFromTick: T` au journal. Jamais exécuté « en arrière » dans le tick courant, jamais perdu.

**En place depuis le correctif 6.1** : `EventQueue` refuse (exception de programmation) tout événement programmé avant la position déjà atteinte. Avant, un tel événement restait en tête du tas et bloquait silencieusement toute la file ; le moteur actuel n'en programme jamais (toutes ses échéances sont au moins au tick suivant), et le lot 7 devra appliquer la règle de report ci-dessus avant d'appeler la file.

## Ce qui reste hors du correctif 6.1

- Aucun moteur général d'états, aucun déclencheur, aucune altération nouvelle : Fragilité et rupture de garde restent les deux états codés du lot 6, désormais avec instance et génération.
- Les nouveaux champs `combat.limits.*` ci-dessus ne sont pas encore lus ; seuls `maxDurationMs` et `maxEventsPerTick` existent.
- Décisions à prendre avant le lot 7 : A12 à A15 et A23 (voir le rapport du lot 6), et la validation des valeurs proposées ici.
