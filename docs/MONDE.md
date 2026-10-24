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

`WorldClock` tire l'heure du monde des ticks, jamais de l'horloge réelle (cadrage Q26). Valeurs **provisoires**, réglables : une journée du monde dure **2 h de jeu** ; le monde commence à **8 h** du jour 0. Moments de la journée : nuit de 21 h à 5 h, aube de 5 h à 7 h, jour de 7 h à 19 h, crépuscule de 19 h à 21 h. Tout est entier : un même tick donne toujours la même heure. Le temps du monde continue d'une session à l'autre (début de session = temps de la sauvegarde). Les effets du jour et de la nuit sur le jeu (créatures, traces, ressources, passages) viendront avec le contenu des Vallons.

## Personnage (sous-lot 12.2)

Le personnage persistant (cadrage Q3, Q20) : un identifiant, une classe, son **expérience**, ses talents et ses compétences choisies. Son **niveau** se déduit de l'expérience ; ses **attributs** viennent d'une table par classe et niveau (pas de points à répartir à la main) ; son build est validé contre la classe et le niveau par le validateur unique des builds avant chaque entrée en zone.

- **Progression** : fichier `data/v0/world/progression.json` (`kind` « progression », schéma `data/schemas/progression.schema.json`, validé par `ProgressionValidator`). Valeurs **provisoires**, réglables dans les données :
  - niveaux 1 à 10 ; expérience cumulée pour atteindre chaque niveau : 0, 100, 220, 360, 520, 700, 900, 1 120, 1 360, 1 620 ;
  - expérience d'une créature vaincue : ses PV maximaux divisés par 20 (au moins 1) — un loup de 700 PV donne 35, un sanglier de 1 200 PV 60 ;
  - attributs du Bastion du niveau 1 (force 12, dextérité 6, intelligence 4, vitalité 12, volonté 8, endurance 6) au niveau 10 (18, 7, 5, 18, 10, 10), jamais en baisse d'un niveau à l'autre ;
  - build de départ du Bastion : Frappe du rempart et Heurt de bouclier, Endurance du roc ; aucun talent.
- **Création** : `PlayerCharacters.Create(id, classe, progression)` donne un personnage de niveau 1 avec le build de départ de sa classe.
- **Partie en solo** (`WorldSession`) : reprend la sauvegarde, ouvre la session de zone suivante au temps du monde sauvegardé, y fait entrer le personnage, et lui donne l'expérience de chaque créature vaincue **pour la première fois** (registre des événements consommés) pendant qu'il est présent et vivant. En solo, il est seul à pouvoir l'avoir vaincue ; le partage en groupe viendra avec le serveur (lot 13).
- **Niveau gagné** : il prend effet à la **prochaine entrée** dans une zone (le combat en cours garde le build d'entrée, comme le reste du moteur qui ne change pas un build en route). Les points de talent du nouveau niveau se dépensent hors combat (écran du lot 12.8).

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

- **Simulation** : le scénario de zone `scenarios/zones/vallons.json`, agrandi à 84 × 58 m (de [−24, −18] à [60, 40]) : le village et le moulin à l'ouest de la rivière (gué inchangé), la haie du nord percée au moulin par une porte, le pré des loups au-delà (la rivière s'arrête à z = 34 : on le rejoint aussi par un long détour à l'est puis au nord), les ruines au centre, la carrière à l'est (blocs, front de taille), la petite zone du Seuil au nord-est (pierres dressées). Dix créatures : cinq loups, trois sangliers, l'archer des ruines et un rôdeur du Seuil (attaques magiques).
- **Points sûrs** : place du village (entrée), camp de la carrière. Le personnage entre au point d'entrée ; le retour après une défaite viendra avec le 12.7. Chaque point sûr doit pouvoir recevoir l'emprise d'un personnage (0,5 m) : entière dans l'aire praticable, hors de tout obstacle (porte du moulin fermée comprise), sans chevaucher la place d'une créature de départ qui lui serait solide.
- **Lieux nommés** (rectangles) : village, moulin, rivière, pré des loups, ruines, carrière, petite zone du Seuil (`PlacesAt(point)`), pour les découvertes et les quêtes (12.5). La « manifestation » de la mission aura lieu dans la petite zone du Seuil.
- **Populations** : chaque créature du scénario, avec son délai de réapparition (loups 90 s, sangliers et archer 2 min, rôdeur 3 min), sa laisse (18 à 20 m) et sa table de butin.
- **Passage du moulin** : la porte `porte_moulin` ferme la haie ; `OpenPassage("moulin")` l'ouvre **pour de bon** (clé `zone.vallons/passage/moulin` au registre du monde, gardé dans la sauvegarde). L'obstacle disparaît à la **session suivante** (`ZoneSetups`).

**Vie des populations** (`WorldSession` sur une zone du monde) :

- **Réapparition** (section 2 de l'architecture : une identité n'est jamais réutilisée) : une créature morte revient après son délai, à sa place, **sous une nouvelle identité** (`loup1_v2`, `loup1_v3`… ; une identité déjà portée par un autre combattant de la session est sautée), calme et soignée. Sa mort garde sa clé unique : expérience et butin une seule fois par vie. Une place occupée retarde l'arrivée d'une seconde.
- **Laisse** (essai du 22 octobre 2026) : une créature engagée dont l'écart à sa cible, entre les bords des emprises, dépasse sa laisse **lâche** : elle quitte la zone (ni mort, ni expérience, ni butin) et une vie calme et soignée la remplace à sa place au tick suivant.
- **Refus du moteur** : un départ (laisse) ou une arrivée (réapparition) refusé par le moteur (plafond de commandes, file pleine) ne fait pas échouer le tick : il est retenté au tick suivant (la laisse) ou une seconde plus tard (la réapparition). Les récompenses du tick (expérience, butin) sont attribuées avant la vie des populations.
- **Plafond** : une session tient au plus 64 combattants (règles) ; une population qui n'a plus de place attend la session suivante (`Exhausted`).

## PNJ, dialogues et quêtes (sous-lot 12.5)

L'histoire de la tranche est en données : `data/v0/world/story.json` (`kind` « story », schéma `story.schema.json`, validé par `StoryValidator` contre les textes, la zone et le catalogue des objets) et les **textes par clés** `data/v0/world/texts/fr.json` (`kind` « texts », cadrage Q37 : français d'abord, anglais ensuite sans toucher au reste). Aucun texte affiché n'est écrit dans le code ni dans l'histoire : seulement des clés `text.…`.

- **PNJ** : identifiant `npc.…`, nom (clé), position dans la zone, dialogue. On leur parle à **3 m** au plus (provisoire), présent et vivant.
- **Dialogues** : des nœuds (texte, réponses) et des **entrées** : la première entrée dont les conditions sont réunies donne le nœud de départ. Chaque réponse a ses conditions (elle n'est proposée que si elles sont réunies), ses **effets** et son nœud suivant (ou la fin).
- **Conditions** (H3) : acquis durables (`fact`, `notFact`, état d'une quête : `questNotStarted`, `questActive`, `questAt` une étape, `questDone` ; confiance d'un PNJ : `trustAtLeast`, `trustBelow`) et circonstances temporaires (`hasItem`, moment de la journée `phase`, lieu `inPlace`, `passageOpen`).
- **Effets** : `setFact`, `trust` (H1 : les PNJ se souviennent ; de −100 à 100), `startQuest`, `giveItem`, `takeItem`, `giveXp`, `openPassage`, `discover` (journal des découvertes). Les effets d'une réponse s'appliquent **en entier ou pas du tout** : des objets qui ne tiennent pas ou qui manquent laissent tout inchangé (`NoRoom`).
- **Quêtes** : des étapes successives, chacune avec un texte de journal, un **objectif** vérifié à chaque tick (`fact` appris, lieu atteint `reach`, créatures vaincues `defeat` pendant l'étape, objets détenus `collect`, `passage` ouvert) et ses effets ; la quête a ses récompenses finales. Des récompenses qui ne tiennent pas dans l'inventaire font attendre l'étape (`RewardBlocked`, annoncé une fois).
- **Portée** (H2) : tout l'état narratif est **personnel** (faits, quêtes, confiance, découvertes), gardé avec le personnage. Les expéditions de groupe et les chapitres publics viendront avec le monde partagé (lot 13).

**Mission « Les traces sous le moulin »** (Q2), valeurs et textes provisoires :

1. **Enquêter** : Odile, la meunière, raconte les traces. Deux réponses : aider (confiance +1) ou la soupçonner (confiance −1) — la mission commence dans les deux cas.
2. **Suivre les traces** jusqu'à la porte du moulin (lieu « moulin ») ; découverte des traces griffues.
3. **Rouvrir le passage** : Maëlle, la forgeronne, forge une ferrure avec trois minerais de fer (portés par les sangliers de la carrière, en attendant la récolte du 12.6) ; le passage du moulin s'ouvre pour de bon. Si Odile a confiance, Maëlle donne un indice sur le minerai.
4. **Le pré des loups** : passer la porte, découvrir les éclats.
5. **La manifestation** : atteindre la petite zone du Seuil ; découverte de la manifestation.
6. **Le rôdeur du Seuil** : le vaincre (le chef de fin s'ajoutera au 12.7).
7. **Revenir chez Odile** : si elle a confiance, elle offre l'**amulette de l'aube** ; sinon, un merci sec (conséquence observable liée à un PNJ, Q2). Récompense finale : 300 points d'expérience.

Gaspard, le vieux garde du camp de la carrière, conseille contre le rôdeur pendant l'étape 6.

## Sauvegarde

Fichier local de la tranche (cadrage Q10), versionné, lu par l'analyseur strict et validé par un seul validateur (`SaveValidator`, chemins d'erreur précis). Version 4 depuis le sous-lot 12.5 :

```json
{
  "schemaVersion": 4,
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
      "equipment": {"shield": "item.ecu_de_chene", "weapon": "item.epee_du_milicien"},
      "story": {
        "facts": ["fact.traces.recit"],
        "quests": {"quest.traces_sous_le_moulin": {"step": "traces", "progress": 0}},
        "trust": {"npc.odile": 1},
        "discoveries": []
      }
    },
    "consumed": [
      "zone.vallons/1/death/loup1"
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
- **Versions** : les versions 1 (sans personnage), 2 (sans inventaire ni équipement) et 3 (sans histoire) sont lues et **mises à jour** (`Migrated`) ; l'écriture donne toujours la version courante. Une version inconnue est refusée.

Limites : [LIMITES_LOT_12.md](LIMITES_LOT_12.md).
