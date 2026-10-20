# Règles du combat dans l'espace (sous-lot 11.3)

Proposition de Claude du 20 octobre 2026, **validée par Damien le 20 octobre 2026 à 01 h 48 (« pour les 9 questions on garde tes avis »)**, avant le moteur spatial (11.4). Elle applique les décisions L11-D2 à L11-D7 du 20 octobre 2026 ([registre](CADRAGE.md#5-décisions-du-20-octobre-2026-l11-d1-à-l11-d12)) et l'architecture du 11.2 ([ARCHITECTURE_MONDE.md](ARCHITECTURE_MONDE.md)).

Ce qui est **décidé** est rappelé comme tel. Les cinq propositions de la section 10 sont **retenues** (marquées *Retenu*). Les autres choix marqués *Proposition* sont des choix techniques de Claude, réglables. Toutes les valeurs sont provisoires et seront des paramètres des données.

## 1. Espace

- **Unités** : le mètre, en virgule fixe (`Fixed`, 0,0001 m près) ; le temps en ticks de 50 ms. Aucun nombre à virgule flottante dans le moteur.
- **Plan au sol** (décidé, L11-D6) : une position est un point (x, z) sur une **surface praticable**. Les étages (pont, souterrain) sont des surfaces distinctes, reliées par des passages (rampe, escalier). Deux combattants à la même position (x, z) sur deux surfaces ne se touchent pas et ne sont pas à portée.
  - *Proposition* : la première démonstration n'a qu'une surface ; les pentes y sont visuelles (pas d'effet sur la vitesse).
- **Orientation** : chaque combattant a une direction (angle sur le plan). Elle sert aux cônes, à la couverture du bouclier et à l'affichage.
- **Emprise** : un cercle par combattant. *Retenu comme valeurs de départ* : rayon de 0,5 m pour le Bastion, 0,6 m pour un loup, 1,2 m pour un chef ; une donnée de chaque combattant.
- **Décor** : obstacles solides (murs, rochers, troncs), en segments et polygones convexes, donnés avec la zone. Ils arrêtent le déplacement et la ligne de vue des compétences à cible.
  - *Proposition* : au lot 11, ils n'arrêtent pas les zones au sol, que la conception place en conséquence.

## 2. Déplacement

- **Intention de déplacement** à chaque tick : une direction et une allure (marche). Le moteur applique la vitesse, le décor et les collisions ; personne ne se téléporte.
- **Vitesse de base** : *Retenu comme valeur de départ* : 5 m/s, celle du prototype Unity.
- **Garde en marchant** (décidé, L11-D3) : la garde ralentit le Bastion. La valeur se règle par essais ; l'essai 11.9 la fixe. *Retenu comme valeur de départ* : la moitié de la vitesse.
- **Parade et compétences en mouvement** : *Proposition* oui pour le Bastion ; seules les incantations longues des classes futures s'interrompront au déplacement.
- **Pas d'esquive universelle** (décidé, L11-D4) : on évite en marchant.
- **Règle de conception** (L11-D4), vérifiée par le validateur des données : une attaque ordinaire évitable laisse le temps d'en sortir au pas de base. Son annonce dure au moins le temps de traverser sa zone (rayon ÷ vitesse de base), plus un temps de réaction. *Retenu comme valeur de départ* : 300 ms de réaction.

## 3. Collisions

Décidé (L11-D5) :

- **décor** solide ;
- **joueurs entre eux** : ni blocage ni poussée passive ;
- **créatures entre elles** : séparation douce ou évitement, sans chaîne de poussées ;
- **déplacements forcés** (repousser, attirer) seulement par des attaques et capacités identifiables.

**Contact entre joueurs et créatures** : règle explicite, choisie après un essai dans la scène du 11.6 (plusieurs ennemis, un angle, une entrée étroite). Les deux règles candidates sont décrites dans le [plan, section 4.1](PLAN_LOT_11.md#41-contact-joueurcréature--proposition-à-éprouver-l11-d5) :

- **A**, volumes solides sans poussée, avec un garde-fou qui empêche une créature de fermer le dernier passage d'un joueur ;
- **B**, traversée freinée.

Le moteur du 11.4 réalise les deux, réglables par les données, pour que l'essai décide.

## 4. Ciblage et portée

Décidé (L11-D2) : ciblage mixte.

| Type | Visée | Contrôles du moteur |
|---|---|---|
| **Compétence à cible** (coups simples dirigés) | La cible sélectionnée | Au **lancement** : cible valide, vivante, sur la même surface, à portée (distance entre les bords des emprises), ligne de vue non coupée par le décor. Sinon refus motivé (`targetOutOfRange`, `noLineOfSight`, comme `targetDead`) |
| **Cône** | Orientation du lanceur | Forme posée au lancement, depuis la position et l'orientation du lanceur |
| **Point au sol** | Un point choisi, à portée | Le point doit être à portée et sur une surface praticable |
| **Autour de soi** | Le lanceur | Cercle centré sur le lanceur au moment de l'impact |

- **Verrouillage** (précisé selon L11-D2) : *Retenu* : cible, direction et point sont **verrouillés au lancement**. Un coup instantané du Bastion part et touche dans le même tick, comme aujourd'hui.
- **Sélection de cible en vue 2 et sur mobile** : *Proposition* : touche Tab et clic, comme aujourd'hui ; aide à la visée sur mobile (cible la plus proche dans la direction regardée) au 11.8.
- **La caméra ne change rien** : ni portée, ni zone, ni résolution (Q13).

## 5. Attaques annoncées des ennemis

Décidé (L11-D7) : la forme d'une attaque et ses défenses sont deux informations distinctes. Chaque attaque d'un ennemi déclare :

| Champ | Sens |
|---|---|
| `shape` | `target` (coup dirigé), `cone`, `circle` (autour d'un point), `line`, `persistent` (zone qui reste) |
| `telegraphMs` | Durée de l'annonce, visible dans les deux vues |
| `range`, `radius`, `angle`, `width` | Dimensions de la forme |
| `guardable` | La garde réduit-elle ses dégâts ? |
| `parryable` | La parade parfaite s'applique-t-elle ? |
| `persistentMs`, `tickMs` | Pour une zone qui reste : sa durée et sa cadence |

- **Résolution** : la forme et sa position sont **verrouillées au début de l'annonce**, puis l'attaque frappe au tick d'impact tout ce qui se trouve dedans **à ce tick**, frontière exacte. Un joueur sorti à temps n'est pas touché.
- **Convention de la démonstration** (décidée, L11-D7) :
  - coup dirigé : garde et parade selon les règles existantes ;
  - souffle ou onde : garde seulement si l'attaque le dit, pas de parade ;
  - zone persistante : il faut en sortir, la garde seule ne suffit pas ;
  - attaque imparable : retour visuel explicite, on y répond en se déplaçant.
- **Annonce en réseau** : le tick d'impact est envoyé au client, qui affiche le compte à rebours avancé du temps aller-retour (option B généralisée, [ARCHITECTURE_MONDE.md](ARCHITECTURE_MONDE.md#5-entrée-dans-une-zone-état-courant-mises-à-jour)).

## 6. Garde, parade et couverture du bouclier

Décidé (L11-D3) : la couverture du bouclier est décrite explicitement, sans protection à 360° implicite.

- *Retenu* : la garde et la parade parfaite ne valent que contre un coup venu de l'**arc frontal** du Bastion, de **180°** centré sur son orientation (paramètre des règles). Un coup reçu de dos n'est ni gardé ni paré : dégâts pleins.
- **Origine d'un coup** : la position de l'attaquant pour un coup dirigé ou un projectile ; le centre de la forme pour une zone.
- *Retenu* : **orientation en garde**. En garde, le Bastion se tourne vers sa cible sélectionnée ; sans cible, il garde l'orientation de son déplacement. C'est simple au clavier comme au pouce, et le placement compte : un ennemi qui le contourne le frappe de dos.
- Coûts d'endurance, rupture de garde et fenêtre de parade (150 ms) sont inchangés.

## 7. Le Bastion dans l'espace

*Retenu* : des portées pour chaque compétence. Elles changent le sens de certaines fiches du catalogue (« tous les ennemis » devient « tous les ennemis dans le rayon »).

| Compétence ou talent | Portée ou forme proposée |
|---|---|
| Frappe du rempart, Heurt de bouclier, Contrecoup | Mêlée : cible à 2,5 m |
| Onde de rupture | Cercle de 5 m autour du Bastion (au lieu de tous les ennemis) |
| Provocation | Cible à 15 m |
| Barrière d'acier | Soi, ou un allié à 15 m |
| Bastion absolu | Provoque les assaillants à 15 m ; −30 % de dégâts pour les alliés à 15 m |
| Riposte précise (parade parfaite) | Frappe l'attaquant s'il est à 2,5 m ; contre un coup à distance, pas de riposte |
| Jugement du colosse | Frappe de zone : cercle de 5 m autour du Bastion |
| Serment du gardien | Provoque l'assaillant qui frappe l'allié lié, s'il est à 15 m du Bastion |
| Lien protecteur | Lien actif tant que l'allié est à 15 m |

- **Ennemis à distance** : sans compétence de rapprochement (L11-D4), le Bastion avance en garde vers eux ; la Provocation les fait venir à lui.

## 8. Formats prévus au 11.4

| Format | Évolution prévue |
|---|---|
| Moteur | 0.16.0 |
| Journal | Format 10 : positions dans les lignes utiles (`combatant`, déplacements regroupés par tick, lancers, impacts), refus de portée et de ligne de vue |
| Scénarios | Schéma 5 : positions et orientations de départ, surface, décor, emprises |
| Classes | Schéma 9 : portée et forme des compétences |
| Règles | Schéma 10 : vitesse de base, ralentissement en garde, arc du bouclier, règle de contact (A ou B), temps de réaction minimal des annonces |
| Attaques d'assaillant | Forme, annonce et défenses (section 5) |

Comme au 11.1, les journaux des scénarios livrés sans positions sont comparés à ceux du moteur précédent ; le combat sans positions reste reproductible.

## 9. Preuves prévues

- **Portée et esquive réelles** : au tick près, à la frontière exacte ; propriétés sur positions aléatoires ; en réseau, esquive mesurée de 0 à 200 ms d'un sens (11.7).
- **Défenses par attaque** : seules les défenses déclarées s'appliquent ; un coup de dos n'est ni gardé ni paré ; la garde ne neutralise pas une zone persistante.
- **Annonces évitables** : le validateur refuse une attaque ordinaire évitable dont l'annonce est trop courte pour en sortir au pas de base.
- **Caméra sans effet** : le même combat scripté donne le même journal dans les deux vues.
- **Contact** : les deux règles candidates testées (aucun chevauchement en A, aucun blocage en B, aucun piège dans un angle en A).

## 10. Retenu par Damien le 20 octobre 2026

1. **Arc du bouclier** : garde et parade valent contre les coups venus des 180° de devant ; de dos, dégâts pleins. **Retenu.**
2. **Orientation en garde** : vers la cible sélectionnée, sinon dans le sens du déplacement. **Retenu.**
3. **Portées du Bastion** (section 7), dont Onde de rupture limitée à 5 m et Riposte précise en mêlée seulement. **Retenu.**
4. **Verrouillage au lancement** de la cible, de la direction et du point. **Retenu.**
5. **Valeurs de départ des essais** : vitesse de 5 m/s, garde à la moitié, réaction de 300 ms, emprises de 0,5 à 1,2 m. **Retenu**, à régler au 11.9.

Le contact entre joueurs et créatures (A ou B) se choisit après l'essai du 11.6, pas maintenant.
