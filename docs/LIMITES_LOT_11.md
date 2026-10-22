# Limites du lot 11 (combat dans l'espace)

Plan du lot et décisions : [PLAN_LOT_11.md](PLAN_LOT_11.md), [CADRAGE.md](CADRAGE.md#5-décisions-du-20-octobre-2026-l11-d1-à-l11-d12).

## Sous-lot 11.1 : ennemis qui meurent

- **Pas encore de positions** : la mort est jugée sans distance ni placement, comme tout le combat jusqu'au moteur spatial (11.4).
- **Assaillants scriptés** : un assaillant mortel garde le comportement de l'entraînement (attaques à intervalle fixe, dans l'ordre). Il n'a ni puissance offensive propre (ses réactions infligent 0, comme avant), ni comportement dans l'espace (11.5).
- **Cycle de vie minimal** : vivant → mort, sans retour dans un même combat. Réapparition et réveil renforcé ne sont pas réalisés ; le contrat qui les rendra possibles est décrit dans [MOTEUR.md](MOTEUR.md#cycle-de-vie-des-assaillants-sous-lot-111). La mort ne donne encore rien : expérience, butin et registre des récompenses viendront au lot 12.
- **Mannequins** : toujours immortels (`dummy` n'accepte pas `health`). Un scénario qui veut une cible mortelle immobile prend un assaillant.
- **Valeurs provisoires** : les PV de la meute à vaincre (solo : 600, 600, 500 ; coopération : 900, 900, 700) sont réglés pour qu'un joueur qui se défend gagne en moins d'une minute et qu'un joueur qui ne se défend pas tombe. Ils sont mesurés avec des scripts simples (tests), pas encore avec Damien.
- **Fin de combat** : quand tous les ennemis sont morts, le combat s'arrête aussitôt (`teamDefeated`). Dans le monde partagé (lots 12 et 13), une rencontre ne terminera plus la simulation de la zone : ce sera une règle de la zone, pas du moteur.
- **Formats** : moteur 0.15.0, journal au format 9, scénarios en schéma 4. Les journaux du moteur 0.14.0 sont refusés au rejeu, sans migration (pré-alpha) ; leurs lignes d'événements sont reproduites par le nouveau moteur (tests de non-régression).

## Sous-lot 11.4a : positions, déplacement et décor

Détail du fonctionnement : [MOTEUR.md](MOTEUR.md#combat-positionné-sous-lot-114a).

- **Pas encore de portées** (au 11.4a) : dans un combat positionné, compétences et attaques touchaient sans regarder la distance ni la ligne de vue. Portées, ciblage et refus `targetOutOfRange` et `noLineOfSight` : faits au 11.4b (ci-dessous) pour les compétences, réactions et protections. Attaques annoncées au sol et garde de face (arc de 180°, orientation en garde vers la cible) : 11.4c. L'orientation est déjà tenue (direction de marche) pour ces morceaux.
- **Combattants fixés au départ** : personne n'entre ni ne sort en cours de combat, et il n'y a pas encore d'instantané d'état : levé au 11.4d (voir plus bas).
- **Créatures sans comportement** : un assaillant ne marche que par des commandes `move` et `stop` écrites dans le scénario. Approcher, poursuivre, encercler : 11.5.
- **Un seul niveau** : pas de surfaces superposées ni de passages entre elles (pont, souterrain). La proposition retenue pour la première démonstration n'en a qu'une ; les surfaces viendront avec la zone des Vallons.
- **Garde-fou anti-piège local** : il protège un joueur coincé contre le décor par une créature (huit directions d'essai, un pas d'essai), pas un joueur enfermé dans un cul-de-sac fermé à distance. Une créature qui pousse peut aussi gêner un joueur sans l'enfermer (elle glisse autour de lui). L'essai du 11.6 dira si la règle A tient avec ce garde-fou, sinon la règle B.
- **Ordre de déplacement** : les combattants avancent un par un dans l'ordre de leurs identifiants ; à contact égal, le premier prend la place. C'est déterministe, pas simultané.
- **Petits glissements ignorés** : un pas qui n'avance pas d'un huitième de sa longueur est annulé (pas de tremblement) ; pousser presque de face contre un mur ne fait donc pas glisser, et un combattant peut s'arrêter jusqu'à un huitième de pas avant le contact.
- **Étourdissement** : il immobilise (blocage `movement`, décision de Damien du 20 octobre 2026 à 03 h 38) ; la commande `move` reste admise pendant l'étourdissement (l'intention est gardée) et le combattant repart à la fin. Aucune autre altération livrée ne bloque le déplacement.
- **Réseau** : le protocole du lot 10 ne transmet pas les déplacements (refus explicite à l'écriture et à la lecture). Le combat positionné en réseau, avec prédiction du déplacement local, est le 11.7.
- **Unity** : le prototype déplace encore son personnage lui-même, hors du cœur ; le déplacement simulé par le moteur arrive avec la scène du 11.6.
- **Volume** : au plus 64 obstacles de 16 sommets, coordonnées dans ±1000 m ; aucune partition de l'espace (chaque pas teste tous les obstacles et toutes les emprises, avec un écart rapide par boîte englobante). Suffisant pour la démonstration, à revoir pour une zone peuplée (lot 13).
- **Commandes** : un joystick qui change de direction à chaque tick produit une commande par tick ; `combat.limits.maxCommands` (20 000) borne un combat fermé de dix minutes. La zone (11.7, lot 13) n'aura pas cette limite globale.
- **Formats** : moteur 0.16.0, journal au format 10, scénarios en schéma 5, règles en schéma 10 (contenu 0.9.0). Un combat sans espace écrit les mêmes lignes qu'au moteur 0.15.0 (treize scénarios livrés comparés) ; un journal 0.15.0 est refusé au rejeu, sans migration.

## Sous-lot 11.4b : portées et ligne de vue

Détail du fonctionnement : [MOTEUR.md](MOTEUR.md#portées-et-ligne-de-vue-sous-lot-114b) ; données : [DONNEES.md](DONNEES.md#portées-sous-lot-114b).

- **Attaques d'assaillant sans portée** (au 11.4b) : un assaillant frappait encore à toute distance et sans ligne de vue ; depuis le 11.4c, une attaque qui a une forme a sa portée (ci-dessous), une attaque historique sans forme frappe toujours à toute distance. Formes (`target`, `cone`, `circle`, `line`, `persistent`), annonces, défenses par attaque et arc du bouclier : 11.4c ; approche et choix de cible : 11.5. Une Riposte précise contre un assaillant éloigné est donc annulée (`outOfRange`), comme prévu (« contre un coup à distance, pas de riposte »).
- **Ciblage mixte incomplet** : seuls la compétence à cible et le cercle autour du lanceur existent. Cône et point au sol (COMBAT_ESPACE, section 4) n'ont aucune compétence livrée qui s'en sert ; ils viennent avec la première compétence ou attaque qui en a besoin (attaques annoncées du 11.4c).
- **Ligne de vue des compétences à cible seulement** : une onde, une application de groupe ou une réaction de zone passent le décor (COMBAT_ESPACE, section 1) ; une réaction dirigée (Riposte précise, Serment du gardien) ne vérifie pas non plus la ligne de vue, seulement la portée. À revoir si l'essai du 11.6 montre une riposte « à travers » un mur gênante (elle ne peut arriver qu'à 2,5 m d'un attaquant).
- **Premier effet sans cible** : la règle existante des réactions s'applique aussi à la portée : si le premier effet n'a plus de cible à portée, toute la réaction est annulée (`outOfRange`), effets suivants compris. Pour Jugement du colosse, une parade parfaite sans ennemi à 5 m ne rapporte donc pas ses 10 points de Résonance (question posée à Damien dans la PR).
- **Mesure en bords d'emprise et en cercle touché** : une portée de compétence à cible se mesure entre les bords des deux emprises ; un rayon de groupe compte un combattant dès que son emprise touche le cercle. Une grande créature (1,2 m) est donc atteinte un peu plus loin qu'un loup : c'est voulu (« distance entre les bords des emprises », COMBAT_ESPACE, section 4).
- **Verrouillage** : une compétence à cible part sur sa cible même si celle-ci sort de portée pendant la phase de déplacement du même tick (verrouillage au lancement, décision L11-D2). Une compétence de groupe, elle, est jugée à l'impact, après les déplacements du tick.
- **Une surface** : la portée ne regarde pas les niveaux (un seul niveau au lot 11).
- **Volume** : un contrôle de ligne de vue teste tous les obstacles (boîte englobante puis côtés) ; au plus 64 obstacles, suffisant pour la démonstration.
- **Formats** : moteur 0.17.0, journal au format 11, classes en schéma 9 (Bastion 0.6.0), règles en schéma 11 (contenu 0.10.0), scénarios inchangés (schéma 5). Un combat sans espace écrit les mêmes lignes qu'au moteur 0.16.0 (quatorze scénarios livrés comparés) ; un journal 0.16.0 est refusé au rejeu, sans migration ; les contenus figés du Bastion (0.2.0 à 0.5.0) sont passés au schéma 9 sans autre changement.

## Sous-lot 11.4c : attaques annoncées et garde de face

Détail du fonctionnement : [MOTEUR.md](MOTEUR.md#attaques-annoncées-et-garde-de-face-sous-lot-114c) ; données : [DONNEES.md](DONNEES.md#scénarios-et-jeu-de-données-lots-5-et-6).

- **Contrôle pendant l'annonce** : un étourdissement posé sur l'assaillant pendant son annonce ne l'interrompt pas (l'impact a lieu) ; seule sa mort l'annule. Choix simple, à confirmer par Damien (question posée dans la PR) : interrompre une annonce par le Heurt de bouclier serait une vraie mécanique de jeu.
- **Provocation et zones visées** : une provocation détourne aussi les zones visées de l'ennemi (cône, bande, cercle autour de sa cible), comme ses coups ; le Bastion qui provoque prend donc le souffle à la place de son allié. Choix du développement, à confirmer par Damien (question posée dans la PR #31). Les attaques sur tous les ennemis et les zones centrées sur l'ennemi ne changent pas.
- **Rotation instantanée** : en garde, le Bastion fait face à sa cible sélectionnée à chaque tick, sans vitesse de rotation ; un ennemi qui le contourne n'est donc frappé de dos que si le Bastion n'a pas sélectionné cet ennemi-là (ou ne garde pas). À régler à l'essai du 11.6 si c'est trop facile.
- **Cône par le centre** : le cône frappe une emprise dont le centre est dans son ouverture et à sa portée (rayon compris), ou qui touche l'un de ses côtés ; une emprise qui ne touche que l'arc de bout hors de l'ouverture n'est pas comptée (écart de quelques centimètres au plus pour une emprise de 0,5 m).
- **Pas de zone qui bouge** : une zone reste où elle a été posée ; un souffle qui suit sa cible n'existe pas. La charge (11.5) est une bande posée à l'annonce, puis un déplacement de l'ennemi à l'impact.
- **Origine du coup pour le bouclier** : l'assaillant pour un cône ou une bande, le centre pour un cercle ; une zone centrée sur le Bastion est donc toujours « de face ».
- **Annonces évitables** : le contrôle prend le plus grand joueur du combat et le pire point de la forme (centre du cercle inscrit) ; il ne tient pas compte du décor qui pourrait bloquer la sortie, ni de la garde (qui ralentit de moitié).
- **Attaques historiques** : une attaque sans forme garde son ciblage d'avant (la cible ou tous les ennemis, à toute distance), mais l'arc du bouclier s'applique à elle dans un combat positionné (l'assaillant est l'origine du coup).
- **Réseau** : la sélection n'est pas transmise par le protocole du lot 10 (11.7). L'annonce en réseau (tick d'impact envoyé au client) viendra avec la démonstration réseau.
- **Formats** : moteur 0.18.0, journal au format 12, scénarios en schéma 6, règles en schéma 12 (contenu 0.11.0), classes inchangées (schéma 9). Un combat sans espace écrit les mêmes lignes qu'au moteur 0.17.0 (quinze scénarios livrés comparés) ; un journal 0.17.0 est refusé au rejeu, sans migration.

## Sous-lot 11.4d : arrivées, départs et instantané d'état

Détail du fonctionnement : [MOTEUR.md](MOTEUR.md#arrivées-départs-et-instantané-détat-sous-lot-114d) ; données : [DONNEES.md](DONNEES.md#scénarios-et-jeu-de-données-lots-5-et-6).

- **Mode combat fermé seulement** : une équipe sans combattant présent vivant ni arrivée attendue perd le combat, y compris si tous ses membres sont partis (victoire de l'autre équipe). Le mode « zone » sans fin globale, où l'on entre et sort sans rien terminer, vient avec la zone des Vallons (12.1).
- **Place d'arrivée** : une arrivée sur une emprise solide est refusée, pas décalée. Le serveur (11.7) choisit une place libre avant d'appeler `Join`, ou réessaie au tick suivant ; aucune place n'est réservée d'avance.
- **Un mort ne part pas** : un départ demandé au tick d'une mort est annulé (`leaveCancelled`) ; un mort reste dans le combat (bilan et réapparition : lot 12).
- **Départ en fin de tick** : le combattant qui part agit, peut être frappé et réagir jusqu'à la fin de son tick (après les morts du tick et leurs réactions) ; ses DOT posés sur les autres continuent (comme à sa mort).
- **Intégrité d'un instantané** : la relecture vérifie la forme (lisible, canonique, références et bornes, file à la frontière du tick) et refuse un état que le moteur n'atteint jamais entre deux ticks (PV hors de [0, max] ou nuls sans mort, attaque annoncée en double, ressource hors de ses bornes, altération chez un mort, élément de file hors de sa phase, place dans le décor…, liste complète dans [MOTEUR.md](MOTEUR.md#arrivées-départs-et-instantané-détat-sous-lot-114d)) ; `Restore` ne prouve pas qu'un état atteignable est celui d'un vrai combat (des PV changés de 500 à 499 passent) : il est réservé aux instantanés de confiance. Un instantané reçu d'ailleurs se reprend avec `RestoreVerified`, qui rejoue le combat complété jusqu'à son tick et exige le même état (décision de Damien du 22 octobre 2026) : exact, au prix du calcul du passé.
- **Instantané à relire avec son combat** : l'instantané ne contient pas les définitions (classes, builds, règles, scénario) : il se relit avec le combat complété au moment de l'instantané, dont il porte l'empreinte. Un client en zone (11.7, état filtré retenu) ne relit pas d'instantané du moteur : il reçoit l'état des entités proches ; l'instantané sert au serveur (sauvegarde d'une zone, reprise, enquête) et aux essais.
- **Moteur relu** : ses commandes soumises et arrivées pilotées repartent de zéro (celles d'avant sont dans le combat complété fourni) ; il écrit la suite du journal sans en-tête : le journal complet rejouable reste celui du combat complété (`CompletedSetup`).
- **Taille et coût** : texte de l'ordre de quelques Kio pour un petit combat (une ligne par combattant et par élément de la file) ; écrit et relu en temps linéaire, sans compression ; la reprise vérifiée rejoue en plus le passé (calcul seul). Rien n'est encore mesuré sur une zone pleine (12.1).
- **Pas de commande en ligne** : `eds` n'écrit ni ne relit encore d'instantané (essais et code seulement).
- **Formats** : moteur 0.19.0, journal au format 13, scénarios en schéma 7, instantané au format 1 ; règles (schéma 12) et classes (schéma 9) inchangées. Un combat sans arrivée ni départ écrit les mêmes lignes qu'au moteur 0.18.0 (seize scénarios livrés comparés) ; un journal 0.18.0 est refusé au rejeu, sans migration.

## Sous-lot 11.5 : comportements des ennemis

Détail du fonctionnement : [MOTEUR.md](MOTEUR.md#comportements-des-ennemis-sous-lot-115) ; données : [DONNEES.md](DONNEES.md#scénarios-et-jeu-de-données-lots-5-et-6).

- **Pas de recherche de chemin** : un ennemi marche droit vers sa place et glisse le long du décor ; derrière un mur en U, il reste coincé. À revoir avec la scène du 11.6 (passage étroit, pont).
- **Pas de retour au calme** : une fois engagé, un ennemi le reste jusqu'à la fin du combat ou sa mort ; il poursuit sa cible partout (pas de laisse, pas de réinitialisation : règles de rencontre Q8 au lot 12).
- **Anneau simple** : la meute se répartit à angles égaux autour de sa cible, sans tenir compte du décor ni des autres ennemis qui ne l'encerclent pas ; une place dans un mur reste visée (l'ennemi s'arrête contre lui).
- **Arrêt pendant l'annonce** : un ennemi s'arrête de marcher pendant l'annonce de chacune de ses attaques, coups dirigés compris (une annonce dont l'impact tomberait après la fin du combat l'arrête jusqu'à la fin : revue Codex de la PR #33, 13e tour ; champ `announcedToEnd` de l'instantané, admis seulement pour un tour qui a pu être annoncé, après l'engagement d'un ennemi à comportement : 17e tour). Choix du développement pour que l'annonce se lise (question posée dans la PR).
- **Charge sans recul ni poussée** : la charge s'arrête au contact d'un joueur (règle A) ; elle ne le repousse pas. Elle ne suit pas sa cible pendant l'annonce. Dans un instantané, la place d'un chargeur n'est libérée qu'après une charge réellement résolue (champ `charged`, 17e tour de la revue de la PR #33) ; une charge bloquée (étourdi, absent) ne la libère pas. La reprise de confiance (`Restore`) contrôle la vraisemblance de ce champ (un tour de charge a pu tomber), la reprise vérifiée par rejeu (`RestoreVerified`) son exactitude.
- **Engagement par la meute** : un allié engagé à distance d'engagement entraîne les autres, de proche en proche (question posée dans la PR : c'est ce qui fait venir toute la meute).
- **Pilote de mesure** : `CombatPilot` ne déplace pas son personnage ; un scénario positionné mesuré par `eds report` laisse les ennemis venir à lui.
- **Formats** : moteur 0.20.0, journal au format 14, scénarios en schéma 8, instantané au format 2 ; règles (schéma 12) et classes (schéma 9) inchangées. Un combat sans comportement ni charge écrit les mêmes lignes qu'au moteur 0.19.0 (dix-sept scénarios livrés comparés) ; un journal 0.19.0 est refusé au rejeu, sans migration.
