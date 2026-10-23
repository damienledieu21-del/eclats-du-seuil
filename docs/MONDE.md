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

## Sauvegarde

Fichier local de la tranche (cadrage Q10), versionné, lu par l'analyseur strict et validé par un seul validateur (`SaveValidator`, chemins d'erreur précis). Version 2 depuis le sous-lot 12.2 :

```json
{
  "schemaVersion": 2,
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
      "passiveSkills": ["skill.bastion.endurance_du_roc"]
    },
    "consumed": [
      "zone.vallons/1/death/loup1"
    ]
  }
}
```

- `worldTick` : temps du monde en ticks (au plus un an de jeu) ; `sessions` : sessions déjà jouées ; `consumed` : registre des événements consommés, **trié** et sans doublon (forme canonique : l'écriture redonne toujours le même texte).
- `character` : le personnage (`null` tant qu'il n'est pas créé). Identifiant en minuscules (64 caractères au plus, pour que la clé de sa mort tienne dans le registre), `ultimate` obligatoire (`null` sans ultime), expérience de 0 à un milliard, rangs de talent positifs. La validité du build contre la classe et le niveau est vérifiée à l'entrée en zone (la sauvegarde ne dépend pas des données du jeu).
- **Bornes** : une sauvegarde fait au plus 32 Mio (100 000 événements de 200 caractères au plus), limite appliquée jusqu'à l'analyseur JSON ; l'écriture refuse ce que la lecture refuserait (temps, sessions, nombre d'événements). Une session de zone ne démarre que si elle tient entière sous la borne du temps du monde (un an de jeu) et que son registre de départ tient sous la sienne ; avant chaque tick, elle vérifie qu'il reste une place pour chaque mort qui ajouterait une clé (combattant mortel, ni mort ni parti, mort pas encore inscrite).
- **Écriture atomique** (`SaveStore`) : le texte est écrit en entier dans `<fichier>.tmp`, vidé sur le disque, puis il remplace la sauvegarde ; la version précédente devient `<fichier>.bak` si elle est valide (une principale abîmée est remplacée sans toucher à la copie). Une coupure laisse toujours une sauvegarde complète. À la lecture, une sauvegarde absente ou invalide est remplacée par sa copie valide ; aucune des deux : nouvelle partie (ou « illisible », avec le motif).
- **Versions** : la version 1 (12.1, sans personnage) est lue et **mise à jour** (`Migrated`, personnage absent) ; l'écriture donne toujours la version courante. Une version inconnue est refusée. L'inventaire (12.3) et les quêtes (12.5) s'ajouteront de la même façon.

Limites : [LIMITES_LOT_12.md](LIMITES_LOT_12.md).
