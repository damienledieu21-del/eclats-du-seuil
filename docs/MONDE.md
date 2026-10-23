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

## Sauvegarde

Fichier local de la tranche (cadrage Q10), versionné, lu par l'analyseur strict et validé par un seul validateur (`SaveValidator`, chemins d'erreur précis) :

```json
{
  "schemaVersion": 1,
  "kind": "save",
  "contentVersion": "0.1.0-provisoire",
  "data": {
    "zone": "zone.vallons",
    "worldTick": 72000,
    "sessions": 2,
    "consumed": [
      "zone.vallons/1/death/loup1"
    ]
  }
}
```

- `worldTick` : temps du monde en ticks (au plus un an de jeu) ; `sessions` : sessions déjà jouées ; `consumed` : registre des événements consommés, **trié** et sans doublon (forme canonique : l'écriture redonne toujours le même texte).
- **Bornes** : une sauvegarde fait au plus 32 Mio (100 000 événements de 200 caractères au plus), limite appliquée jusqu'à l'analyseur JSON ; l'écriture refuse ce que la lecture refuserait (temps, sessions, nombre d'événements). Une session de zone ne démarre que si elle tient entière sous la borne du temps du monde (un an de jeu) et que son registre de départ tient sous la sienne ; avant chaque tick, elle vérifie qu'il reste une place pour chaque mort qui ajouterait une clé (combattant mortel, ni mort ni parti, mort pas encore inscrite).
- **Écriture atomique** (`SaveStore`) : le texte est écrit en entier dans `<fichier>.tmp`, vidé sur le disque, puis il remplace la sauvegarde ; la version précédente devient `<fichier>.bak` si elle est valide (une principale abîmée est remplacée sans toucher à la copie). Une coupure laisse toujours une sauvegarde complète. À la lecture, une sauvegarde absente ou invalide est remplacée par sa copie valide ; aucune des deux : nouvelle partie (ou « illisible », avec le motif).
- **Versions** : une autre version est refusée (pré-alpha, pas encore de joueurs à migrer). Le personnage (12.2), l'inventaire (12.3) et les quêtes (12.5) s'ajouteront en incrémentant la version.

Limites : [LIMITES_LOT_12.md](LIMITES_LOT_12.md).
