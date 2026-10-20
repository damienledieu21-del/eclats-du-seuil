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

## Sous-lot 11.4, première étape : positions, déplacement, portée

Le moteur spatial (11.4, « grande » taille au plan) est livré par étapes, chacune dans un état cohérent et vert. Cette première étape pose l'espace, le déplacement et la portée des compétences à cible ([MOTEUR.md](MOTEUR.md#combat-dans-lespace-sous-lot-114-première-étape)). Ce qui manque encore, pour les étapes suivantes du 11.4 :

- **Décor** : aucun obstacle (segments, polygones convexes) ni **ligne de vue** (`noLineOfSight`) ; seuls les bords du rectangle `space.bounds` sont solides. Une seule surface praticable, sans étage ni passage.
- **Collisions** : aucune entre combattants. Les règles candidates A (volumes solides sans poussée, garde-fou du dernier passage) et B (traversée freinée), réglables par les données, viendront avec le décor ; le choix revient à Damien après l'essai du 11.6.
- **Zones et défenses par attaque** : les attaques des assaillants touchent toujours leur cible où qu'elle soit (comportements et formes au 11.5 et à l'étape suivante : `shape`, `telegraphMs`, `guardable`, `parryable`, zones résolues au tick d'impact). Les compétences de zone et de soi du Bastion n'ont pas encore de rayon : Onde de rupture touche encore tous les ennemis vivants, Bastion absolu tous les alliés et tous les ennemis, et les déclencheurs (Riposte précise, Jugement du colosse, Serment du gardien, Lien protecteur) ignorent la distance.
- **Arc du bouclier et orientation en garde** (retenus le 20 octobre 2026) : pas encore appliqués ; la garde et la parade couvrent encore toutes les directions. L'orientation suit seulement le sens de la marche.
- **Assaillants et mannequins immobiles** : ils ne prennent pas de commande de déplacement (`actorCannotAct`) ; ils se déplaceront par leurs comportements au 11.5.
- **Combattants qui arrivent et partent** pendant le combat : pas encore (composition fixe).
- **Altérations et déplacement** : seul le `movementSpeedModifier` des altérations agit (le gel ralentit de 30 %). Aucune altération n'empêche de marcher, pas même l'étourdissement : voir la question ci-dessous.
- **Réseau** : le protocole 1 ne transporte pas les déplacements (démonstration réseau positionnée au 11.7) ; aucun scénario positionné n'est proposé en réseau.
- **Unity** : rien de visible encore ; la scène de démonstration est le 11.6.
- **Formats** : moteur 0.16.0, journal au format 10, règles en schéma 10 (contenu 0.9.0), classes en schéma 9 (Bastion 0.6.0), scénarios en schéma 5. Les journaux 0.15.0 sont refusés au rejeu, sans migration (pré-alpha) ; les treize scénarios livrés, sans espace, gardent le même corps de journal.

### Question pour Damien (11.4)

- **L'étourdissement empêche-t-il de marcher ?** Aujourd'hui (proposition réversible, faute de décision), aucune altération ne bloque le déplacement : un Bastion étourdi ne peut ni frapper, ni garder, ni parer, mais peut encore sortir d'une zone. Proposition de Claude : **oui, l'étourdissement bloque aussi le déplacement** (nouvelle valeur `movement` de `blocks`, en données, ajoutée à `stun`), sinon il perd l'essentiel de son effet dans l'espace ; le gel resterait un ralentissement. Les ennemis n'étant pas encore mobiles, rien ne change en jeu avant le 11.5.
