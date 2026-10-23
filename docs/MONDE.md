# Le monde (lot 12)

Le projet `EdS.World` (netstandard2.1, sans dépendance) réalise l'architecture du monde partagé ([ARCHITECTURE_MONDE.md](ARCHITECTURE_MONDE.md), retenue le 20 octobre 2026) pour la tranche des Vallons d'Aube ([PLAN_LOT_11.md](PLAN_LOT_11.md#lot-12--les-vallons-daube-en-solo)). En solo, la zone tourne sur le poste, avec le code qu'utilisera le serveur de zone (lot 13).

## Session de zone (sous-lot 12.1)

Une **session de zone** (`Zone`) est une simulation de zone : le moteur de combat en mode zone ([MOTEUR.md](MOTEUR.md#mode-zone-sous-lot-121)), qui ne s'arrête pas quand une équipe tombe.

- **Identité** : `zone.<nom>` (`zone.vallons`), numéro de session (1 pour la première partie, puis un de plus à chaque reprise), temps du monde au début de la session.
- **Entrer et sortir** : `Enter(personnage)` le fait arriver au tick courant, à sa place (arrivée pilotée du moteur : refusée si une emprise solide occupe la place) ; `Leave(id)` le fait partir en fin de tick. Rien n'est réservé d'avance.
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

## Sauvegarde

Fichier local de la tranche (cadrage Q10), versionné, lu par l'analyseur strict et validé par un seul validateur (`SaveValidator`, chemins d'erreur précis). Version 3 depuis le sous-lot 12.3 :

```json
{
  "schemaVersion": 3,
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
      "equipment": {"shield": "item.ecu_de_chene", "weapon": "item.epee_du_milicien"}
    },
    "consumed": [
      "zone.vallons/1/death/loup1"
    ]
  }
}
```

- `worldTick` : temps du monde en ticks (au plus un an de jeu) ; `sessions` : sessions déjà jouées ; `consumed` : registre des événements consommés, **trié** et sans doublon (forme canonique : l'écriture redonne toujours le même texte).
- `character` : le personnage (`null` tant qu'il n'est pas créé). Identifiant en minuscules, expérience de 0 à un milliard, rangs de talent positifs. La validité du build contre la classe et le niveau est vérifiée à l'entrée en zone (la sauvegarde ne dépend pas des données du jeu).
- **Écriture atomique** (`SaveStore`) : le texte est écrit en entier dans `<fichier>.tmp`, vidé sur le disque, puis il remplace la sauvegarde ; la version précédente devient `<fichier>.bak`. Une coupure laisse toujours une sauvegarde complète. À la lecture, une sauvegarde absente ou invalide est remplacée par sa copie valide ; aucune des deux : nouvelle partie (ou « illisible », avec le motif).
- `inventory` (12.3) : places occupées de l'inventaire, dans l'ordre, une pile par place (objet, quantité de 1 à 999) ; `equipment` : objet porté par emplacement, emplacements dans l'ordre ordinal. Objets, piles, emplacements et niveaux requis sont vérifiés contre le catalogue quand la partie reprend.
- **Versions** : la version 1 (12.1, sans personnage) et la version 2 (12.2, sans inventaire ni équipement) sont lues et **mises à jour** (`Migrated` ; personnage absent, ou inventaire et équipement vides) ; l'écriture donne toujours la version courante. Une version inconnue est refusée. Les quêtes (12.5) s'ajouteront de la même façon.

Limites : [LIMITES_LOT_12.md](LIMITES_LOT_12.md).
