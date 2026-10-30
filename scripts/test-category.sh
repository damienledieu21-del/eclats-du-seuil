#!/usr/bin/env bash
# Lance les tests d'une ou plusieurs catégories et échoue si l'une d'elles n'exécute aucun test (correctif 6.1).
# « dotnet test --filter » réussit silencieusement quand le filtre ne correspond à rien : une recette de lot pouvait
# donc passer au vert sans avoir rien testé.
# Sous-lot 8.1 :
# - toute catégorie citée dans les sources des tests doit être connue (liste ci-dessous) : une catégorie mal orthographiée
#   sortait sans bruit de son filtre ;
# - un projet de tests qui déclare une catégorie demandée doit en exécuter au moins un test (une classe filtrée ou une
#   déclaration morte ne passent plus inaperçues tant que les autres projets en ont).
# Tests plus rapides (décision de Damien du 29 octobre 2026) : avec --resultats DOSSIER, les tests ne sont pas relancés. Ils ont déjà
# été exécutés par une exécution complète de la même construction, qui a écrit ses résultats TRX dans DOSSIER (« dotnet test --no-build
# -c … --logger trx --results-directory DOSSIER ») ; pour chaque catégorie, la liste de ses tests (« dotnet test --list-tests --filter
# Category=… », même filtre, sans rien exécuter) est rapprochée de ces résultats par scripts/category_results.py : résultats des mêmes
# assemblages (mêmes chemins), écrits après leur dernière construction ; chaque test de la catégorie doit y avoir un résultat réussi ;
# les comptes (total, par projet) sont ceux qu'aurait donnés le filtre. Mêmes contrôles de catégories, mêmes comptes, mêmes messages.
# Revue de la PR #73 : résultats périmés si un fichier du dossier de sortie d'un assemblage de tests (dépendances, données copiées)
# a changé depuis le début de l'exécution ; test ignoré (Skip) refusé ; chaque test listé rapproché des résultats de son assemblage.
# Sans --resultats, les tests de chaque catégorie sont exécutés comme avant.
# Usage : scripts/test-category.sh [-c Release] [--resultats DOSSIER] Catégorie [Catégorie…]   (options dotnet test supplémentaires après --)
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"

configuration=Debug
results=
while [[ $# -gt 0 ]]; do
  case "$1" in
    -c) configuration="$2"; shift 2 ;;
    --resultats) results="$2"; shift 2 ;;
    *) break ;;
  esac
done
categories=()
while [[ $# -gt 0 && "$1" != "--" ]]; do categories+=("$1"); shift; done
[[ "${1:-}" == "--" ]] && shift
if [[ ${#categories[@]} -eq 0 ]]; then
  echo "Usage : $0 [-c Release] [--resultats DOSSIER] Catégorie [Catégorie…] [-- options dotnet test]" >&2
  exit 1
fi
if [[ -n "$results" ]]; then
  if [[ ! -d "$results" ]]; then
    echo "Échec : dossier de résultats « $results » introuvable." >&2
    exit 1
  fi
  echo "Recettes par catégorie : tests listés par catégorie, rapprochés des résultats de l'exécution complète ($results)."
fi

known=(Numeric Formula Data Build Engine Resources Regression Limits Status Trigger Modifier Shield Support Bastion Scenario Runtime Network Time Space Presence Behavior Scene Duo World)
is_known() { local k; for k in "${known[@]}"; do [[ "$k" == "$1" ]] && return 0; done; return 1; }
status=0
declared=$( (grep -rhoE --include='*.cs' --exclude-dir=bin --exclude-dir=obj 'Trait\("Category", *"[^"]*"\)' "$root/tests" || true) \
  | sed -E 's/.*"([^"]*)"\)$/\1/' | sort -u)
while IFS= read -r category; do
  [[ -z "$category" ]] && continue
  if ! is_known "$category"; then
    echo "Échec : catégorie inconnue « $category » dans les tests (connues : ${known[*]})." >&2
    status=1
  fi
done <<<"$declared"
for category in "${categories[@]}"; do
  if ! is_known "$category"; then
    echo "Échec : catégorie demandée inconnue « $category » (connues : ${known[*]})." >&2
    status=1
    continue
  fi
  if [[ -z "$results" ]]; then
    output=$(dotnet test --no-build -c "$configuration" --filter "Category=$category" "$@" 2>&1) || { echo "$output"; echo "Échec : catégorie $category" >&2; status=1; continue; }
    # Somme des « Total: N » de chaque projet de tests (projets sans test correspondant : aucune ligne).
    total=$( (grep -oE 'Total: *[0-9]+' <<<"$output" || true) | (grep -oE '[0-9]+' || true) | awk '{s += $1} END {print s + 0}')
  else
    # -m:1 : un projet de tests après l'autre, pour que chaque test listé suive la ligne « Test run for » de son assemblage (en
    # parallèle, les lignes des projets s'entremêlent).
    listing=$(dotnet test --no-build -c "$configuration" -m:1 --list-tests --filter "Category=$category" "$@" 2>&1) \
      || { echo "$listing"; echo "Échec : catégorie $category (liste des tests)" >&2; status=1; continue; }
    # Une ligne « Projet N » par projet de tests (sortie standard) : résultats de la catégorie dans l'exécution complète. Les messages
    # d'erreur vont tels quels sur la sortie d'erreur, hors des comptes ; -I : Python isolé (ni dossier courant, ni variables PYTHON*).
    output=$(python3 -I "$root/scripts/category_results.py" "$results" "$configuration" <<<"$listing") \
      || { echo "Échec : catégorie $category" >&2; status=1; continue; }
    total=$(awk '{s += $2} END {print s + 0}' <<<"$output")
  fi
  if [[ "$total" -eq 0 ]]; then
    echo "Échec : aucun test dans la catégorie « $category »." >&2
    status=1
    continue
  fi
  counts=()
  failed=0
  for project in "$root"/tests/*/; do
    name=$(basename "$project")
    grep -rqsF --include='*.cs' --exclude-dir=bin --exclude-dir=obj "Trait(\"Category\", \"$category\")" "$project" || continue
    if [[ -z "$results" ]]; then
      count=$( (grep -oE "Total: *[0-9]+, Duration: [^-]* - $name\.dll" <<<"$output" || true) | (grep -oE 'Total: *[0-9]+' || true) \
        | (grep -oE '[0-9]+' || true) | awk '{s += $1} END {print s + 0}')
    else
      count=$(awk -v project="$name" '$1 == project {s += $2} END {print s + 0}' <<<"$output")
    fi
    if [[ "$count" -eq 0 ]]; then
      echo "Échec : $name déclare la catégorie « $category » mais n'en exécute aucun test." >&2
      status=1
      failed=1
    fi
    counts+=("$name $count")
  done
  summary=$(IFS=,; echo "${counts[*]}" | sed 's/,/, /g')
  if [[ "$failed" -eq 0 ]]; then
    echo "Catégorie $category : $total test(s) réussi(s) ($summary)."
  else
    echo "Catégorie $category : $total test(s) exécuté(s), projet(s) sans test ($summary)." >&2
  fi
done
exit $status
