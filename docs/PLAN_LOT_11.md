# Plan à partir du lot 11 — proposition (en attente des décisions de Damien)

Proposition de Claude du 19 octobre 2026, deuxième version, écrite après le [cadrage de Damien](CADRAGE.md) du même jour. Elle remplace la première version (zone de forêt, questions Q1 à Q10), dont les réponses sont désormais dans le cadrage.

**Rien n'est codé** avant que Damien ait validé l'ordre des chantiers (décision L11-D1, section 8). Les choix de gameplay restent les siens. Une fois tranchés, ils passent dans la [feuille de route](FEUILLE_DE_ROUTE.md) et dans le [registre du cadrage](CADRAGE.md).

## 1. État réel du projet

Vérifié dans le code le 19 octobre 2026 (`main` à `f5998c5`).

| Domaine | Ce qui existe | Ce qui manque pour le cadrage |
|---|---|---|
| **Combat** | Moteur déterministe 0.14.0 (tick de 50 ms, virgule fixe, journal au format 8, rejeu octet par octet), 64 combattants par combat au plus (paramètre, borne 256). **Bastion complet** : trois arbres, six actives, l'ultime, huit passifs, garde, parade, Charges, Résonance, boucliers, provocation, transfert, altérations, déclencheurs | **Ennemis qui meurent** : les assaillants n'ont pas de PV et frappent à intervalles programmés (`FirstAttackMs`, `IntervalMs`), sans comportement. Les joueurs ne peuvent que perdre ou tenir jusqu'au temps imparti. **Aucun soin** (pas de catégorie d'effet) |
| **Positions** | Aucune (A7). Unity déplace le personnage et place les assaillants pour l'affichage seulement | Tout le socle : positions, déplacement, portée, formes, zones au sol, collisions, obstacles |
| **Progression** | Règles de niveau dans `rules.json` (niveau maximal 30, points, déblocages, majeur à 18 points + 1), `BuildValidator`, `TalentEditor` (redistribution point par point), builds de référence aux niveaux 30 et 10 | Expérience, attributs par niveau, livres, héritages, maîtrises |
| **Objets** | Les formules prévoient trois puissances d'équipement (`StatContributions`). L'origine `equipment` des déclencheurs est réservée | Objets, raretés, équipement, inventaire, butin, effets d'équipement |
| **Monde** | Rien | Zone, PNJ, dialogues, quêtes, récolte, fabrication, points sûrs, temps du monde |
| **Multijoueur** | Lot 10 : serveur autoritaire d'**un combat**, salle de deux places (le protocole en admet huit), TCP sans chiffrement pour le réseau local. Chaque client rejoue tout le combat et compare l'empreinte ; resynchronisation ; retour dans les 30 s ; robot ; mesures (`eds reseau`) ; télégraphie avancée du ping (option B) | Monde partagé, réplication filtrée, prédiction du déplacement, comptes, chiffrement, hébergement |
| **Persistance** | Aucune, à part les journaux de combat | Sauvegarde locale, puis sauvegarde serveur |
| **Unity** | Prototype jouable sur PC (Windows) : arène générée, interface IMGUI, combat solo et réseau, écran des talents, graphismes provisoires. **Deux caméras** : « isométrique » (perspective plongeante à 50°, recul de 8 à 30, quarts de tour), troisième personne (18°, recul de 3 à 14, souris) ; bascule par la touche C, sans transition | Vues 1 et 2 reliées, visibilité près des obstacles, scène de terrain, commandes tactiles, essai sur téléphone |
| **Textes** | En français, écrits dans le code d'Unity et dans les noms des données | Textes par clés, préparés pour la traduction (Q37) |

## 2. Acquis réutilisables

- **Déterminisme et rejeu** : base de l'autorité du serveur, des tests de propriétés et des journaux de référence, pour le monde comme pour le combat.
- **Reconstruction à un tick confirmé** (`NetworkCombat`, lot 10) : c'est le mécanisme de la prédiction locale avec correction, nécessaire au déplacement en réseau.
- **Code partagé** : `IntentionPlanner` (intentions en commandes) et `CombatDisplay` (affichage), communs au solo et au réseau.
- **Option B** : la télégraphie avancée du ping s'étend aux zones au sol (instant d'impact affiché en avance du temps aller-retour).
- **Équipement** : `StatContributions` pour les bonus de caractéristiques, l'origine `equipment` des déclencheurs pour les effets spéciaux.
- **Données** : validateur unique, schémas versionnés, refus avec chemin précis, pour les nouvelles données (ennemis, objets, zone, quêtes, recettes).
- **Caméras** : les deux caméras du prototype sont les bases des vues 1 et 2.
- **Mesures** : `eds report` et `eds reseau`, les pilotes scriptés et les robots.

## 3. Ordre proposé des chantiers

**Les positions d'abord.** Comportement des ennemis, compétences, réseau, Unity, rencontres du monde et contenu dépendent de l'espace. Construire la tranche sur des positions seulement visuelles obligerait à tout refaire ensuite. Le cadrage (Q13) le déconseille aussi.

Viennent ensuite le contenu en solo, puis le monde partagé, puis le mobile. La simulation de zone est écrite dès le lot 12 pour tourner sur le serveur : en solo, le jeu héberge la zone sur le poste, comme l'hôte du lot 10. Le monde partagé ajoute donc le réseau et l'hébergement, sans réécriture (décision L11-D8).

| Jalon | Lot | Objectif | Démonstration attendue | Périmètre |
|---|---|---|---|---|
| 1 | **11** | **Combat dans l'espace** | Scène de démonstration (H14) : esquive réelle, portée, zones au sol, vues 1 et 2 ; en solo et à deux en réseau | Tranche |
| 2 | **12** | **Les Vallons d'Aube en solo** | Mission « Les traces sous le moulin » de bout en bout, niveaux 1 à 10, équipement, récolte et fabrication, chef, sauvegarde locale | Tranche |
| 3 | **13** | **Monde partagé et persistance** | Deux joueurs, puis davantage, dans les Vallons : se voir, combattre ensemble, chacun sa mission ; comptes de test et sauvegarde sur le serveur | Tranche |
| 4 | **14** | **Mobile et nouvelle interface** | La tranche sur téléphone Android, même personnage sur PC et mobile, interface qui remplace l'IMGUI du prototype | Tranche |
| 5 | **15 et suivants** | **Première alpha** | Trois classes, refonte de la progression, donjon avec boss et réveil renforcé, groupes, échanges, une à trois régions selon le chiffrage | Alpha |
| 6 | — | **Jeu complet** | Ville, cinq régions, Cœur du Seuil, économie, guildes, factions, boss de monde, refuge… | Final |

## 4. Lot 11 — combat dans l'espace

Objectif : un combat où se placer compte, résolu par le moteur et le serveur, lisible dans les deux vues. On peut sortir d'une zone annoncée pour l'éviter. La garde et la parade restent au cœur du Bastion.

| Sous-lot | Contenu | Taille |
|---|---|---|
| 11.1 | **Ennemis qui meurent** : PV des assaillants en données, mort, victoire des joueurs ; nouvelles versions du schéma des scénarios et du journal, nouveaux journaux de référence ; barre de vie et mort dans Unity. Valeurs provisoires | moyenne |
| 11.2 | **Règles du combat dans l'espace** (document et décisions L11-D2 à L11-D7) : unités, plan au sol, vitesses, garde en mouvement, portées et formes (cercle, cône, ligne), zones au sol annoncées, collisions, obstacles, autorité du serveur, prédiction, formats. Il donne aussi des **portées et rayons provisoires pour chaque compétence du Bastion**, à valider : « tous les ennemis » devient « tous les ennemis dans le rayon » | petite |
| 11.3 | **Moteur spatial** : positions en virgule fixe, déplacement par intentions à vitesse bornée, obstacles statiques, collisions simples, ciblage spatial (`SkillTargeting` étendu), zones au sol résolues au tick d'impact ; portées du Bastion en données ; scénarios positionnés ; propriétés et frontières au tick près | grande |
| 11.4 | **Ennemis dans l'espace** : comportements en données (approcher, poursuivre, garder une distance, encercler, attaque de zone annoncée, charge), choix de cible déterministe | moyenne |
| 11.5 | **Déplacement en réseau** : intentions de déplacement, prédiction locale du joueur et correction (reconstruction au dernier tick confirmé, puis rejeu), autres combattants lissés, zones au sol avancées comme l'option B ; mesures `eds reseau` de l'esquive de 0 à 200 ms d'un sens, avec gigue | grande |
| 11.6 | **Scène de démonstration dans Unity (H14)** : petit coin des Vallons en formes simples (moulin, pont, arbres, un toit, un passage étroit), Bastion, plusieurs ennemis, attaque annoncée au sol, obstacle qui masque le personnage. Caméra réglable entre les vues 1 et 2 (molette, rappel rapide, réglage gardé, aucune bascule automatique), visibilité adaptée près des obstacles sans voir à travers les parois ; déplacement simulé par le moteur | grande |
| 11.7 | **Premier essai tactile** (si un téléphone Android est disponible, L11-D11) : version Android, déplacement au pouce, boutons, geste de zoom entre les deux vues, images par seconde mesurées | moyenne |
| 11.8 | **Essai et rapport** : les quatre vérifications de H14 (même combat dans les deux vues, obstacles et passages, commande et stabilité, lisibilité et performances), esquive en réseau, réglages | petite |

11.1 sert dans tous les cas : il peut commencer dès l'accord sur l'ordre (L11-D1), pendant que Damien répond aux décisions L11-D2 à L11-D7.

## 5. Lots 12 à 14, alpha, jeu complet

### Lot 12 — les Vallons d'Aube en solo

| Sous-lot | Contenu | Taille |
|---|---|---|
| 12.1 | **Architecture du monde** (`EdS.World`, netstandard2.1, sans dépendance) selon L11-D8 : simulation de zone déterministe, entités qui entrent et sortent, combats sur place, temps du monde tiré des ticks (Q26), formats de sauvegarde | moyenne |
| 12.2 | **Personnage** : expérience et niveaux 1 à 10 (courbe provisoire), attributs par classe et niveau (Q20), déblocages ; sauvegarde locale versionnée, écriture atomique, migrations | moyenne |
| 12.3 | **Objets, équipement, inventaire** : catalogue (niveau requis, puissance, rareté à six rangs, propriétés), quatre emplacements dont le bouclier, 24 places empilables, butin ; un premier effet spécial d'équipement si le temps le permet | grande |
| 12.4 | **Les Vallons en données** : village, rivière, moulin, carrière, ruines, petite zone du Seuil ; points sûrs ; populations et réapparitions ; raccourci du moulin (état gardé) ; manifestation ; nœuds de récolte | moyenne |
| 12.5 | **PNJ, dialogues et quêtes** : états narratifs (H1), conditions durables et temporaires (H3), journal des découvertes, portée personnelle des choix (H2) ; mission « Les traces sous le moulin » | grande |
| 12.6 | **Métiers** : minerai et forge, plantes et remèdes, XP de métier séparée ; potions limitées à recharge partagée si Q7 est confirmée (catégorie « soin » dans le moteur) | moyenne |
| 12.7 | **Chef de fin et récompenses** : boss à phases, défaite sans perte (retour au point sûr), registre des récompenses attribuées une seule fois ; réveil renforcé seulement s'il entre dans la tranche (Q2) | moyenne |
| 12.8 | **Unity** : la zone jouable en formes simples, inventaire, équipement, journal de quête, dialogues, fabrication ; textes par clés (Q37) | grande |
| 12.9 | **Essai complet et rapport** : durée, difficulté, compréhension de la mission, réglages | petite |

### Lot 13 — monde partagé et persistance

- **Serveur de zone** : la simulation du lot 12 tourne sur le serveur ; les joueurs entrent et sortent.
- **Réplication** filtrée par zone d'intérêt, prédiction du joueur local (celle du 11.5, généralisée).
- **Règles à plusieurs** : crédit des actions, aide, partage de quête, réinitialisation des rencontres quand tous sont partis (Q8), portée des choix (H2).
- **Comptes de test** et sauvegarde sur le serveur (Q10), chiffrement des échanges.
- **Hébergement d'essai** : sur le réseau local, rien à payer ; sur Internet, c'est une dépense que Damien décide (L11-D12).
- **Mesures** : joueurs par zone, bande passante, rapport.

### Lot 14 — mobile et nouvelle interface

Commandes tactiles complètes, nouvelle interface pensée pour le PC et le mobile (remplace l'IMGUI), performances mesurées sur les téléphones visés, même personnage et même serveur sur PC et mobile.

### Première alpha (lots 15 et suivants)

- **Classes** : deuxième et troisième classes (le mage du Seuil est proposé, Q17).
- **Progression refondue** : livres (deux actifs), héritages, maîtrises, budget de points, plafond (Q18, H6, H7).
- **Donjon** : boss garanti et réveil renforcé (H4) ; groupes (taille Q31).
- **Régions** : une à trois selon le chiffrage (Q22).
- **Métiers et échanges** : métiers étendus, échanges directs et commandes, refuge de base.
- **Langues** : textes en français et en anglais.
- **Production artistique** : méthode et budget à décider (Q39).
- **Essais extérieurs** : outils de retour des testeurs, conditions d'un essai (Q40).

### Jeu complet

Havre d'Ambre et les cinq régions, Cœur du Seuil, marchés régionaux et caravanes, guildes, factions et transformations des régions, montures et voyages, boss de monde, combat entre joueurs volontaire, refuge complet, compagnons, Atlas de la Rupture, jour, nuit et météo, modèle économique.

## 6. Validations ciblées et conception transverse

### Règles sensibles et leur preuve

| Règle | Preuve prévue | Lot |
|---|---|---|
| **Portée et esquive réelles** | Hors de portée, la compétence est refusée. Un combattant sorti de la zone au tick d'impact n'est pas touché, resté dedans il l'est (frontière exacte, au tick près). Propriétés sur positions aléatoires, journaux de référence ; en réseau, mesure de l'esquive de 0 à 200 ms | 11.3, 11.5 |
| **Caméra sans effet sur les règles** | Le même combat scripté joué dans les deux vues donne le même journal, octet par octet ; essai de Damien | 11.6, 11.8 |
| **Conservation après défaite** | Propriété : après toute défaite, niveaux, XP, objets, livres, métiers et étapes acquises sont inchangés ; seuls les consommables utilisés manquent | 12.2, 12.7 |
| **Récompenses attribuées une fois** | Registre à clés uniques (personnage, rencontre, palier). Ni défaite, ni redémarrage, ni reconnexion, ni message en double, ni sauvegarde rechargée ne rendent deux fois le premier palier. La suite renforcée n'est récompensée que vaincue | 12.7, 13 |
| **Conditions des secrets** | Évaluateur de conditions : acquis durables gardés, circonstances temporaires vérifiées au déclenchement ; chaque combinaison testée, dans n'importe quel ordre ; retour clair dans le journal | 12.5 |
| **Deux livres et budget** | Le validateur des builds refuse un troisième livre ; une copie ne cumule pas ; les points reviennent au retrait ; le budget n'est jamais dépassé | alpha |
| **XP de métier indépendante** | Fabriquer ne donne pas d'XP de combat, combattre pas d'XP de métier ; une fabrication triviale forme moins | 12.6 |
| **Portée des choix narratifs** | Chaque choix a sa portée (personnelle, expédition, publique). Deux joueurs aux choix différents dans la même zone voient chacun la leur ; un PNJ indispensable n'est jamais supprimé pour les autres | 12.5, 13 |
| **Sauvegarde et reprise** | Sauvegarder puis charger redonne exactement le même état. Une coupure pendant l'écriture laisse l'ancienne sauvegarde intacte. Versions et migrations sont testées ; un fichier corrompu est refusé avec un message clair | 12.2, 13 |

### Sujets de conception, avec leurs critères (proposés, à valider par les essais)

- **Interface PC et mobile** (lot 14, maquettes avant) :
  - lisible en 1920 × 1080 et sur un téléphone de 6 pouces ;
  - cibles tactiles d'au moins 9 mm ;
  - actions de combat atteignables au pouce ;
  - aucune information indispensable portée par la seule couleur.
- **Animations** : chaque coup ennemi est annoncé par une animation et une zone lisibles dans les deux vues. Les animations suivent les ticks et ne décident d'aucun résultat (le moteur décide).
- **Sons** : un son distinct pour le blocage, la parade parfaite, la parade ratée et la rupture de garde ; une alerte sonore pour un coup dangereux hors du champ.
- **Retours visuels** : les effets laissent voir le terrain, les adversaires et les annonces (H14). Rareté, réveil renforcé et « hors de portée » sont reconnaissables.

## 7. Estimation

Indicative, non engagée, à corriger après le lot 11 sur le rythme réel. Hypothèses :

- le rythme des lots 9 et 10 : quatre à six sous-lots en un à deux jours, revues Codex comprises (deux à trois tours) ;
- Damien essaie chaque livraison Unity dans les 24 heures ;
- les décisions sont prises avant leur lot ;
- formes simples jusqu'à l'alpha, aucune dépense.

| Lot | Sous-lots | Dont grands | Durée indicative | Incertitude |
|---|---|---|---|---|
| 11 | 8 | 3 | 1 à 2 semaines | moyenne : le déplacement en réseau est neuf |
| 12 | 9 | 3 | 2 à 4 semaines | moyenne : contenu (dialogues, quête) à écrire avec Damien |
| 13 | environ 6 | 2 | 2 à 4 semaines | forte : réplication, comptes, hébergement |
| 14 | environ 4 | 2 | 1 à 3 semaines | forte : dépend du téléphone et des performances |
| Alpha | 25 à 40 | — | plusieurs mois | très forte : le contenu (classes, régions, donjon) et la production artistique pèsent plus que le code |

Tranche complète (lots 11 à 14) : de 6 à 13 semaines environ, selon ces hypothèses.

## 8. Décisions nécessaires

Pour chaque décision : les options, puis mon avis. Répondre par la lettre, par « avis », ou corriger.

### Avant le lot 11

**L11-D1. Ordre des chantiers.**
- A. Les positions d'abord : lots 11 à 14 ci-dessus, puis l'alpha.
- B. La tranche d'abord, avec des positions seulement visuelles ; les positions ensuite.
- *Avis* : A (B ferait tout refaire, et le cadrage le déconseille). Avec A, je commence 11.1 dès ton accord.

### Avant le moteur spatial (11.3), à trancher dans la PR du 11.2

**L11-D2. Visée des compétences.**
- A. Ciblage : on choisit une cible, la compétence part si elle est à portée.
- B. Visée libre : chaque compétence part dans la direction visée ou sur le point choisi.
- C. Hybride : cible choisie pour les coups à cible unique ; direction du personnage pour les cônes et les charges ; point au sol pour quelques zones (surtout la future classe à distance) ; aide à la visée sur mobile.
- *Avis* : C, dynamique sur PC et jouable au pouce.

**L11-D3. Se déplacer en garde et en lançant.**
- A. Garde immobile.
- B. Garde en marchant, ralentie (moitié de la vitesse, valeur provisoire) ; parade possible en mouvement ; compétences du Bastion lancées sans s'arrêter ; les incantations longues (classes futures) s'interrompent au déplacement.
- C. Garde à pleine vitesse.
- *Avis* : B.

**L11-D4. Esquive et mobilité.**
- A. Pas d'esquive dédiée : on sort d'une zone en se déplaçant ; la mobilité vient des compétences de chaque classe ; le Bastion avance en garde vers les ennemis à distance.
- B. Une esquive commune à toutes les classes (roulade ou pas de côté), avec un coût en endurance et une recharge.
- *Avis* : A, qui garde la parade au centre du Bastion. Revoir après l'essai 11.8 : si marcher ne suffit pas, passer à B.

**L11-D5. Collisions.** Le terrain bloque toujours, les créatures ne se superposent pas, les joueurs se traversent entre eux. Entre joueurs et créatures :
- A. Blocage ferme : le Bastion peut barrer physiquement un passage.
- B. Séparation douce : on se pousse, on ne bloque pas.
- *Avis* : B. Le blocage ferme (« tenir le passage ») serait plutôt une compétence ou une posture du Bastion, à étudier.

**L11-D6. Hauteur et saut.**
- A. Combat sur un plan au sol, avec des étages séparés (ponts, souterrains) ; pas de saut ni de chute en combat.
- B. 3D complète avec saut.
- *Avis* : A ; le saut d'exploration est à rediscuter au lot 12.

**L11-D7. Les attaques de zone des ennemis.**
- A. On les évite seulement.
- B. On les évite, ou on les encaisse en garde (dégâts réduits) ; la parade parfaite reste réservée aux coups dirigés (frappes, projectiles).
- C. Tout se pare.
- *Avis* : B, qui prolonge les règles actuelles (attaques parables ou non, déjà en données).

### Avant le lot 12

**L11-D8. Architecture du monde partagé** (rupture difficile à défaire : écart E5 du cadrage).
- A. **Une simulation déterministe par zone**, sur le serveur, au même tick et avec le même cœur ; les combats sur place font partie de cette simulation. En solo, le jeu héberge la zone sur le poste. Le réseau du monde envoie l'état filtré par zone d'intérêt, avec prédiction du joueur local. Le rejeu intégral du lot 10 reste pour les combats fermés (instances, duels) et les essais.
- B. **Rencontres séparées** : le monde crée un combat à composition fixe par rencontre (première version du plan). C'est plus simple au début, mais rejoindre un combat en cours, les boss de monde et la région partagée y entrent mal.
- *Avis* : A.

**L11-D9. Contenus de groupe** (écart E1).
- A. Garder « aucun groupe imposé pour la moindre activité » : tout contenu collectif a sa version solo.
- B. « Aucune voie essentielle de progression n'exige un groupe ; des contenus collectifs dédiés, annoncés comme tels, peuvent exister. »
- *Avis* : B, qui est ce que dit ton cadrage (Q14, Q23).

**L11-D10. Recommandations de la tranche à confirmer** :
- Q2, la mission ;
- Q3, niveaux 1 à 10 ;
- Q4, quatre emplacements ;
- Q5, 24 places ;
- Q7, potions limitées ;
- Q10, sauvegarde locale pour la tranche ;
- Q12, un raccourci et une manifestation ;
- Q20, attributs par classe et niveau.

« Comme les recommandations » suffit.

### Pratique et budget

**L11-D11. Téléphone d'essai.** As-tu un téléphone Android pour le 11.7 ? Le module Android d'Unity est gratuit. Sans téléphone, le premier essai tactile passe au lot 14. Un iPhone demanderait un Mac et un compte de développeur Apple payant.

**L11-D12. Budget et horizon.**
- Plafond mensuel pour un serveur d'essai sur Internet : à fixer avant le lot 13 s'il doit être joué à distance.
- Enveloppe pour les ressources artistiques : à fixer avant l'alpha.
- Date ou rythme souhaité pour les premiers testeurs (Q40).

Aucune dépense n'est engagée sans ton accord.
