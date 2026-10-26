# Essai du lot 12 : les Vallons d'Aube en solo (sous-lot 12.9)

Essai fait par Damien les 25 et 26 octobre 2026, sur PC (Unity 6.3) puis sur le Galaxy Note8. Il a coché une fiche d'essai de 33 points : mission, systèmes, réglages, téléphone. La fiche est une page privée de Damien ; ses réponses sont reprises ici. Le jeu essayé est celui de la PR #49 (12.8), puis les corrections des PR #50 à #53 au fil de l'essai.

## 1. Bilan

**La mission se joue de bout en bout** : Odile, les traces, le minerai, la ferrure, la porte du moulin, le pré des loups, la manifestation, le rôdeur, le Gardien du Seuil et le retour chez Odile sont cochés « OK ». Les systèmes aussi : inventaire et équipement, potions, herbes et potions fabriquées, réapparition des créatures, défaite puis reprise, sauvegarde (quitter et revenir), boutons au doigt sur PC (F8) et sur le Note8 (installation, écrans et dialogues, lisibilité et fluidité). Durée de la mission : environ 30 minutes, « Bien ».

**Réglages gardés par Damien** (« Garder ») : difficulté des combats et du Gardien (après le 12.9a), délais de réapparition, distance de la laisse, prix des recettes, rythme des niveaux ; lisibilité des écrans : « Ça va ».

## 2. Ce que Damien a relevé, et ce qui a été fait

| Remarque de Damien | Fait | PR |
|---|---|---|
| « Je ne peux pas battre le ennemi ils sont trop forts » (les créatures venaient de la démonstration du lot 11, faite pour un Bastion de niveau 10) | Créatures et Gardien rééquilibrés par combats simulés : un niveau 1 bat un loup, un sanglier ou l'archer ; le rôdeur au niveau 5 ; le Gardien au niveau 10 avec des potions. Expérience des créatures : PV ÷ 5 | #50 (12.9a) |
| « Ou se situe la forge ? », « enclume pas visible », puis « Ajoute aussi la forge tant qu'à faire » | Forge de Maëlle : lieu de la zone, juste au nord de la place du village, enclume grise, « K : forger » ; toutes les recettes s'y font | #50 |
| « le boss éveillé devrait être plus fort que sa première forme » | Gardien du Seuil 250 PV, Gardien éveillé 400 PV (dégâts déjà plus hauts) | #50 |
| « C'est normal que j'ai que 2 attaques ? » ; choix : « La 2 pour l'instant et la 3 après » | Compétences gagnées en montant de niveau : Barrière d'acier au niveau 5, Onde de rupture et Bastion absolu au niveau 10, équipées à la halte et annoncées ; progression en schéma 2 | #51 (12.9b) |
| « LE loup est trop rapide je ne peux pas le fuir » | Loups à 4 m/s, sous le Bastion (5 m/s) : on les sème | #52 (12.9c) |
| « je trouve qu'il n'y a pas assez de details » (on comprend où aller) | Chaque étape du journal dit où aller (direction, lieu, PNJ, touche) | #52 |
| « le bouton agir pour récolter actionne aussi le bouclier » ; « ça arrive quand je marche et que je clique sur agir » | Deux doigts posés faisaient un clic droit simulé par Unity, lu comme la garde : au doigt, seul le bouton Garde garde (décision testée, revue Codex) | #52 |
| « J'aurais rajouté une mini carte en haut à droite qu'on puisse agrandir si besoin » | Petite carte en haut à droite (décor, toi, objectif de l'étape en cours, PNJ, points sûrs, forge, ennemis en vue), en grand avec L ou au doigt | #53 (12.9d) |

**Écarts avec le tableau d'équilibrage proposé** (exposés à Damien avant la fusion de la PR #50) : le sanglier a 180 PV et 25 de dégâts au lieu de 260 et 45, le Gardien 250 puis 400 PV au lieu de 700 et 450, avec des dégâts plus bas : avec les valeurs du tableau, les combats simulés étaient perdus.

## 3. À vérifier au prochain essai

Faits après les essais de Damien, testés par le cœur et compilés contre les bouchons de l'API Unity, mais pas encore vus dans Unity ni sur le Note8 :

- la **carte** : taille, couleurs, lisibilité en petit et en grand, étoile de l'objectif ; la fiche de la cible à sa gauche ; la rangée Menu, Aide… sous elle au doigt ;
- la **garde au doigt** : marcher en touchant Agir ne lève plus le bouclier ;
- les **compétences gagnées** aux niveaux 5 et 10, et le **journal détaillé** ;
- **semer un loup** (une vingtaine de secondes de fuite avant qu'il lâche).

Sur le téléphone, une nouvelle construction (**Build And Run**) est nécessaire : le Note8 garde la version installée.

## 4. Décisions de Damien pendant l'essai

- **Compétences** : gagnées en montant de niveau pour l'instant, aux niveaux où les règles ouvrent leurs emplacements (« 5 et 10 »), Barrière d'acier puis Onde de rupture ; un **écran de choix des compétences** viendra ensuite (« la 3 après »).
- **Gardien** : la forme éveillée est plus forte que la première.
- **Petit essai de style** (packs gratuits de modèles, d'animations et de sons) **après le 12.9, avant le lot 13** : « oui on fera un petit essai de style », « apres le 12.9 oui » (25 octobre 2026, 1 h 22).
- **Relecture indépendante avant Codex**, à partir du lot 13 : avant chaque demande de revue Codex, une relecture par un agent neuf, sans le contexte de la session, une seule par PR : « Ok on fera ça » (25 octobre 2026, 2 h 00).

## 5. Suite

Le lot 12 est fait : la tranche des Vallons se joue en solo du niveau 1 à la fin de la mission ; le Note8 l'installe et la fait tourner au doigt. Prochaines étapes, dans l'ordre décidé : le petit essai de style, puis le lot 13 (monde partagé, [plan](PLAN_LOT_11.md#lot-13--monde-partagé-et-persistance)). Les points de la section 3 se vérifient au passage, au prochain essai.
