# Consignes pour les agents — Éclats du Seuil

Les agents Claude suivent [CLAUDE.md](CLAUDE.md). Ce fichier fixe les règles de revue appliquées par Codex sur les pull requests. Le contexte du jeu, le lot en cours et les décisions validées sont dans [docs/FEUILLE_DE_ROUTE.md](docs/FEUILLE_DE_ROUTE.md) ; la vision du jeu complet et le statut de chaque choix (retenu, recommandation, ouvert) dans [docs/CADRAGE.md](docs/CADRAGE.md).

## Code Review Rules

Rédiger la revue en français. Signaler en priorité, avec le fichier et la ligne :

1. **Déterminisme** : tout ce qui peut faire varier un résultat ou un journal entre deux exécutions (horloge réelle, `float`/`double` dans le combat, ordre d'un dictionnaire ou d'un `HashSet` qui sort dans un journal, état global ou statique mutable, RNG autre que `Pcg32` injecté).
2. **Arithmétique** : opération pouvant déborder sans `checked` ou `IntMath`, arrondi non défini, valeur non bornée qui échappe au précontrôle numérique (`CombatSetup.ValidateNumericLimits`).
3. **Atomicité** : une commande ou une réaction refusée qui modifie quand même l'état (ressource, recharge, tirage aléatoire, file) ; un effet partiellement appliqué ; un budget ou une capacité de file non réservé avant mutation.
4. **Bornes** : file d'événements, déclencheurs, profondeur ou cycle de réactions pouvant croître sans limite ; boucle sans terminaison prouvée.
5. **Compatibilité** : dans `src/EdS.Domain`, `src/EdS.Simulation` et `src/EdS.Data` (`netstandard2.1`, C# 9), toute dépendance externe ou API absente de .NET Standard 2.1.
6. **Formats** : changement de journal, de schéma de règles ou de classes, ou d'empreinte sans hausse de version, sans test de migration ou de refus, ou avec un journal de référence historique modifié au lieu d'un nouveau journal figé.
7. **Validation** : donnée invalide acceptée silencieusement, clé de modificateur ou de déclencheur acceptée alors qu'elle ne produit aucun effet, erreur sans chemin précis.
8. **Décisions de gameplay** : comportement qui contredit une décision listée dans `docs/FEUILLE_DE_ROUTE.md`, un choix « Retenu » de `docs/CADRAGE.md` ou la documentation du moteur, sans que la PR le signale explicitement ; recommandation présentée comme une décision de Damien.
9. **Tests** : comportement nouveau sans test, test qui ne peut pas échouer, catégorie de tests absente de `ci.yml` ou de `scripts/test-category.sh`.

Ne pas signaler : le style, les noms en français, la longueur des commentaires, les valeurs d'équilibrage provisoires (elles sont volontairement non équilibrées tant que le sous-lot 8.7 n'est pas fait).
