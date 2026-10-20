# Architecture de référence du monde partagé (sous-lot 11.2)

Proposition de Claude du 20 octobre 2026, **validée par Damien le 20 octobre 2026 à 01 h 48 (« pour les 9 questions on garde tes avis »)**, avant le moteur spatial (11.4). C'est un choix difficile à défaire.

Elle applique la décision L11-D8 du 20 octobre 2026 ([registre](CADRAGE.md#5-décisions-du-20-octobre-2026-l11-d1-à-l11-d12)) : simulation de zone sous autorité du serveur ; entrée dans une zone depuis l'état courant, sans rejeu du passé ni place réservée. Elle s'appuie sur l'état réel du code ([plan, sections 1 et 2](PLAN_LOT_11.md#2-acquis-réutilisables-et-ce-qui-change)). Les quatre points de la section 9 sont retenus.

## 1. Ce que l'architecture doit permettre

Exigences retenues, reprises du cadrage :

- une **région partagée** où l'on se voit, se déplace et combat ensemble (Q9) ;
- le **combat sur place**, sans changement d'écran (Q11) ;
- des **positions réelles** (Q13) ;
- des **boss de monde** ouverts à tous (Q34) ;
- un joueur qui **arrive pendant un affrontement**, ou qui **revient après une coupure**, sans réattribution d'un événement déjà consommé (L11-D8) ;
- des rencontres **réinitialisées** quand tous ont échoué ou sont partis, sans effacer une victoire (Q8) ;
- trois **échelles de conséquences** : personnelle, d'expédition, publique (H2) ;
- des **récompenses attribuées une fois** (H4) ;
- des **secrets** qui ne se révèlent pas derrière une paroi (H14) ;
- le **déterminisme** du projet : tick de 50 ms, virgule fixe, journaux reproductibles ;
- le **même cœur** sur le serveur, le PC et le mobile.

## 2. Vocabulaire

| Terme | Sens |
|---|---|
| **Zone** | Unité logique de simulation : un lieu (les Vallons, un donjon) et tout ce qui s'y trouve, simulé au même tick. Ce n'est pas une machine : un processus serveur héberge plusieurs zones, et une zone peut tourner sur le poste du joueur en solo. |
| **Instance** | Copie d'une zone pour un groupe ou un joueur (donjon, expédition), avec sa propre simulation. |
| **Entité** | Ce qui vit dans une zone : personnage, créature, objet interactif (nœud de récolte, mécanisme du moulin). Elle a une **identité d'instance** unique dans la zone, jamais réutilisée. |
| **Rencontre** | Des entités hostiles en combat les unes avec les autres, dans la zone. Ce n'est pas un combat séparé : c'est un regroupement que la zone suit pour ses règles (qui participe, réinitialisation, crédit des actions). |
| **Événement consommable** | Fait qui donne quelque chose et ne doit compter qu'une fois : une mort, une étape de quête, une récompense. Il a une identité unique (zone, numéro). |

## 3. Autorité

- Le **serveur** détient l'état de référence : positions, attaques, dégâts, morts, puis butin, expérience, inventaires, états de quête. Il est seul juge, comme au lot 10.
- Le **client** envoie des intentions (déplacement, garde, parade, compétence, cible, interaction) et affiche. Il peut **anticiper** son propre déplacement et certaines présentations, puis il se corrige sur l'état du serveur.
- **En solo**, le jeu héberge la zone sur le poste, avec le même code, comme l'hôte du lot 10. Une partie solo n'a pas de valeur dans le monde public (Q10).

## 4. La simulation de zone

**Une simulation déterministe par zone**, au même tick que le combat. Le combat n'est pas un moteur à part qu'on lance par rencontre : c'est une partie des règles de la zone.

1. **Le moteur de combat devient le cœur de la zone.** Le moteur actuel (`CombatEngine`) a déjà l'essentiel : tick, file d'événements, commandes, budgets, provenance, déclencheurs, morts. Il lui manque :
   - des **entités qui entrent et sortent** en cours de route (aujourd'hui, la liste est fixée au départ) ;
   - les **positions** (11.4) ;
   - un **mode « zone »** sans fin globale : la zone ne s'arrête pas quand une équipe tombe ; ce sont ses rencontres qui se résolvent.

   Le mode « combat fermé » d'aujourd'hui reste, pour les essais, les mesures et les duels.
2. **Les rencontres** sont calculées par la zone : quelles entités se battent, qui y a participé, quand elles sont réinitialisées (Q8). Le crédit d'une action (dégâts, protection, soutien) se lit dans le journal, déjà relié par la provenance des effets.
3. **Le temps du monde** (jour, nuit, réapparitions) est compté en ticks de la zone, jamais à l'horloge réelle.
4. **Les identités ne sont jamais réutilisées.** Un loup qui réapparaît est une nouvelle entité. Un boss réveillé (réveil renforcé, H4) a une nouvelle vie, d'identité distincte. La première mort reste enregistrée (contrat du 11.1).

## 5. Entrée dans une zone, état courant, mises à jour

1. **Instantané d'état.** L'état complet d'une zone à un tick s'écrit de façon canonique et se relit à l'identique : entités, ressources, états, recharges, files, tirages aléatoires. On en tire une empreinte. Il n'existe pas encore : c'est un chantier du moteur (11.4, puis 11.7). Il sert à :
   - entrer dans une zone ;
   - revenir après une coupure ;
   - rejouer seulement les derniers ticks pour corriger une prédiction ;
   - sauvegarder une zone sur le serveur (lot 13) ;
   - enquêter (rejeu à partir d'un instantané).
2. **Entrée.** Le client reçoit l'état courant de ce qu'il a le droit de voir, puis des mises à jour à chaque tick. Il ne rejoue pas le passé et n'a pas de place réservée.
3. **Contenu des mises à jour.** Deux façons possibles :

   | | A. Commandes (comme le lot 10) | B. État filtré (recommandé) |
   |---|---|---|
   | Principe | Le client reçoit l'instantané complet de la zone, puis toutes les commandes ; il simule toute la zone | Le serveur envoie, à chaque tick, l'état des entités proches du joueur (zone d'intérêt) ; le client lisse leur mouvement et prédit le sien |
   | Exactitude | Exacte, contrôle d'empreinte | Approchée, corrigée par le serveur |
   | Débit | Faible | Plus élevé, borné par la zone d'intérêt |
   | Calcul du client | Toute la zone, lourd sur mobile, croît avec le nombre de joueurs | Ce qui est proche seulement |
   | Secrets | Le client connaît tout : un tricheur voit à travers les parois | Le client ne reçoit que ce qu'il peut voir ou entendre (H14) |
   | Échelle | Quelques joueurs | Une région avec beaucoup de joueurs |

   **Retenu (20 octobre 2026)** : B pour la région et les donjons. A reste pour les combats fermés (essais, mesures, duels), et le lot 10 continue de servir là.
4. **Prédiction du joueur local.** Le client simule son propre déplacement avec les mêmes règles (vitesse, ralenti en garde, décor solide). Si le serveur le place ailleurs, il revient à la dernière position confirmée et rejoue ses intentions non confirmées. Les autres entités sont affichées légèrement en retard et lissées.
5. **Annonces des coups (option B généralisée).** Le serveur envoie le tick d'impact de chaque attaque annoncée. Le client affiche le compte à rebours avancé du temps aller-retour, comme pour la parade du lot 10. Le joueur sort de la zone ou pare au moment qui compte pour le serveur. La garde et la parade restent jugées sur le tick du serveur : aucune triche possible.

## 6. Événements consommables et retour après une coupure

- Chaque événement consommable a une **identité unique** (zone, numéro croissant). Les récompenses et étapes de quête sont inscrites dans un **registre** par personnage, à clé unique (personnage, événement source, palier).
- Une même mort ne donne qu'une récompense. Ni un message en double, ni une reconnexion, ni une sauvegarde rechargée ne la donnent deux fois (H4, Q8).
- **Retour après une coupure** : le client présente son jeton (comme au lot 10) et reçoit l'état courant de sa zone d'intérêt. Le serveur garde son personnage selon les règles de la zone (garde baissée, comme au lot 10). Les événements personnels déjà inscrits au registre ne sont pas rejoués.

## 7. Zones, instances, déplacements entre zones

- Un **processus serveur** héberge plusieurs zones ; une zone peut changer de processus plus tard (lot 13 et suivants). Le choix d'un hébergeur reste séparé (dépense, décision de Damien).
- **Passer d'une zone à l'autre** : le personnage quitte une simulation et entre dans l'autre avec son état persistant (niveau, inventaire, quêtes). Rien de la zone quittée ne le suit, sauf ce qu'il a consommé.
- **Instances** : un donjon ou une expédition crée une zone pour son groupe, avec les mêmes règles. Le réveil renforcé d'un boss (H4) et ses règles de tentative y sont rattachés.

## 8. Ce que devient le lot 10, et ordre de réalisation

Le détail, acquis par acquis, est dans le [plan, section 2](PLAN_LOT_11.md#2-acquis-réutilisables-et-ce-qui-change).

**Gardés** :

- noyau déterministe ;
- protocole strict et borné ;
- transport et réseau simulé ;
- jetons de retour ;
- temps aller-retour et télégraphie avancée ;
- `IntentionPlanner`, `CombatDisplay`, robots, mesures.

**Remplacés pour le monde** :

- la salle à places fixes devient une zone où l'on entre et sort ;
- le rejeu intégral devient un état filtré suivi de mises à jour ;
- la resynchronisation par l'historique complet devient une resynchronisation par l'état courant.

Ordre de réalisation :

1. **11.4** — entités qui entrent et sortent, positions, instantané d'état du moteur, avec un test d'aller-retour exact.
2. **11.7** — démonstration réseau minimale sur ce modèle : un serveur local et deux clients ; le second arrive pendant un affrontement ; mort d'un ennemi cohérente ; retour à l'état courant sans réattribution ; latence artificielle ; prédiction du déplacement local.
3. **12.1** — la zone des Vallons sur la même simulation, hébergée sur le poste en solo.
4. **Lot 13** — serveur de zone, réplication filtrée complète, comptes de test, persistance.

## 9. Retenu par Damien le 20 octobre 2026

1. **Le principe** : une simulation déterministe par zone sous autorité du serveur ; les combats sur place en font partie ; en solo, la zone tourne sur le poste. **Retenu.**
2. **Les mises à jour du monde** : état filtré par zone d'intérêt, avec prédiction du joueur local (B, section 5). Le rejeu intégral du lot 10 (A) reste pour les combats fermés. **Retenu : B.**
3. **Les donjons et expéditions** suivent le même modèle que la région, B compris (secrets protégés, même code). **Retenu.**
4. **TCP gardé** pour la démonstration du 11.7, avec mesures. On ne passe à un transport plus rapide que si les mesures le demandent. **Retenu.**

Rien d'autre ne demande de décision de gameplay ici. Les règles du combat dans l'espace (couverture du bouclier, contact entre joueurs et créatures, portées) sont l'objet du 11.3.
