# Le monde (lot 12)

Le projet `EdS.World` (netstandard2.1, sans dépendance) réalise l'architecture du monde partagé ([ARCHITECTURE_MONDE.md](ARCHITECTURE_MONDE.md), retenue le 20 octobre 2026) pour la tranche des Vallons d'Aube ([PLAN_LOT_11.md](PLAN_LOT_11.md#lot-12--les-vallons-daube-en-solo)). En solo, la zone tourne sur le poste, avec le code qu'utilisera le serveur de zone (lot 13).

## Session de zone (sous-lot 12.1)

Une **session de zone** (`Zone`) est une simulation de zone : le moteur de combat en mode zone ([MOTEUR.md](MOTEUR.md#mode-zone-sous-lot-121)), qui ne s'arrête pas quand une équipe tombe.

- **Identité** : `zone.<nom>` (`zone.vallons`), numéro de session (1 pour la première partie, puis un de plus à chaque reprise), temps du monde au début de la session.
- **Entrer et sortir** : `Enter(personnage)` le fait arriver au tick courant, à sa place (arrivée pilotée du moteur : refusée si une emprise solide occupe la place) ; `Leave(id)` le fait partir en fin de tick. Un personnage qui entre avec la session (`entering`, utilisé par la partie en solo) fait partie de sa simulation de départ, donc de l'en-tête du journal (identité de sa classe comprise), et arrive au tick 0. Rien n'est réservé d'avance.
- **Avancer** : `Step()` simule un tick et rend les **événements consommables** du tick. Ne pas faire avancer le moteur à côté.
- **Événements consommables** (section 6 de l'architecture) : chaque mort a une clé unique dans le monde, `zone.vallons/<session>/death/<combattant>`. Elle est inscrite au **registre** de la session ; `FirstTime` dit si elle l'est pour la première fois. Le registre est gardé dans la sauvegarde : une session rejouée, un message en double ou une sauvegarde rechargée ne redonnent pas une mort comme nouvelle. Les étapes de quête et les récompenses utiliseront le même registre (`Consume(clé)`, sous-lots suivants).
- **Rencontres** (section 2 de l'architecture) : `Encounters()` regroupe les créatures engagées et vivantes avec la cible qu'elles poursuivent, de proche en proche. C'est sur elles que s'appuieront la laisse et la réinitialisation (12.4).
- **Déterminisme** : les mêmes entrées donnent la même zone, tick après tick (instantané du moteur identique).

## Temps du monde

`WorldClock` tire l'heure du monde des ticks, jamais de l'horloge réelle (cadrage Q26). Valeurs **provisoires**, réglables : une journée du monde dure **3 h de jeu** (décision de Damien du 30 octobre 2026, sujet 32 ; 2 h avant) ; le monde commence à **8 h** du jour 0. Moments de la journée : nuit de **22 h** à 5 h, aube de 5 h à 7 h, jour de 7 h à 19 h, crépuscule de 19 h à **22 h** (décision de Damien du 30 octobre 2026, 23 h 06 ; la nuit commençait à 21 h). Tout est entier : un même tick donne toujours la même heure. Le temps du monde continue d'une session à l'autre (début de session = temps de la sauvegarde). Il est gardé en **ticks** : une sauvegarde d'avant le passage à 3 h reprend au même tick, donc à une autre heure et un autre jour du monde (le stock du jour des marchands suit ce jour) ; rien d'autre ne change. Les effets du jour et de la nuit sur le jeu (créatures, traces, ressources, passages) viendront avec le contenu des Vallons.

## Personnage (sous-lot 12.2)

Le personnage persistant (cadrage Q3, Q20) : un identifiant, une classe, son **expérience**, ses talents et ses compétences choisies. Son **niveau** se déduit de l'expérience ; ses **attributs** viennent d'une table par classe et niveau (pas de points à répartir à la main) ; son build est validé contre la classe et le niveau par le validateur unique des builds avant chaque entrée en zone.

- **Progression** : fichier `data/v0/world/progression.json` (`kind` « progression », schéma `data/schemas/progression.schema.json`, validé par `ProgressionValidator`). Valeurs **provisoires**, réglables dans les données :
  - niveaux 1 à 10 ; expérience cumulée pour atteindre chaque niveau : 0, 100, 220, 360, 520, 700, 900, 1 120, 1 360, 1 620 ;
  - expérience d'une créature vaincue : ses PV maximaux divisés par 5 (au moins 1) — un loup de 150 PV donne 30, un sanglier de 180 PV 36 (essai du 25 octobre 2026 : divisés par 20 auparavant) ;
  - attributs du Bastion du niveau 1 (force 12, dextérité 6, intelligence 4, vitalité 12, volonté 8, endurance 6) au niveau 10 (18, 7, 5, 18, 10, 10), jamais en baisse d'un niveau à l'autre ;
  - build de départ du Bastion : Frappe du rempart et Heurt de bouclier, Endurance du roc ; aucun talent ;
  - **compétences gagnées en montant de niveau** (12.9b, choix de Damien du 25 octobre 2026 : `skillUnlocks`, progression en schéma 2) : Barrière d'acier au niveau 5, Onde de rupture et l'ultime Bastion absolu au niveau 10, aux niveaux où les règles ouvrent leurs emplacements (`progression.slots` : 2 actives au niveau 1, 3 au 5, 4 et l'ultime au 10) ; depuis la réponse de Damien du 30 octobre 2026, **Provocation au niveau 3 et Contrecoup au niveau 7**, qui vont dans la **réserve** (aucune place de leur sorte ne s'ouvre à leur niveau : le joueur les place lui-même avec l'écran des compétences, touche B). Une compétence gagnée **vient avec sa place** (`SkillUnlock.WithPlace`) quand une place de sa sorte s'ouvre à son niveau dans les règles ; c'est déduit des règles, sans champ de plus (format inchangé). Validation : compétence de la classe, pas déjà connue, niveau de 2 au niveau maximal et au moins son niveau minimal, niveaux dans l'ordre, et à chaque niveau un build valide (départ et compétences gagnées avec leur place jusque-là ; celles de la réserve n'ont pas à tenir dans la barre).
- **Création** : `PlayerCharacters.Create(id, classe, progression)` donne un personnage de niveau 1 avec le build de départ de sa classe.
- **Apprentissage** (12.9b) : `PlayerCharacters.Learn` équipe les compétences gagnées **avec leur place** jusqu'au niveau du personnage qu'il n'a pas encore, dans l'ordre de la progression, chacune s'il reste un emplacement de sa sorte ouvert à ce niveau (une active prend l'emplacement libre suivant ; si l'emplacement est déjà pris, rien n'est remplacé et la compétence va dans la réserve, donnée pour de bon : la halte ne la placera plus). La partie en solo l'applique à l'ouverture de chaque session (un niveau prend effet à la halte) : `WorldSession.Learned` dit ce qui vient d'être équipé, la sauvegarde le garde. L'aventure annonce la compétence à venir quand le niveau est atteint, puis à la halte la compétence et sa touche. Une compétence de la réserve est apprise dès le niveau atteint (l'écran la montre aussitôt) : l'aventure l'annonce alors (« Nouvelle compétence en réserve : Provocation. Place-la dans ta barre depuis l'écran des compétences. », texte `text.ui.skill_reserve`, sans touche pour convenir aussi au doigt) et la halte ne la place jamais, même si la barre a de la place.
- **Changer ses compétences** (sous-lot V1.5, décision de Damien du 29 octobre 2026 : « partout hors combat ») : la barre du personnage (actives, ultime, passives) se change partout, sauf tant qu'un ennemi vivant est à moins de 12 m (la règle des talents et de la halte) ou qu'un ennemi **engagé le vise**, même de plus loin (un archer), qu'un dialogue est ouvert ou que le personnage est tombé (`AdventureSession.CanChangeSkills` ; un motif par cas, `AdventureSession.SkillsLocked`, textes `text.ui.skills.in_combat`, `in_dialogue` et `defeated`). Un seul juge, `PlayerCharacters.CheckSkills`, au niveau actuel du personnage (d'après son expérience) et avec ses talents, dans l'ordre :
  1. une **place fermée** est refusée avec le niveau qui l'ouvre (`PlayerCharacters.ClosedPlace` ; règles : 2 actives au niveau 1, 3 au 5, 4 et l'ultime au 10 ; passives aux niveaux 1, 5, 20 et 30) ;
  2. une compétence doit être **apprise** (`PlayerCharacters.Learned` : celles de départ, puis celles gagnées jusqu'au niveau du personnage) ou déjà dans sa barre (une sauvegarde d'avant reste jouable ; une fois retirée, elle ne revient pas) ;
  3. le **validateur unique des builds** (sorte de la place, doublon, niveau minimal, talents exigés).

  **Une place peut rester vide** (réponse de Damien du 30 octobre 2026 : « oui, permis ») : la règle « barre pas pire » de la relecture de la PR #71 (une place ouverte ne se vidait pas tant qu'une compétence apprise pouvait la remplir) est retirée, ultime et passives comprises. Pour que la halte ne remplisse pas une place vidée, une compétence gagnée avec sa place n'est **donnée qu'une fois** : sa clé `character/skill/<compétence>` (`SkillUnlock.GivenKey`) est inscrite au **registre du monde** (événements consommés, déjà gardé dans la sauvegarde : format inchangé) quand la halte la donne (placée, déjà dans la barre, ou en réserve faute de place) ou quand le joueur change sa barre après l'avoir vue à l'écran (niveau gagné en route). `PlayerCharacters.Learn` ne place plus une compétence dont la clé est au registre. Une sauvegarde d'avant (sans clé) : à la première entrée, les compétences avec leur place déjà dans la barre sont données sans rien changer ni annoncer, et une qui manquait dans une place libre y est placée une dernière fois, comme avant. Comme la découverte d'un point sûr, les clés ne sont inscrites que s'il reste au registre, après elles, la place des morts possibles d'un tick, et **toutes ou aucune** (relecture indépendante de la PR #74) : à l'entrée, sans cette place, rien n'est inscrit et elles seront données à une halte suivante ; un changement de barre qui demanderait des clés sans cette place est **refusé en entier** (motif `RegistryFull`, texte `text.ui.skills.registry_full`), rien ne change.
  - Limite (relecture indépendante de la PR #74) : l'état « compétence donnée » est propre au personnage mais vit dans le registre du monde, avec une clé sans identifiant de personnage (`character/skill/<compétence>`) ; c'est juste tant qu'une sauvegarde a un seul personnage. **Avant le lot 13** (monde partagé), la clé devra porter l'identifiant du personnage, ou cet état passer dans le personnage avec une nouvelle version de la sauvegarde.

  Refus atomique avec son motif (places fermées par `PlayerCharacters.ClosedPlace`, motif `SkillNotLearned` jugé par la partie, les autres par le validateur des builds ; `SlotLeftEmpty` n'est plus rendu). Une action qui ne change rien à la barre de la prochaine halte n'écrit rien et ne dit rien (une compétence gagnée en route reste annoncée à la halte avec sa touche). La barre admise est gardée dans la sauvegarde (format inchangé : `activeSkills`, `ultimate`, `passiveSkills`) et prend effet à la prochaine entrée dans une zone, comme les talents (`WorldSession.SetSkills`). L'écran (`AdventureSession.Skills`, `SkillBook`) montre la barre de la prochaine halte (`WorldSession.NextEntry` : une compétence gagnée en route y est déjà dans sa place), les places ouvertes et fermées avec leur niveau, les compétences apprises (avec leur place, ou en réserve) et celles à venir, décrites par les textes `text.skill.…`. `AdventureSession.PlaceSkill` met une compétence dans une place (deux compétences de la barre s'échangent ; une compétence de la réserve remplace celle de la place ; vers une place vide, elle va à la fin, les places se remplissant dans l'ordre), `RemoveSkill` vide une place.
  - Limites : depuis la réponse de Damien du 30 octobre 2026, la réserve a des actives (Provocation dès le niveau 3, Contrecoup dès le niveau 7 : 4 actives apprises pour 3 places au niveau 5, 6 pour 4 au niveau 10) ; une seule passive (Endurance du roc) pour 2 places passives à partir du niveau 5 : l'écran montre la 2e « Rien à placer pour l'instant » (les autres passives s'apprendront plus tard). Un changement de barre après un niveau gagné en route range déjà la compétence gagnée, que la halte n'annonce alors plus. Les 5e et 6e places actives (niveaux 15 et 20, proposition du plan) ne sont pas encore là : le pont temps réel et le protocole du réseau réservent l'emplacement 4 à l'ultime, et la barre de combat, les touches et les boutons tactiles ont quatre places ; elles viendront avec les niveaux au-delà de 10 (V1.8).
- **Partie en solo** (`WorldSession`) : reprend la sauvegarde, ouvre la session de zone suivante au temps du monde sauvegardé, y fait entrer le personnage, et lui donne l'expérience de chaque créature vaincue **pour la première fois** (registre des événements consommés) pendant qu'il est présent et vivant. En solo, il est seul à pouvoir l'avoir vaincue ; le partage en groupe viendra avec le serveur (lot 13).
- **Niveau gagné** : il prend effet à la **prochaine entrée** dans une zone (le combat en cours garde le build d'entrée, comme le reste du moteur qui ne change pas un build en route). Les points de talent du nouveau niveau se dépensent hors combat : depuis l'essai réaliste 6, l'arbre des Éclats (T) les place dans l'aventure, jugés par le validateur des builds au niveau actuel du personnage, gardés dans la sauvegarde et pris en compte à la prochaine halte, comme l'équipement (`AdventureSession.SetTalents`, refusé tant qu'un ennemi est à moins de 12 m).

## Objets, équipement, inventaire (sous-lot 12.3)

Catalogue en données (`data/v0/world/items.json`, `kind` « items », schéma `data/schemas/items.schema.json`, validé par `ItemValidator`). Valeurs **provisoires** :

- **Emplacements** (cadrage Q4, tranche : quatre dont le bouclier) : `weapon` (arme), `shield` (bouclier), `chest` (torse), `neck` (pendentif). La liste est dans les données ; le jeu final en prévoit douze (recommandation).
- **Inventaire** (Q5) : **24 places** ; une place tient une pile d'un même objet. Un équipement ne s'empile pas ; un matériau s'empile jusqu'à sa pile (20 ou 50 dans la tranche).
- **Objets** : identifiant `item.…`, nom, type (`equipment` ou `material`), **rareté** à six rangs (Q30 : `common`, `rare`, `very_rare`, `epic`, `legendary`, `mythic` ; « Secret » est une origine, pas un rang). Un équipement a son emplacement, son **niveau requis** et son **bonus** (puissances, armure, résistance magique, PV max, endurance max, résistance aux altérations ; au plus 50 000 par objet). Les quatre notions de H5 restent distinctes : niveau requis, puissance, rareté et adéquation au build (deux boucliers de niveau 5, l'un pour les PV, l'autre pour la résistance aux altérations).
- Tranche : 10 équipements (3 armes, 3 boucliers, 2 torses, 2 pendentifs, niveaux 1 à 8) et 5 matériaux (peau de loup, défense de sanglier, flèche brisée, minerai de fer, éclat de Seuil).

**Inventaire et équipement** (`Inventory`) : chaque opération est **atomique** (refusée, elle ne change rien, avec son motif) :

- `Add` range une quantité **en entier** : piles entamées du même objet d'abord, puis nouvelles places ; sinon `Full`. `Remove` retire en entier ou rien.
- `Equip(place, niveau)` porte l'équipement de cette place si le niveau suffit ; l'objet porté avant prend sa place (échange, sans place de plus). `Unequip(emplacement)` le range s'il reste une place.
- `Gear()` : somme des bonus des objets portés, comptée par le moteur à l'entrée du personnage dans une zone ([MOTEUR.md](MOTEUR.md#bonus-déquipement-sous-lot-123)). Un changement d'équipement prend effet à la prochaine entrée.

**Butin** : tables `loot.…` du catalogue (un à seize objets, chacun avec sa quantité et sa chance en pour cent). La partie en solo reçoit la table de chaque créature de la zone (par identifiant de combattant ; les Vallons en données, 12.4, la donneront). Une créature vaincue **pour la première fois**, le personnage présent et vivant, donne le butin de sa table, **tiré par la clé de sa mort** (`Loot.Roll` : générateur PCG initialisé par l'empreinte FNV-1a de la clé) : le même événement donne toujours le même butin, et le registre des événements consommés empêche de l'attribuer deux fois. Chaque objet tombé est rangé en entier, ou **perdu** si l'inventaire n'a plus de place (`LootGained.Kept` faux).

## Les Vallons en données (sous-lot 12.4)

Une **zone du monde** est un fichier de données (`data/v0/world/zones/vallons.json`, `kind` « zone », schéma `data/schemas/zone.schema.json`, validé par `ZoneValidator` contre son scénario et le catalogue des objets). Valeurs **provisoires** :

- **Simulation** : le scénario de zone `scenarios/zones/vallons.json`, agrandi à 84 × 58 m (de [−24, −18] à [60, 40]) au 12.9, puis à **240 × 170 m** (de [−100, −70] à [140, 100]) le 28 octobre 2026 (« Vallons agrandis », ci-dessous) : le village et le moulin à l'ouest de la rivière (pont), la haie du nord percée au moulin par une porte, le pré des loups au-delà, les ruines au centre, la carrière à l'est (blocs, front de taille), la petite zone du Seuil au nord-est (pierres dressées). Depuis l'agrandissement, la rivière court jusqu'au bord nord de l'aire et la haie jusqu'au bord ouest : le pré et la forêt des Loups ne s'atteignent plus que par la porte du moulin. Vingt-sept créatures (détail ci-dessous).
- **Points sûrs** : place du village (entrée), camp de la carrière ; place du bourg, ferme des champs, hameau de la carrière (Vallons agrandis). Le personnage entre au point d'entrée ; le retour après une défaite viendra avec le 12.7. Chaque point sûr doit pouvoir recevoir l'emprise d'un personnage (0,5 m) : entière dans l'aire praticable, hors de tout obstacle (porte du moulin fermée comprise), sans chevaucher la place d'une créature de départ qui lui serait solide.
- **Lieux nommés** (rectangles) : village, moulin, rivière, pré des loups, ruines, carrière, petite zone du Seuil, forge de Maëlle (12.9a, à l'ouest du village, juste au nord de la place, de [−23, −7] à [−17, −1]) ; depuis l'agrandissement : bourg d'Aube, champs du sud, lac des Aubes, vergers, hameau de la carrière, plaine de l'est, hautes pierres, tour de guet, camp des rôdeurs, forêt des Loups (`PlacesAt(point)`), pour les découvertes et les quêtes (12.5). La « manifestation » de la mission aura lieu dans la petite zone du Seuil.
- **Populations** : chaque créature du scénario, avec son délai de réapparition (loups 90 s, sangliers et archer 2 min, rôdeur 3 min), sa laisse (18 à 20 m) et sa table de butin.
- **Équilibrage de l'essai** (25 octobre 2026, 12.9a : les créatures venaient de la démonstration du lot 11, faite pour un Bastion de niveau 10) : loups 150 PV et 10 dégâts, sangliers 180 PV et 25, archer 120 PV et 14 ou 16, rôdeur 220 PV et 18 ou 24. Les loups courent à 4 m/s, moins vite que le Bastion (5 m/s) : on peut les semer (12.9c, retour de Damien : « le loup est trop rapide, je ne peux pas le fuir » ; 6 m/s auparavant). Un Bastion de niveau 1 bat un loup, un sanglier ou l'archer seul ; il faut le niveau 5 pour le rôdeur, et des potions pour le Gardien même au niveau 10 (`Lot129BalanceTests`, combats simulés).
- **Passage du moulin** : la porte `porte_moulin` ferme la haie ; `OpenPassage("moulin")` l'ouvre **pour de bon** (clé `zone.vallons/passage/moulin` au registre du monde, gardé dans la sauvegarde). L'obstacle disparaît à la **session suivante** (`ZoneSetups`).

**Vallons agrandis** (demande de Damien du 28 octobre 2026 : « un grand agrandissement, on pourrait mettre des bâtiments, des routes ou pleins d'autres choses », puis « fais le et on verra après ») ; valeurs provisoires :

- **Aire** : 240 × 170 m, plus de huit fois l'ancienne ; 59 obstacles (plafond : 64). Le village, le moulin, la carrière et le Seuil n'ont pas bougé : la mission se joue comme avant.
- **Bourg d'Aube** (ouest) : huit maisons, l'auberge, la chapelle, une place pavée avec son puits et deux étals ; grand-rue de terre jusqu'au bord ouest. **Champs du sud** : grange, ferme et champs cultivés. **Lac des Aubes** au sud de la rivière (la rivière s'y jette). **Hameau de la carrière** à l'est, **tour de guet** au nord, **camp des rôdeurs** au nord-est (palissades, tentes), grands **bois** (forêt des Loups au nord-ouest, bois du nord-est et du sud-est).
- **Routes** (zone de schéma 3, `data.roads`, format dans [DONNEES.md](DONNEES.md)) : neuf chemins de terre et la place pavée relient le village au bourg, aux champs, au lac, au hameau, à la tour et au camp ; le chemin de la tour passe à l'ouest du Seuil (loin du rôdeur de la mission). Les routes sont du **décor** : on y marche comme ailleurs, le moteur ne les connaît pas. Aucune ne passe dans un obstacle (toute sa largeur, sauf la porte du moulin, le puits et les étals de la place) ; aucune créature de départ n'attend à moins de 12 m d'une halte ni à portée d'engagement (`VallonsAgrandisTests`).
- **Créatures** : dix-sept de plus, mêmes forces que les anciennes : sept loups (forêt des Loups, hautes pierres), cinq sangliers (champs, vergers, plaine), trois archers (tour de guet, camp) et deux rôdeurs (camp). Vingt-sept en tout. **Plafond** : une session ouvre avec 28 combattants (le personnage compris) et garde deux places pour le chef ; les morts et les créatures qui lâchent restent comptés, donc **98 réapparitions par session** depuis que le plafond est passé de 64 à 128 combattants (décision de Damien du 29 octobre 2026, « relever le plafond » ; 34 avec 64, 51 avant l'agrandissement) ; au-delà, les populations attendent la halte suivante (`Exhausted`).
- **Nœuds de récolte** : herbes de la forêt, du lac et du verger ; filons des hautes pierres et de la tour (onze en tout).
- **Affichage** (`TownRules`, `TownShapes`, `MapPainter`, sans Unity, testés par `VallonsAgrandisTests`) :
  - ce que représente un obstacle (maison, auberge, chapelle, grange, puits, étal, tour, tente, bois), d'après le début de son identifiant ;
  - bâtiments faits par le code sur l'emprise de l'obstacle : sous 2 m, seuls les reliefs (appuis, colombages, volets, 12 cm au plus) et les marches devant les portes (34 cm, sous 50 cm de haut) en sortent ; plus haut, les débords de toit, l'enseigne et l'auvent ; porte vers la route la plus proche ;
  - bois pleins d'arbres, champs (blé mûr ou vert, labours) en dehors des routes, des obstacles et des haltes ;
  - terre battue sur les chemins, pavés sur la place ;
  - mêmes règles pour la carte peinte et le décor de Unity (couvertures, cheminées et clocher compris) ; la carte peinte n'est repeinte que si ce qu'elle montre change (une porte ouverte), pas à chaque halte.

**Vie des populations** (`WorldSession` sur une zone du monde) :

- **Réapparition** (section 2 de l'architecture : une identité n'est jamais réutilisée) : une créature morte revient après son délai, à sa place, **sous une nouvelle identité** (`loup1_v2`, `loup1_v3`… ; une identité déjà portée par un autre combattant de la session est sautée), calme et soignée. Sa mort garde sa clé unique : expérience et butin une seule fois par vie. Une place occupée retarde l'arrivée d'une seconde.
- **Laisse** (essai du 22 octobre 2026) : une créature engagée dont l'écart à sa cible, entre les bords des emprises, dépasse sa laisse **lâche** : elle quitte la zone (ni mort, ni expérience, ni butin) et une vie calme et soignée la remplace à sa place au tick suivant.
- **Refus du moteur** : un départ (laisse) ou une arrivée (réapparition) refusé par le moteur (plafond de commandes, file pleine) ne fait pas échouer le tick : il est retenté au tick suivant (la laisse) ou une seconde plus tard (la réapparition). Les récompenses du tick (expérience, butin) sont attribuées avant la vie des populations.
- **Plafond** : une session tient au plus 128 combattants (règles ; 64 jusqu'au 29 octobre 2026) ; une population qui n'a plus de place attend la session suivante (`Exhausted`).

## PNJ, dialogues et quêtes (sous-lot 12.5)

L'histoire de la tranche est en données : `data/v0/world/story.json` (`kind` « story », schéma `story.schema.json`, validé par `StoryValidator` contre les textes, la zone et le catalogue des objets) et les **textes par clés** `data/v0/world/texts/fr.json` (`kind` « texts », cadrage Q37 : français d'abord, anglais ensuite sans toucher au reste). Aucun texte affiché n'est écrit dans le code ni dans l'histoire : seulement des clés `text.…`.

- **PNJ** : identifiant `npc.…`, nom (clé), position dans la zone, dialogue. On leur parle à **3 m** au plus (provisoire), présent et vivant.
- **Dialogues** : des nœuds (texte, réponses) et des **entrées** : la première entrée dont les conditions sont réunies donne le nœud de départ. Chaque réponse a ses conditions (elle n'est proposée que si elles sont réunies), ses **effets** et son nœud suivant (ou la fin).
- **Conditions** (H3) : acquis durables (`fact`, `notFact`, état d'une quête : `questNotStarted`, `questActive`, `questAt` une étape, `questDone` ; confiance d'un PNJ : `trustAtLeast`, `trustBelow`) et circonstances temporaires (`hasItem`, moment de la journée `phase`, lieu `inPlace`, `passageOpen`).
- **Effets** : `setFact`, `trust` (H1 : les PNJ se souviennent ; de −100 à 100), `startQuest`, `giveItem`, `takeItem`, `giveXp`, `openPassage`, `discover` (journal des découvertes). Les effets d'une réponse s'appliquent **en entier ou pas du tout** : des objets qui ne tiennent pas ou qui manquent, ou un passage à ouvrir sans place au registre du monde, laissent tout inchangé (`NoRoom`). Une étape se franchit au tick suivant : une réponse récompensée porte donc sa propre garde (`notFact` sur le fait qu'elle apprend), pour ne pas se répéter d'ici là (amulette d'Odile, ferrure de Maëlle, merci de Gaspard).
- **Quêtes** : des étapes successives, chacune avec un texte de journal, un **objectif** vérifié à chaque tick (`fact` appris, lieu atteint `reach`, créatures vaincues `defeat` pendant l'étape, objets détenus `collect`, `passage` ouvert) et ses effets ; la quête a ses récompenses finales. Des récompenses qui ne tiennent pas dans l'inventaire font attendre l'étape (`RewardBlocked`, annoncé une fois).
- **Portée** (H2) : tout l'état narratif est **personnel** (faits, quêtes, confiance, découvertes), gardé avec le personnage. Les expéditions de groupe et les chapitres publics viendront avec le monde partagé (lot 13).

**Mission « Les traces sous le moulin »** (Q2), valeurs et textes provisoires :

1. **Enquêter** : Odile, la meunière, raconte les traces. Deux réponses : aider (confiance +1) ou la soupçonner (confiance −1) — la mission commence dans les deux cas.
2. **Suivre les traces** jusqu'à la porte du moulin (lieu « moulin ») ; découverte des traces griffues.
3. **Rouvrir le passage** : récolter trois minerais de fer dans la carrière, forger une ferrure à la forge de Maëlle (12.6, forge visible depuis le 12.9a) ; Maëlle la pose et le passage du moulin s'ouvre pour de bon. Si Odile a confiance, Maëlle donne un indice sur le minerai.
4. **Le pré des loups** : passer la porte, découvrir les éclats.
5. **La manifestation** : atteindre la petite zone du Seuil ; découverte de la manifestation.
6. **Le rôdeur du Seuil** : le vaincre.
7. **Le Gardien du Seuil** (12.7) : vaincre le chef de fin, sous ses deux formes ; découverte du Gardien.
8. **Revenir chez Odile** : si elle a confiance, elle offre l'**amulette de l'aube** ; sinon, un merci sec (conséquence observable liée à un PNJ, Q2). Récompense finale : 300 points d'expérience.

Gaspard, le vieux garde du camp de la carrière, conseille contre le rôdeur pendant l'étape 6.

## Soin et métiers (sous-lot 12.6)

**Objets de soin** (catalogue des objets en version 2 : type `consumable`, `heal`, `cooldownMs`, `stack`) : potion de soin (300 PV, pile de 5) et remède de l'aube (700 PV, pile de 3), recharge partagée de 20 s. À l'entrée dans une zone, le personnage emporte **au plus une pile** de chaque objet de soin de son inventaire ([MOTEUR.md](MOTEUR.md#soin-sous-lot-126)). `Drink(objet)` le fait boire ; le moteur peut refuser (PV pleins, recharge, étourdi). Un objet bu quitte l'inventaire. Les objets emportés sont **réservés** (`Inventory.Reserved`, `Available`) : ils restent dans l'inventaire et la sauvegarde, mais aucune autre opération (recette, réponse de dialogue, retrait) ne peut les prendre avant qu'ils soient bus. Le pilote refuse à l'admission un soin qui nomme un objet non emporté.

**Métiers** (`data/v0/world/crafting.json`, `kind` « crafting », validé par `CraftingValidator` contre les textes, la zone et le catalogue) : forge, herboristerie, minage, chacun avec **sa propre expérience** et ses niveaux 1 à 10 (provisoire : 40, 100, 180… points). Combattre ne forme aucun métier ; récolter et fabriquer ne donnent jamais d'expérience de combat.

- **Récolte** (`Gather`) : six nœuds dans les Vallons (trois filons de fer dans la carrière, trois touffes d'herbe de l'aube au village, près de la rivière et dans le pré). À 3 m au plus ; un nœud récolté repousse au bout d'une minute (dans la session) ; l'objet doit tenir dans l'inventaire.
- **Fabrication** (`Craft`) : à l'atelier de la recette (un lieu de la zone : la **forge de Maëlle**, à l'ouest du village, juste au nord de la place, marquée d'une enclume dans Unity ; le village entier jusqu'au 12.9a), au niveau de métier requis, les entrées présentes et le produit qui tient — sinon rien ne change. Recettes : ferrure (3 minerais de fer, forge), potion de soin (2 herbes, herboristerie), remède de l'aube (3 herbes et un éclat de Seuil, herboristerie niveau 3). **Une fabrication triviale forme moins** : moitié de l'expérience à 2 niveaux d'écart, quart à 4.
- **Mission** : la porte du moulin s'ouvre avec une **ferrure forgée par le joueur** (récolte du minerai, fabrication à la forge de Maëlle), que Maëlle pose. Le minerai tombe encore parfois des sangliers (25 %).

## Chef de fin et récompenses (sous-lot 12.7)

Le chef d'une zone est décrit dans la zone (schéma 2, bloc `boss`) ; ses formes sont des assaillants d'un scénario à part (`scenarios/zones/vallons_chef.json`), qui n'a que des combattants :

```json
"boss": {
  "id": "gardien_du_seuil",
  "scenario": "zones/vallons_chef",
  "forms": ["gardien_du_seuil", "gardien_eveille"],
  "fact": "fact.chef.vaincu",
  "rewards": [{"item": "item.lame_du_seuil", "count": 1}],
  "loot": "loot.gardien"
}
```

- **Formes successives** (boss à phases) : la première forme arrive avec la session, au cœur des pierres dressées. Quand elle tombe, la forme suivante rejoint le combat à sa place prévue (nouvel essai chaque seconde si la place est occupée). Les Vallons : le **Gardien du Seuil** (250 PV, griffe, onde du Seuil annoncée), puis le **Gardien éveillé** (400 PV, plus fort et plus rapide, ruée en ligne annoncée) — valeurs de l'essai du 25 octobre 2026 (12.9a). Chaque forme vaincue donne son expérience ; le butin (`loot.gardien` : deux éclats de Seuil, parfois le talisman) suit la chute de la dernière forme, **à chaque victoire**.
- **Première victoire récompensée une seule fois** (cadrage H4, plan : « récompenses attribuées une fois ») : la victoire est une clé du registre du monde, `zone.vallons/boss/gardien_du_seuil`. La récompense (la **lame du Seuil**, arme épique de niveau 8) et le fait `fact.chef.vaincu` sont donnés **ensemble ou pas du tout**, à un personnage présent et vivant, en consommant la clé. Une sauvegarde relue, une nouvelle session ou une seconde victoire ne la redonnent pas. Un inventaire plein fait attendre la récompense (`RewardBlocked`, annoncé une fois) jusqu'à ce qu'une place se libère dans la session.
- **Contrôles** : la place de chaque forme tient dans la simulation de la zone (aire, obstacles) et n'occupe aucun point sûr ; la première ne chevauche pas une créature de départ ; un personnage ne peut pas porter l'identité d'une forme. La zone, le personnage et toutes les formes tiennent sous le plafond des combattants. Les populations gardent une place de combattant à chaque forme encore à venir ; une forme attend une place prise, même par une créature qui arrive au même tick, et une arrivée refusée malgré tout est retentée sous une nouvelle identité (`{forme}_v2`) ; la victoire compte même au tick qui termine la session ; un registre du monde plein fait attendre la récompense comme un inventaire plein.
- **Défaite sans perte** (cadrage Q8) : quand le personnage tombe, la session le signale (`Defeated`, `IsDefeated`). Rien d'acquis n'est perdu : expérience, niveaux, objets, équipement, métiers, faits, quêtes, confiance et découvertes restent tels quels dans la sauvegarde ; seuls les objets de soin bus manquent. La partie reprend à la session suivante, au point sûr d'entrée, PV pleins ; le chef y est de nouveau entier.
- **Mission** : l'étape « chef » (vaincre le Gardien : objectif `fact` sur `fact.chef.vaincu`) s'insère entre le rôdeur et le retour chez Odile.

## L'aventure jouable (sous-lot 12.8)

`AdventureSession` (`EdS.Runtime`) fait jouer la zone en temps réel par Unity : elle charge le monde (`data/v0/world/…`, `scenarios/zones/…`), reprend la sauvegarde locale (absente : nouveau Bastion ; illisible : nouvelle partie, motif affiché), ouvre une session (`WorldSession`) et la joue par le pont temps réel (`RealtimeCombat`, ici sur le moteur de la session). Détail des touches : [UNITY.md](UNITY.md#4-bis-laventure-dans-les-vallons-daube-sous-lot-128).

- **Messages par clés** (cadrage Q37) : ce que la session rapporte (expérience, niveau, butin, quêtes, découvertes, confiance, chef, récompense, défaite, récolte, fabrication, refus) devient un texte de `texts/fr.json` (clés `text.ui.…`) ; les écrans aussi (titres, boutons, aide). Seuls les noms d'objets et de lieux restent dans leurs fichiers de données.
- **Actions hors combat** : parler au PNJ à portée, sinon récolter le nœud à portée ; dialogue (réponses traduites) ; fabriquer (l'atelier montre le refus qu'aurait chaque recette, sans rien changer) ; porter ou retirer un objet ; boire l'objet de soin emporté qui soigne le moins.
- **Cible parmi les proches** : dans une zone, les ennemis en vue à 20 m au plus, du plus proche au plus loin (provisoire) ; une cible qui sort de cette portée compte comme perdue de vue.
- **Halte** : à 5 m d'un point sûr, sans ennemi vivant à 12 m (provisoire) : sauvegarde, puis nouvelle session à ce point sûr (`WorldSession` reçoit le point sûr d'entrée). Niveau, équipement et passages ouverts y prennent effet, PV pleins.
- **Défaite** (Q8) : plus rien n'avance jusqu'à la reprise, qui sauvegarde et rouvre une session au dernier point sûr approché (l'entrée au lancement). Une fin de session (une heure), ou une arrivée refusée (une créature sur le point sûr), se reprend de même. Une sauvegarde automatique impossible (disque plein) s'affiche et se réessaie 5 s plus tard, sans couper la partie.
- **Sauvegarde automatique** : au plus toutes les 5 s de temps réel quand quelque chose a changé, à chaque halte ou reprise, en quittant ou en passant en arrière-plan, y compris après une défaite pas encore reprise (les acquis d'avant la chute ne se perdent pas ; les PV du combat ne sont jamais sauvegardés).

## Intérieurs

Choix de Damien du 28 octobre 2026 (« on fait la façon 1 ») : on entre dans une maison par sa porte, qui mène à un **intérieur séparé**, une petite zone à part (comme les maisons de Skyrim), et non dans le bâtiment du village ouvert sur place. C'est la règle de [ARCHITECTURE_MONDE.md](ARCHITECTURE_MONDE.md) §7 : passer d'une zone à l'autre, c'est quitter une simulation et entrer dans l'autre avec l'état persistant.

- **Données** (zone en schéma 4, [DONNEES.md](DONNEES.md#versions-de-format)) : une pièce est une zone `indoor: true` (son aire est le sol, ses bords les murs, ses obstacles les meubles ; son scénario n'a aucun combattant). Les portes (`doors[]`) vont par paires : `to` nomme l'autre zone, `toDoor` la porte d'en face, qui doit revenir ici ; `building` relie une porte des Vallons à l'obstacle de son bâtiment (à 2 m au plus), pour que la façade du modèle 3D se tourne vers elle. Livrés : l'**Auberge de l'Aube** (14 × 10 m), la **Chapelle du Bourg** (9 × 16 m) et la **Maison du Bourg** (8 × 6 m), chacune avec un point sûr et une porte « Bourg d'Aube ».
- **Chargement** : `ZoneJsonReader.ReadLinked` lit la zone de départ puis, de porte en porte, toutes les zones reliées (au plus 64) ; une zone introuvable, un fichier qui décrit une autre zone, ou deux portes qui ne se répondent pas refusent le monde. Le rapport est celui du fichier en cause : la porte qui mène à une zone introuvable, ou la première zone dont une porte ne trouve pas sa réponse (`ZoneValidator.CheckDoors`, `CheckDoorsOf`) ; une forme du chef ne peut pas être posée sur une porte (on y arrive comme à un point sûr).
- **Passer une porte** (F à 2 m d'elle, quand aucun PNJ ni nœud de récolte n'est à portée) : même règle que la halte, aucun ennemi vivant à 12 m et pas de dialogue ouvert. La partie est sauvegardée dans la zone d'arrivée, puis une session de cette zone s'ouvre **à la porte d'en face** ; le dernier point sûr devient celui de la zone d'arrivée le plus proche de cette porte. Une sauvegarde impossible annule le passage. Une partie reprise l'est dans la zone sauvegardée, à son entrée (une zone inconnue ramène aux Vallons).
- **Ce qui reste dehors** : l'histoire et les métiers sont ceux des Vallons (`WorldSession` reçoit la zone de l'histoire) : dans une pièce, ni PNJ qui parle, ni récolte, ni atelier ; la carte montre la pièce, sa porte, et l'objectif « sortir ».
- **Rendu** (`InteriorRules.Layout`, déterministe : mêmes données, même pièce) : le genre de la pièce vient de son lieu (auberge, chapelle, sinon maison). Chaque meuble est reconnu au début de son identifiant (`comptoir`, `table`, `banc`, `cheminee`, `lit`, `etagere`, `caisse`, `armoire`, `rouet`, `statue`…) et garni : bancs ou tabourets, bougie, vaisselle, bouteilles ; étagères garnies ; caisses empilées ; lit, coffre, rouet et panier ; autel avec chandeliers et siège du prêtre. Aux murs : boucliers, épées, tableaux, lanternes ; au plafond : lanternes ou lustres et poutres ; des fenêtres sur les murs libres (hautes et étroites dans la chapelle) ; au plus 7 lumières (feu, bougies, lanternes) qui vacillent ; des villageois (aubergiste et deux clients, une fidèle, l'habitant). Les objets sont les modèles de Poly Haven rangés dans `PolyHaven/Interieurs` ([CREDITS.md](CREDITS.md)), sinon des formes simples faites par le code.

Dans une pièce, ni oiseaux ni rivière, le vent du dehors à peine ; la carte est le plan de la pièce (plancher, meubles, murs, porte).

Limites : une pièce n'a qu'un niveau (l'escalier de l'auberge est un décor) ; les portes ne se ferment pas à clé ; les PNJ des intérieurs sont du décor, sans dialogue. Passer une porte ouvre une nouvelle session, donc agit comme une halte (PV pleins, niveau et équipement appliqués) sans être près d'un point sûr : question posée à Damien. Ressortir reconstruit tout le décor des Vallons (un à-coup possible sur le téléphone).

## Monnaie, marchands et voyage rapide (sous-lot V1.1)

Premier sous-lot du lot « Vallons solo finis » ([PLAN_VALLONS_SOLO.md](PLAN_VALLONS_SOLO.md#v11--monnaie-marchands-voyage-rapide-m)). Valeurs **provisoires**.

**Monnaie** (choix de Damien du 29 octobre 2026, 12 h 57) : trois pièces, **cuivre, argent et or** ; 100 cuivre font 1 argent, 100 argent font 1 or. La bourse est gardée avec le personnage (`SavedCharacter.Money`, sauvegarde en version 6) comme **un seul nombre de pièces de cuivre**, de 0 à 9 999 999 999 (999 999 or 99 argent 99 cuivre, `Coins.Max`). Toute somme passe par `Coins.TryAdd` et l'arithmétique `checked` : un dépassement (bourse vide ou pleine) est **refusé**, jamais tronqué. Affichage par les textes du monde : « 3 or 45 argent 12 cuivre » (pièces non nulles, de l'or au cuivre ; « 0 cuivre » pour une bourse vide). On gagne des pièces par les **quêtes** (effet `giveMoney` de l'histoire, en schéma 2) et par la **vente** ; les contrats de la guilde (V1.7) en donneront aussi. Jamais par l'argent réel (sujet 6). Mission d'Odile : 50 cuivre quand la porte du moulin s'ouvre, 1 argent 50 cuivre pour le rôdeur, 5 argent à la fin (en plus de l'expérience). La bourse est bornée dès la construction du personnage (`SavedCharacter`, `WithMoney` : `ArgumentOutOfRangeException` hors bornes, comme le validateur des sauvegardes) : une partie ne peut pas en porter une hors bornes, et son affichage ne peut pas échouer. Des pièces qui ne tiendraient pas dans la bourse font attendre la récompense, comme un inventaire plein, avec **leur propre message** (événement `PurseFull`, « Bourse pleine : dépense des pièces pour recevoir ta récompense. ») ; dans un dialogue, la réponse est refusée sans rien changer (`DialogueRefusal.PurseFull`, « Ta bourse est pleine : ces pièces n'y tiendraient pas. »). Un inventaire plein garde `RewardBlocked` et `NoRoom`.

**Marchands** (`data/v0/world/merchants.json`, `kind` « merchants », schéma 1, `data/schemas/merchants.schema.json`, validé par `MerchantValidator` contre les textes, l'histoire et le catalogue des objets) :

- `values` : **valeur de base** de chaque objet qui s'achète ou se vend, en cuivre. Un objet sans valeur ne se vend pas (la ferrure, objet de la mission).
- `merchants` : pour chacun, son **PNJ** (un PNJ de l'histoire : on lui parle pour ouvrir la boutique), son **accueil** (clé de texte), la **part de la valeur de base payée à la revente** (`sellPercent`, arrondie au cuivre inférieur, au moins 1 cuivre), ses **heures de fermeture** (`closed` : moments de la journée ; réponse de Damien du 30 octobre 2026 : Garin et Bertille ferment la nuit, de 22 h à 5 h, Sauge jamais), ses **offres** (objet, prix d'une unité, `always` : toujours en vente, sinon certains jours) et le nombre d'offres tirées chaque jour (`daily`).
- **Contrat de l'économie**, vérifié par le validateur : aucune offre sous la valeur de base de son objet, et une part de revente d'au plus 100 % : acheter puis revendre ne fait jamais gagner d'argent.
- Au **bourg d'Aube**, autour de la place : **Garin l'armurier** (au sud, devant la maison de la place ; armes, écus, gambison toujours ; épée de garde, rondache, pavois, cotte de mailles, marteau du carrier, deux par jour ; rachète à 25 % ; **fermé la nuit**), **Sauge l'herboriste** (à côté de l'étal du sud ; **potions de soin** et herbe de l'aube toujours ; remède de l'aube ou éclat de Seuil, un par jour ; 25 % ; **ouverte à toute heure**) et **Bertille l'épicière** (à côté de l'étal du nord ; potions de soin, un peu plus chères, toujours ; herbe de l'aube ou éclat de Seuil, un par jour ; rachète un peu mieux, 30 % ; **fermée la nuit**).
- **Le minerai de fer ne s'achète pas** (réponse de Damien du 30 octobre 2026 : Bertille ne le vend plus) : la ferrure du moulin demande d'aller miner. Il garde sa valeur de base et se revend.
- **Stock du jour** (`Market.Stock`) : les offres permanentes, plus `daily` offres tirées parmi les autres par un `Pcg32` initialisé par l'empreinte FNV-1a de la **clé du jour** (`merchant.armurier/day/3`, jour du monde tiré des ticks, jamais de l'horloge réelle) ; le même jour donne toujours le même stock, dans l'ordre du fichier. Le stock tournant poussé (quantités, arrivages) vient avec la vague 2.

**Achat, vente, rachat** (`WorldSession.Buy`, `Sell`, `BuyBack`) : opérations **atomiques** (la bourse et l'inventaire changent ensemble, essayés d'abord sur une copie de l'inventaire, ou rien ne change) ; un refus rend son motif (`TradeRefusal`) :

- boutique **trop loin** (à plus de 3 m du PNJ, ou personnage absent, tombé, dans un intérieur) ou **fermée** à ce moment de la journée ;
- **objet inconnu** du catalogue, **pas en vente** aujourd'hui, **sans valeur** (il ne se vend pas), quantité impossible (moins de 1 ou plus de 999, vente vide) ;
- **or insuffisant**, **inventaire plein**, **bourse pleine** (une vente qui la ferait déborder) ;
- **objet porté** (l'équipement se retire d'abord) ou **réservé** (objets de soin emportés au combat, jusqu'à ce qu'ils soient bus) ; pas assez d'objets.

Dans l'aventure, **parler à un marchand** (F) ouvre sa boutique ; **fermée** à ce moment de la journée, il le dit et son dialogue de l'histoire s'ouvre à la place (il reste un PNJ comme les autres). Vendre **une place du sac** (`AdventureSession.SellPlace`, clic ou clic droit) ne vend que la **part libre** de cet objet : des potions emportées au combat (réservées) ne bloquent plus la vente de celles achetées ou ramassées depuis ; le refus (`Reserved`) ne vient que si rien n'est libre. Les objets d'un même identifiant sont identiques : la vente retire le nombre exact, des dernières places (comme tout retrait). Toute nouvelle session (halte, porte, voyage, reprise) ferme la boutique.

Une vente peut porter sur **plusieurs lots d'un coup** (`Sell(marchand, lots)`, tout ou rien) : la vente rapide des objets moins bons (plus tard) s'y appuiera. Chaque lot vendu devient **rachetable au même prix** (`Market.Buyback`, le plus récent d'abord, 12 au plus) tant que dure la session de jeu : la même instance de `Market` passe d'une session de zone à l'autre (halte, porte, voyage, reprise) ; quitter le jeu les oublie (ils ne sont pas sauvegardés).

**Voyage rapide** (`WorldSession.CanTravel`, `AdventureSession.Travel`) :

- un **point sûr** d'une zone du dehors est **découvert** quand le personnage, présent et vivant, s'en approche à 5 m (la distance de la halte) ; celui où commence une session (entrée, halte, reprise, arrivée d'un voyage) l'est d'emblée, sans annonce. La découverte est une clé du **registre du monde** (`zone.vallons/safe/place_du_bourg`, `ZoneDefinition.SafePointKey`), gardé dans la sauvegarde comme les passages ouverts. Elle attend si le registre n'a plus la place des morts possibles d'un tick ; à l'ouverture d'une session, elle se fait après l'arrivée du chef, dont la première forme compte donc parmi ces morts possibles ;
- on voyage vers un point sûr découvert, **hors combat** (`WorldSession.InCombat`, la règle de la halte et des talents) : aucun ennemi vivant à moins de 12 m, aucun ennemi **engagé qui vise le personnage**, même de plus loin ; pas de dialogue ouvert ni de reprise à faire (sinon `Unavailable`). Les intérieurs ne sont pas des destinations (on y entre par leur porte), mais on peut en partir ;
- pas de voyage vers le point sûr **où l'on est** (même zone, à 5 m : `AlreadyHere`, « Tu es déjà à ce point sûr : fais plutôt une halte. ») : le voyage n'est pas une halte gratuite. Ce refus vient en dernier (il ne concerne que cette destination) ; le refus de combat passe avant ;
- le voyage **reprend le chemin d'une porte** : la session d'arrivée est préparée, la partie est sauvegardée dans la zone d'arrivée, puis la session s'ouvre au point sûr (PV pleins ; niveau, équipement et talents pris en compte, comme à une halte) ; une sauvegarde impossible (disque plein) annule le voyage (`SaveFailed`) ;
- **gratuit** dans la vague 1 (le prix éventuel sera un réglage d'équilibrage).

Limites : les marchands n'ont ni quantités en stock ni prix qui varient ; les PNJ des marchands sont des capsules dorées tant qu'ils n'ont pas de personnage Mixamo (dossiers `Armurier`, `Herboriste`, `Epiciere`) ; les lots rachetables ne survivent pas à un redémarrage ; un point sûr se découvre en passant à 5 m, sans halte ; le voyage rend les PV pleins comme une halte (à confirmer par Damien : voir la PR #72) ; « en plein combat » a encore trois règles proches (`InCombat` du voyage, `CanUseDoor` des portes, à 12 m sans l'ennemi engagé qui vise de loin, et le verrou des compétences), à réunir plus tard.

## Équipement complet (sous-lot V1.2)

Deuxième sous-lot du lot « Vallons solo finis » ([PLAN_VALLONS_SOLO.md](PLAN_VALLONS_SOLO.md#v12--équipement-complet--places-boucliers-écran-l)). Valeurs **provisoires**.

**18 places** (choix de Damien du 29 octobre 2026, [CADRAGE.md](CADRAGE.md) Q4), décrites dans le catalogue (`items.json`, version 3), dans l'ordre de l'écran :

| Groupe | Places |
|---|---|
| combat (16) | `weapon` (arme), `shield` (bouclier, **porte la garde**), `head`, `shoulders`, `cloak` (cape), `chest`, `bracers`, `hands`, `belt`, `legs`, `feet`, `neck`, `ring_1` et `ring_2` (sorte `ring`), `relic_1` et `relic_2` (sorte `relic`) |
| Éclat du Seuil (`shard`) | l'Éclat du personnage, **toujours porté** (`required`) ; il grandira avec l'histoire. Objet de départ : l'Éclat du Seuil endormi, sans bonus pour l'instant, sans valeur (il ne se vend pas) |
| outil de métier (`tool`) | pioche du carrier, faucille de l'herboriste, marteau de forge : sans bonus de combat (refusé par le validateur) ; leur usage hors combat viendra avec les métiers |

- Un objet porte une **sorte** de place (`slot` : `weapon`, `ring`…) ; une place la reçoit (`accepts`, son identifiant par défaut). Deux places de même sorte ont le même groupe.
- **Places obligatoires** : `required` (toutes les classes : l'Éclat) ou `requiredBy` (certaines classes : le bouclier du Bastion, « il porte toujours un bouclier »). On y **échange** l'objet, on ne l'ôte pas (`InventoryRefusal.Required`).
- **Équipement de départ** (`starting`) : l'Éclat pour tous, l'épée du milicien et l'écu de chêne pour le Bastion, portés par un nouveau personnage (`Inventory.Outfit`). Le validateur vérifie qu'il remplit toutes les places obligatoires de chaque classe, au niveau 1, sans plus d'objets d'une sorte que de places.
- **Garde des boucliers** : chaque bouclier porte la sienne (`guard`), jugée contre `combat.shield` des règles ([DONNEES.md](DONNEES.md#garde-des-boucliers-sous-lot-v12)), donnée au moteur à l'entrée dans une zone (`Inventory.Shield`, [MOTEUR.md](MOTEUR.md#garde-du-bouclier-sous-lot-v12)). L'écu de chêne a exactement les valeurs des règles ; un bouclier léger (targe, rondaches : 0,5 ou 0,55, 6 ou 8 d'endurance, parade de 200 ms) rend la parade plus facile ; un pavois (0,7, 14 à 16, 100 ms) bloque plus et coûte plus.

**Catalogue** : 73 équipements des niveaux 1 à 20 (12 armes, 10 boucliers, 4 à 6 objets par pièce d'armure et par bijou, 6 anneaux, 4 reliques, l'Éclat, 3 outils), plus les 9 matériaux et objets de soin. Les **quatre notions de H5** restent distinctes : **niveau requis**, **puissance** (bonus et garde), **rareté**, **adéquation au build** (`fits` : arbres Rempart, Contrecoup ou Gardien, une indication sans effet). Chaque tranche de cinq niveaux a plusieurs choix de même rareté pour une même place (trois boucliers rares de niveau 5 : le pavois pour le Rempart, la rondache pour le Contrecoup, l'écu du garde pour le Gardien) ; la rareté n'est pas la puissance (la lame du Seuil, épique de niveau 8, bat moins fort que l'épée du capitaine, très rare de niveau 15). Les personnages ne dépassent pas encore le niveau 10 (V1.8) : les objets des niveaux 11 à 20 attendent. Les **effets** des armes et des bijoux, les **ensembles** et les **propriétés tirées au hasard** viennent avec V1.3 : ils seront des champs nouveaux d'un objet, dans une version suivante du catalogue, sans rien casser de la forme actuelle.

**Prix** (`merchants.json`, valeur de base en cuivre) : les anciennes valeurs gardées ; les nouvelles selon le niveau et la rareté (commun 150 + 100 par niveau, rare 600 + 250, très rare 2 000 + 400, épique 5 000 + 600, à la dizaine). **Garin l'armurier** vend en plus, toujours, la targe d'osier, la coiffe de cuir, les bottes de cuir et la cape de laine ; et, certains jours, l'écu du garde, le casque de fer, les jambières de mailles, la cape de voyageur, les gantelets de fer et l'anneau de fer (prix : valeur × 1,3). Bertille vend la pioche, Sauge la faucille.

**Inventaire et équipement** (`Inventory`, opérations **atomiques**, chacune avec son motif) :

- `Equip(place, niveau)` : à la **première place libre** de la sorte (un anneau au premier doigt libre), sinon à la première (échange) ; `EquipTo(place, emplacement, niveau)` : à une place choisie (`WrongSlot` si elle ne reçoit pas cette sorte) ; `Unequip(emplacement)` : refusé à une place obligatoire (`Required`) ou sac plein ; `Swap(a, b)` : échange deux places d'une même sorte (les anneaux, les reliques), refusé si une place obligatoire restait vide.
- **Hors combat seulement** : `WorldSession.Equip`, `EquipTo`, `Unequip` et `Swap` sont refusés en plein combat (`InCombat`) par la règle existante `WorldSession.InCombat` (celle de la halte, des talents et du voyage), sans nouvelle règle. L'effet vient à la prochaine entrée (halte, porte, voyage), comme avant.
- **Rangement** (`Sort`) : par sorte (équipement dans l'ordre des places, puis soins, puis matériaux), par rareté ou par niveau ; les piles entamées d'un même objet se regroupent ; quantités, réserves et équipement inchangés. **Filtres** (`Filter`, une vue) : tout, équipement, à ma portée (portable au niveau actuel), soins, matériaux. Le sac garde 24 places ; les sacs viendront plus tard.
- **Comparaison** (`ItemComparison`, dans `EdS.Runtime`) : pour l'objet survolé, la place où il irait et l'objet qui y est porté, chaque bonus (porté → candidat, écart) et, pour un bouclier, la part bloquée (plus haut : mieux), l'endurance par blocage (plus bas : mieux) et la fenêtre de parade (plus large : mieux), chaque ligne marquée mieux, moins bien ou pareil.

**Sauvegarde en version 7** : même forme (objet porté par emplacement), avec les 18 places. Une sauvegarde de version 6 (ou plus ancienne) se relit : ses emplacements `weapon`, `shield`, `chest`, `neck` gardent leur nom ; à la reprise (`Migrated`), ses **places obligatoires vides sont remplies** (`Inventory.FillRequired`) : par l'objet de sa sorte dans le sac, sinon par l'objet de départ (l'Éclat du Seuil ; pour un Bastion qui avait ôté son écu en V1.1, son bouclier du sac ou un écu de chêne), avec le message « Équipement mis à jour : … ».

Limites : l'outil de métier et l'Éclat n'ont pas encore d'effet ; les objets des niveaux 11 à 20 attendent les niveaux au-delà de 10 (V1.8) ; aucune table de butin ne donne encore les nouvelles pièces (seuls les marchands et l'équipement de départ) ; l'aspect suit l'équipement porté tout de suite, ses effets attendent la prochaine entrée.

## Sauvegarde

Fichier local de la tranche (cadrage Q10), versionné, lu par l'analyseur strict et validé par un seul validateur (`SaveValidator`, chemins d'erreur précis). Version 7 depuis le sous-lot V1.2 (version 6 au sous-lot V1.1) :

```json
{
  "schemaVersion": 7,
  "kind": "save",
  "contentVersion": "0.1.0-provisoire",
  "data": {
    "zone": "zone.vallons",
    "worldTick": 72000,
    "sessions": 2,
    "character": {
      "id": "bastion",
      "class": "class.bastion",
      "xp": 135,
      "talents": {},
      "activeSkills": ["skill.bastion.frappe_du_rempart", "skill.bastion.heurt_de_bouclier"],
      "ultimate": null,
      "passiveSkills": ["skill.bastion.endurance_du_roc"],
      "inventory": [
        {"item": "item.peau_de_loup", "count": 3},
        {"item": "item.gambison", "count": 1}
      ],
      "equipment": {"ring_1": "item.anneau_de_cuivre", "shard": "item.eclat_du_seuil_endormi", "shield": "item.ecu_de_chene", "weapon": "item.epee_du_milicien"},
      "story": {
        "facts": ["fact.traces.recit"],
        "quests": {"quest.traces_sous_le_moulin": {"step": "traces", "progress": 0}},
        "trust": {"npc.odile": 1},
        "discoveries": []
      },
      "professions": {"minage": 24},
      "money": 34512
    },
    "consumed": [
      "zone.vallons/1/death/loup1",
      "zone.vallons/safe/place_du_village"
    ]
  }
}
```

- `worldTick` : temps du monde en ticks (au plus un an de jeu) ; `sessions` : sessions déjà jouées ; `consumed` : registre des événements consommés, **trié** et sans doublon (forme canonique : l'écriture redonne toujours le même texte).
- `character` : le personnage (`null` tant qu'il n'est pas créé). Identifiant en minuscules (64 caractères au plus, pour que la clé de sa mort tienne dans le registre), `ultimate` obligatoire (`null` sans ultime), identifiants de classe, de talents et de compétences au format de l'écriture (minuscules, chiffres, « _ » et « . »), talents écrits dans l'ordre ordinal, expérience de 0 à un milliard, rangs de talent positifs. La validité du build contre la classe et le niveau est vérifiée à l'entrée en zone (la sauvegarde ne dépend pas des données du jeu).
- **Bornes** : une sauvegarde fait au plus 32 Mio (100 000 événements de 200 caractères au plus), limite appliquée jusqu'à l'analyseur JSON ; l'écriture refuse ce que la lecture refuserait (temps, sessions, nombre d'événements). Une session de zone ne démarre que si elle tient entière sous la borne du temps du monde (un an de jeu) et que son registre de départ tient sous la sienne ; avant chaque tick, elle vérifie qu'il reste une place pour chaque mort qui ajouterait une clé (combattant mortel, ni mort ni parti, mort pas encore inscrite).
- **Écriture atomique** (`SaveStore`) : le texte est écrit en entier dans `<fichier>.tmp`, vidé sur le disque, puis il remplace la sauvegarde ; la version précédente devient `<fichier>.bak` si elle est valide (une principale abîmée est remplacée sans toucher à la copie). Une coupure laisse toujours une sauvegarde complète. À la lecture, une sauvegarde absente ou invalide est remplacée par sa copie valide ; aucune des deux : nouvelle partie (ou « illisible », avec le motif).
- `inventory` (12.3) : places occupées de l'inventaire, dans l'ordre, une pile par place (objet, quantité de 1 à 999) ; `equipment` : objet porté par emplacement, emplacements dans l'ordre ordinal. Objets, piles, emplacements et niveaux requis sont vérifiés contre le catalogue quand la partie reprend.
- `story` (12.5) : état narratif personnel, trié : faits appris, avancement de chaque quête (étape en cours ou `done`, compteur de l'objectif), confiance non nulle des PNJ, découvertes. Il est vérifié contre l'histoire quand la partie reprend.
- `professions` (12.6) : expérience non nulle de chaque métier.
- `money` (V1.1) : bourse du personnage, en pièces de cuivre, de 0 à 9 999 999 999 (« 3 or 45 argent 12 cuivre » : 34512). Les **points sûrs découverts** (voyage rapide) sont des clés du registre `consumed` (`zone.vallons/safe/place_du_bourg`), comme les passages ouverts et, depuis la réponse de Damien du 30 octobre 2026, les compétences gagnées déjà données (`character/skill/skill.bastion.barriere_d_acier`) ; les lots rachetables chez les marchands ne sont pas sauvegardés.
- **Versions** : les versions 1 (sans personnage), 2 (sans inventaire ni équipement), 3 (sans histoire), 4 (sans métiers), 5 (sans bourse : bourse vide) et 6 (quatre emplacements : places obligatoires remplies à la reprise) sont lues et **mises à jour** (`Migrated`) ; l'écriture donne toujours la version courante. Une version inconnue est refusée.

Limites : [LIMITES_LOT_12.md](LIMITES_LOT_12.md).
