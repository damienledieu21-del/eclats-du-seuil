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
