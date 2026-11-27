# Performances — audit V2.53 (27 novembre 2026)

Demande de Damien : le jeu saccade sur son PC (GTX 470 à 1224 Mo, 4 Go de RAM, classé « PC modeste » par `MachineTier.Detect`) depuis l'ajout des galeries, grottes noyées, Envers, Bourg de 119 maisons, égouts et crypte. Audit **par lecture du code** de `unity/Assets/EdS` et `src/EdS.Runtime` : Unity n'a pas été lancé, **aucun chiffre ci-dessous n'est mesuré** ; les poids sont des estimations à confirmer ce soir au Profiler.

## 1. Corrigé dans cette PR

| # | Cause | Poids estimé | Correction | Règle testée |
|---|---|---|---|---|
| 1 | **Le réglage Qualité (#247) ne touche pas le rendu** : il ne règle que les textures (mipmaps), le chargement par cases et les ressources lourdes. Un PC modeste garde le rendu de l'essai réaliste : ombres du soleil sur **90 m en 4 cascades, carte 4096**, ombres **douces**, **MSAA ×4**, **occlusion ambiante de l'écran (SSAO)**. | Le plus gros gain attendu côté carte graphique : chaque objet qui projette une ombre est redessiné dans chaque cascade (jusqu'à 4 fois) ; la SSAO est une passe plein écran ; carte 4096 = 64 Mo de mémoire vidéo sur 1224. | Rendu **`EdS_URP_Modeste`** créé par la préparation (menu *Préparer l'essai de style*) : **50 m, 2 cascades, carte 2048, ombres dures, MSAA ×2, sans SSAO** ; 8 lumières par objet gardées (intérieurs), image et profondeur de la scène gardées (eau, failles). Choisi au lancement quand la classe est « PC modeste » (Automatique sur le PC de Damien, ou réglage **Basse**). Dans l'éditeur, le rendu du projet revient à la sortie du mode Jeu. | `PerfRules.Render` (`V253PerfsTests`) |
| 2 | **Feux des intérieurs à ombres douces** sur tout ordinateur (lumière ponctuelle : 6 vues de la pièce à chaque image). | Fort à l'intérieur (maisons, taverne, crypte avec feux). | Sans ombre sur un PC modeste. | `PerfRules.FireShadows` |
| 3 | **Petits objets qui projettent une ombre** (bougies, gobelets, cailloux, petites caisses, lanternes) : tous les modèles posés gardent l'ombre de leur prefab. | Des centaines de petits objets dessinés dans chaque cascade. | Sur PC modeste et téléphone, un objet de moins de **0,6 m** (plus grand côté) ne projette plus d'ombre ; les corps animés gardent la leur ; rien ne change sur le PC complet. Une passe au chargement de la zone (`SpaceView.SmallCasters`). | `PerfRules.CastsShadow` |
| 4 | **Décors qui cachent le joueur évalués à chaque image** : un `Occluder` par renderer (une maison du Bourg en a des dizaines : estimation de 3 000 à 5 000 dans le Bourg) ; pour chacun, test d'objet Unity détruit, trois segments, puis **une écriture `renderer.enabled` ou `localScale` par décor et par image**, même sans changement. | Côté processeur : quelques milliers d'appels au moteur par image (estimation 0,5 à 2 ms sur un vieux processeur). | Boîte des segments caméra-corps calculée une fois par image ; un décor au repos hors de cette boîte est sauté sans appel à Unity ; écritures seulement quand l'état change. Aucun changement visuel (propriété testée : jamais un décor qui cache le joueur n'est sauté). | `OccluderRules.Reach`, `OccluderRules.Overlap` |
| 5 | Pas de mesure en jeu. | — | **F9** : images par seconde, temps moyen et pire image sur 2 s, saccades (images de plus de 50 ms) depuis le lancement, classe de la machine et rendu utilisé. Tableau fixe, texte refait deux fois par seconde (aucune allocation par image). | `FrameStats` |

**À faire par Damien avant l'essai** : relancer **Éclats du Seuil → Préparer l'essai de style** (crée `Assets/Style/Generated/EdS_URP_Modeste` et le pose au catalogue ; la fenêtre de fin dit « rendu du PC modeste : oui »).

## 2. Reste à faire, classé par gain attendu

1. **Allègements « téléphone » qui ne s'appliquent pas au PC modeste** (ils testent `Application.isMobilePlatform` au lieu de la classe) : densité de l'herbe (×1 au lieu de ÷5, touffes de 16 brins au lieu de 8, distance pleine ; `StyleGrass.Create`), blé (`SpaceView.cs`, pas de 0,32 m), villageois (`catalog.Villagers` au lieu d'un tiers), bétail, halo (bloom), sonde de reflets en 512, erreur de relief du terrain (3 au lieu de 8), eau du sample, particules (`Effects.cs`). **Changement visuel** sur le PC modeste : à décider avec Damien après mesure (proposition : herbe ÷2 et bloom coupé sur PC modeste). Règle à écrire : `PerfRules.Reduced(tier)` existe déjà.
2. **Bourg construit d'un bloc au chargement** (`SpaceView.BourgDecor`) : 119 maisons, chacune instanciée deux fois (détaillée et lointaine), colliders détruits après coup (`Object.Destroy` par collider), vitres de nuit. Saccade **au chargement** de la zone, pas en jeu. Piste : construction par cases comme `CellStreamView` (budget en ms par image), ou prefabs sans colliders préparés par le menu.
3. **Ombres des maisons lointaines** : la version lointaine (LOD1) projette aussi une ombre ; avec 50 m d'ombres c'est moins grave. Piste : `shadowCastingMode = Off` sur le LOD1 au-delà de la distance d'ombre (sans effet visible).
4. **Envers et lieux de l'Envers par cases** (`SpaceView.Envers.cs:367`, `SpaceView.EnversLieux.cs:55`) : à chaque case quittée, `_occluders.Where(o => o.Transform.IsChildOf(cell)).ToList()` puis `List.Remove` (parcours de toute la liste pour chaque décor) : O(n²) à chaque changement de case, petite saccade en marchant. Piste : une liste de décors par case. Les cases chargées après coup ne passent pas non plus par `SmallCasters`.
5. **Matériaux copiés** (`r.materials` dans `GearLook`, `StyleLook`, `CompanionView`) : une copie par personnage et par matériau, à la création (suivies par `MaterialCopies`). Avec le SRP Batcher d'URP, des matériaux différents du même shader restent groupés : gain faible, à vérifier seulement si le Profiler montre beaucoup de *SetPass calls*.
6. **Petites allocations régulières** : tri des lanternes 4 fois par seconde (deux listes et un `Select(...).ToList()`, `SpaceView.Night.cs`), textes de l'interface refaits à chaque image (`string.Format` des indications, horloge de l'entraînement). Quelques centaines d'octets par image : ramasse-miettes toutes les quelques secondes, à confirmer (colonne GC Alloc).
7. **Pas de regroupement statique** du décor posé (`StaticBatchingUtility`) ni d'instancing GPU pour les maisons et accessoires répétés : à essayer seulement si les *Batches* dépassent quelques milliers.
8. Textures : déjà au quart sur PC modeste (`globalTextureMipmapLimit` = 2) ; rien à faire avant mesure.

## 3. Ce soir avec Damien : quoi regarder et où

Avant : *Préparer l'essai de style* ; réglage Qualité sur **Automatique** ; construire ou jouer dans l'éditeur (l'éditeur coûte plus cher : noter les deux si possible).

**A. F9 en jeu** (relever « i/s », « ms », « pire », « saccades », la classe et le rendu affichés : il doit dire « PC modeste · EdS_URP_Modeste ») à ces six endroits, 20 s immobile puis 20 s en marchant :
1. Pré des Vallons en plein jour ; 2. place du marché du Bourg, de jour puis de nuit (lanternes) ; 3. dans une maison avec un feu ; 4. égouts ; 5. crypte ; 6. Envers (en traversant plusieurs cases). Puis refaire 1 et 2 en Qualité **Haute** (relancer le jeu) pour comparer avec l'ancien rendu.

**B. Profiler** (*Window → Analysis → Profiler*, cocher *Deep Profile* seulement pour une image précise), sur le Bourg de nuit puis l'Envers :
- **CPU Usage**, vue *Hierarchy*, trier par *Self ms* : `PlayerLoop → Update.ScriptRunBehaviourUpdate → EdsGame.Update`, et dedans `ArenaView.SyncSpace` → `SpaceView.Cut` (décors qui cachent le joueur, herbe, eau, passants) ; `LateUpdate` de l'interface ; `Camera.Render` et `Shadows.RenderShadowMap` / *MainLightShadow*.
- Colonne **GC Alloc** de l'image : viser moins de 1 Ko ; noter les fonctions qui allouent. Les pics **GC.Collect** dans le graphe sont des saccades.
- Les pics isolés (cliquer dessus) en marchant : chercher `CellStreamView`, `Instantiate`, `Destroy`, `IsChildOf` (cases de l'Envers).
- **Rendering** : *Batches*, *SetPass calls*, *Shadow casters*, *Triangles* (aussi dans *Stats* de la vue Game).
- **GPU Usage** (si la GTX 470 le permet en DirectX 11) : part des ombres, de l'opaque, du post-traitement.
- **Memory** : mémoire des textures et des maillages contre les 1224 Mo de la carte.

**C. Frame Debugger** (*Window → Analysis → Frame Debugger*) sur le Bourg : nombre de passes d'ombre (2 attendues avec le rendu modeste, 4 avant), présence de la SSAO (absente attendue), nombre de dessins de l'herbe.

**Décisions à prendre avec les chiffres** : allègements visuels de la liste 2.1 pour le PC modeste (herbe, bloom, villageois) ; construction du Bourg par cases si le chargement saccade.
