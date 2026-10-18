# Feuille de route — Éclats du Seuil

Source de vérité pour les agents : le lot en cours, les décisions validées par Damien et la suite prévue. Les chiffres de gameplay sont des valeurs initiales **provisoires**, pas un équilibrage définitif. Toute modification d'une décision validée passe par Damien.

## Vision

MMORPG fantasy PC/mobile, monde partagé et persistant à terme, **principalement jouable en solo** (aucun groupe imposé pour la moindre activité), combats dynamiques en temps réel, progression profonde, classes aux identités mécaniques très distinctes, trois arbres de talents combinables par classe, synergies, exploration, métiers, économie, coopération facultative, cross-play PC/mobile. Direction artistique : fantasy stylisée plutôt sombre (base créative, pas une charte). Caméra : **non tranchée** (isométrique 3D orientable, troisième personne ou hybride), à comparer dans le prototype Unity.

## Progression validée (ne change pas pendant le lot 8)

- Niveau maximal provisoire 30 ; 1 point de talent par niveau à partir du niveau 2, soit 29 points au niveau 30.
- Trois arbres librement combinables. Talent majeur : 18 points dans son arbre + prérequis, puis 1 point. Un seul majeur par build ; un build sans majeur est valide.
- Actives : 2 (niv. 1), 3 (niv. 5), 4 (niv. 10). Ultime : 1 à partir du niveau 10. Passifs équipables (distincts des talents) : 1 (niv. 1), 2 (niv. 5), 3 (niv. 20), 4 (niv. 30).
- Budget d'attributs et progression de long terme : chantier ultérieur.

## Ordre des chantiers

`8.1 → 8.2 → 8.3 → 8.4 → revue PR 8a → 8.5 → 8.6 → 8.7 → prototype Unity 3D → serveur autoritaire à deux joueurs → vertical slice`

### Lot 8 — premier Bastion réellement jouable

Deux PR de gameplay (A11) : **8a moteur** (8.1 à 8.4, PR #3 et branches empilées), puis **8b contenu et mesures** (8.5 à 8.7).

| Sous-lot | Contenu | État |
|---|---|---|
| 8.1 | X1 modificateurs permanents, X2 conditionnels (`whileGuarding`, `perRampartCharge`, `perResonanceStep`), règles effectives par combattant, plafonds | PR #3 (brouillon) ; décisions du 18/10 appliquées (moteur 0.11.0, schéma de règles 6) |
| 8.2 | X3 renforcements temporaires, X4 boucliers d'absorption (catégorie `shield`, un bouclier par source, consommation FIFO, absorbe aussi les DOT, fin par durée ou épuisement, champ `absorbed`), réduction de dégâts et modificateurs temporaires | PR empilée sur la PR #3 (brouillon) : catégories `buff` et `shield`, schéma de règles 7 et de classe 6, moteur 0.12.0, journal au format 6 ; aucun contenu livré |
| 8.3 | X5 ciblage `self` / `ally` / `allAllies`, X6 transfert de dégâts, X7 provocation, X9 rôle `ally` des déclencheurs | PR #7, empilée sur la PR #6 (brouillon) : compétences de soutien, catégories `taunt` et `protection`, règles en schéma 8, classes en 7, scénarios en 3, moteur 0.13.0, journal au format 7 ; aucun contenu livré ; deux tours de revue Codex traités |
| 8.4 | X8 Contrecoup consomme toutes les Charges, fiches complètes des compétences restantes et de l'ultime, L8-D2, aucun `notImplemented` restant | PR empilée sur la PR #7 (brouillon) : fiches complètes du Bastion, Charges dépensées et coefficient par Charge, `resonanceGain`, `guard.noUpkeep`, `combat.triggers.areaStrikeMinCooldownMs` (3 s), rupture de garde limitée aux contrôles et modificateurs ; règles en schéma 9 (contenu 0.7.0), classes en 8 (contenu 0.3.0), moteur 0.14.0, journal au format 8 |
| 8.5 | Contenu complet du Bastion (talents, intermédiaires, passifs), plus aucun `[TEST]`, builds de référence en données | à faire |
| 8.6 | X10 compteurs de mesure, scénarios S1 à S5, commande CLI `report`, catégorie `Scenario` | à faire |
| 8.7 | Passe d'équilibrage mesurée, surtout en données ; rapport de mesures et écarts | à faire |

Ordre défensif attendu en 8.2 : garde ou parade selon leurs règles, puis bouclier, puis PV. Tests 8.2 attendus : bouclier épuisé pendant un impact, plusieurs boucliers, DOT successifs, mort d'un personnage protégé, réservation exacte, aucun dégât négatif ni effet partiel.

Règles 8.3 : transfert non récursif et non notifiable aux déclencheurs offensifs, conservation des dégâts à l'arrondi près, garde du protecteur seulement selon Serment, protection retirée à la mort du protecteur, allié mort ou hostile refusé, provocation d'un personnage refusée et journalisée (`notApplicable`), provocation la plus récente prioritaire, mort du provocateur gérée.

Tests 8.4 attendus : riposte automatique de zone avec recharge interne 2 950 ms refusée, 3 000 ms acceptée ; Charges insuffisantes ; cibles invalides ; interactions de ressources ; ultimes, boucliers et provocations.

## Décisions validées

**A1 à A12** : pas de soin pour le Gardien au lot 8 (boucliers, protections, transferts, provocations) ; trois talents intermédiaires de cinq rangs par arbre (noms de travail) ; Fureur contenue dépend de la Résonance détenue ; Charges conservées (Rempart) ou dépensées (Contrecoup) selon le build ; une ultime commune, Bastion absolu ; les boucliers absorbent les DOT ; un bouclier par source, FIFO ; dégâts transférés jamais retransférés ni déclencheurs de réactions, garde du protecteur seulement dans le cadre de Serment ; provocation sur un personnage refusée `notApplicable` et journalisée ; attributs identiques pour les builds de comparaison ; deux PR de gameplay 8a puis 8b ; builds de référence livrés en données.

Aussi : aucun soin, aucune deuxième classe, aucun système de positions, aucune interface Unity avant la fin du lot 8 ; pas de déclencheurs d'équipement avant le chantier des objets.

**L8-D1 à L8-D6** : frappes de réaction sans critique ni Résonance automatique ; toute riposte automatique de zone a au moins 3 s de recharge interne (sauf configuration explicite de test) ; une réaction mixte s'exécute entièrement pendant la résolution ; pas d'annulation `staleSource` pour les effets autonomes ; pas de condition générale `requiresAbleToAct` (les déclencheurs autorisés réagissent malgré un étourdissement) ; déclencheurs d'équipement reportés au système d'objets.

**Plafonds 8.1** : réduction de garde 75 % ; réduction totale des dégâts subis 85 %, avant bouclier (paramètres de `rules.json` depuis la décision 2 ci-dessous).

**Décisions du 18 octobre 2026** (questions de fin du rapport 8.1) :

1. **Rempart éternel** : garde incassable (`guard.noUpkeepBreak`) avec réduction de 40 % à 0 endurance ; le raccourci de rupture par Charge est **retiré** de ce majeur.
2. Les plafonds de 75 % et 85 % deviennent des **paramètres de `rules.json`** (nouvelle version du schéma de règles).
3. **Blocage à coût nul** : comportement actuel conservé (il relance le délai de régénération).
4. Un `perRank` de **déclencheur** sur un talent à un seul rang est **refusé**, comme pour les modificateurs.

**Décisions du 18 octobre 2026** (interprétations du rapport 8.4, PR #8, toutes gardées par Damien) :

5. **Provocation** : les +5 de Résonance sont gagnés au lancement, même si la provocation est refusée (cible qui n'est pas un assaillant).
6. **Bastion absolu** : les 8 s valent pour ses trois effets ; aucune recharge propre, la Résonance (100) seule le limite ; garde sans entretien pour le Bastion seul, −30 % de dégâts subis pour lui et ses alliés.
7. **Contrecoup** : les Charges sont dépensées au lancement ; un bonus « par Charge détenue » ne compte pas sur son impact.

## Catalogue du Bastion (valeurs initiales du plan)

Trois arbres : **Rempart** (garde, endurance, parade, Charges, survie), **Contrecoup** (ripostes, dégâts, Résonance), **Gardien** (boucliers, protection, transfert, soutien ; utile aussi en solo).

**Rempart** — Garde stable (3 rangs : réduction de garde +4 %, entretien −0,25 endurance/s par rang) ; Écho défensif (1 rang : un blocage rend 4 endurance ; avec recharge interne de 3 s, donne aussi une Charge) ; **Rempart éternel** (majeur, décision 1 ci-dessus). Intermédiaires : Peau de fer (dégâts subis −1,5 %/rang), Souffle du roc (endurance max +4 et régénération +1/s par rang), Poids du rempart (en garde, réduction additionnelle de 0,5 % par rang et par Charge).

**Contrecoup** — Riposte précise (3 rangs : une parade parfaite frappe l'attaquant, coefficient 0,5 + 0,25/rang, sans critique ni Résonance) ; Fureur contenue (2 rangs : bonus aux dégâts directs par tranche de 10 Résonance détenue, jusqu'à +20 % à 100 au rang 2) ; **Jugement du colosse** (majeur : une parade parfaite déclenche une frappe de zone 1,2, +10 Résonance, recharge interne ≥ 3 s). Intermédiaires : Main lourde (dégâts directs +2 %/rang, réactions comprises), Élan (récupération après parade ratée −50 ms/rang), Écho de guerre (+2 Résonance/rang après une parade parfaite).

**Gardien** — Égide réactive (3 rangs : parade parfaite ou blocage donne un bouclier de 3 % des PV max par rang, 4 s, recharge interne 2 s, renouvellement au plus fort) ; Lien protecteur (1 rang : Barrière d'acier protège aussi un allié lié, 25 % de ses dégâts transférés au Bastion) ; **Serment du gardien** (majeur : provoque l'assaillant qui frappe l'allié lié, 2 s, recharge interne 3 s, transfert porté à 40 %, dégâts transférés passant par la garde du Bastion). Intermédiaires : Vigilance (PV max +2 %/rang), Présence (provocations +0,2 s/rang), Rempart vivant (boucliers posés +4 %/rang).

**Passifs équipables** : Endurance du roc (endurance max +15), Bouclier lourd (coût par blocage −3), Réserve de Charges (Charges max +1), Œil du duelliste (fenêtre de parade +50 ms), Ferveur (Résonance par impact direct +1), Posture de fer (en garde, dégâts subis −5 %), Sentinelle (premier impact sur l'allié lié : +1 Charge, recharge 6 s), Ténacité (résistance aux altérations +100). Martèlement et Vengeance ne sont pas réintroduits.

**Compétences** — Frappe du rempart (1,2 ; 10 endurance ; 6 s ; ennemi unique) ; Heurt de bouclier (0,8 ; 15 ; 10 s ; étourdissement 1,5 s) ; Onde de rupture (0,5 ; 20 ; 12 s ; tous les ennemis ; Fragilité) ; Contrecoup (10 ; 8 s ; consomme toutes les Charges, au moins une ; 0,6 + 0,4 par Charge) ; Provocation (10 ; 12 s ; assaillant ; 3 s ; +5 Résonance) ; Barrière d'acier (20 ; 20 s ; bouclier de 15 % des PV max du Bastion, 6 s, sur soi ou un allié). **Ultime Bastion absolu** : 100 Résonance, 8 s, dégâts subis −30 % pour le Bastion et ses alliés, provoque tous les assaillants, garde sans entretien pendant l'effet.

## Builds et scénarios de référence (8.5 à 8.7)

Attributs communs niveau 30 : Force 30, Dextérité 10, Intelligence 6, Vitalité 30, Volonté 15, Endurance 20.

- **Rempart** : 19 Rempart / 8 Gardien / 2 Contrecoup, majeur Rempart éternel ; Frappe du rempart, Heurt de bouclier, Barrière d'acier, Onde de rupture ; Endurance du roc, Bouclier lourd, Réserve de Charges, Posture de fer.
- **Contrecoup** : 19 Contrecoup / 10 Rempart, majeur Jugement du colosse ; Frappe du rempart, Contrecoup, Onde de rupture, Heurt de bouclier ; Œil du duelliste, Ferveur, Endurance du roc, Ténacité.
- **Gardien** : 19 Gardien / 9 Rempart / 1 Contrecoup, majeur Serment du gardien ; Provocation, Barrière d'acier, Frappe du rempart, Heurt de bouclier ; Sentinelle, Posture de fer, Ténacité, Endurance du roc.
- Contrôles : hybride 10/10/9 sans majeur ; niveau 10 (9 points, 4 actives, ultime, 2 passifs).

Scénarios : **S1** mannequin 60 s (DPS brut) ; **S2** duel soutenu 120 s (assaillant toutes les 1,5 s, Fragilité périodique) ; **S3** meute 60 s (3 assaillants, zone et saignement) ; **S4** protection 90 s (Bastion, allié fragile, 2 assaillants) ; **S5** briseur de garde 90 s (attaques imparables régulières). Profils : débutant (aucune parade parfaite), standard (une attaque sur deux), expert (toutes les attaques éligibles).

Objectifs provisoires : S1 écart de DPS ≤ 15 % ; S2 DPS Contrecoup ≥ +25 % sur Rempart ; S2/S5 survie et dégâts évités du Rempart ≥ +25 % sur Contrecoup ; S4 allié protégé ≥ 1,5 fois plus longtemps avec Gardien ; tous scénarios < 50 % de chaque budget moteur. Chaque spécialisation excelle sur sa mesure, aucune ne domine partout ; les écarts sont mesurés et expliqués.

## Après le lot 8

1. **Prototype Unity 3D** : importer le cœur C# sans le réécrire ; personnage, déplacements, caméra expérimentale (isométrique et troisième personne comparées), garde et parade visibles, compétences, ennemis d'entraînement, barres de ressources, altérations, première interface de talents. Graphismes provisoires.
2. **Serveur autoritaire à deux joueurs** : deux clients, le serveur résout le combat avec le même cœur, commandes invalides refusées, déconnexion/reconnexion, tests de latence, désynchronisation et rejeu. Pas d'infrastructure massive avant cette preuve.
3. **Vertical slice** : une zone, le Bastion jouable, quelques ennemis, une mission, progression, récompense, inventaire, une activité de récolte et fabrication, interactions avec un deuxième joueur.
4. Ensuite, par grands chantiers : progression complète et équipement ; monde, régions, missions ; métiers ; économie ; classes supplémentaires ; multijoueur étendu ; persistance ; interfaces PC/mobile ; direction artistique et son ; tests jouables et première alpha.
