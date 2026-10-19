#!/usr/bin/env bash
# Lance les tests d'une ou plusieurs catégories et échoue si l'une d'elles n'exécute aucun test (correctif 6.1).
# « dotnet test --filter » réussit silencieusement quand le filtre ne correspond à rien : une recette de lot pouvait
# donc passer au vert sans avoir rien testé.
# Sous-lot 8.1 :
# - toute catégorie citée dans les sources des tests doit être connue (liste ci-dessous) : une catégorie mal orthographiée
#   sortait sans bruit de son filtre ;
# - un projet de tests qui déclare une catégorie demandée doit en exécuter au moins un test (une classe filtrée ou une
#   déclaration morte ne passent plus inaperçues tant que les autres projets en ont).
# Usage : scripts/test-category.sh [-c Release] Catégorie [Catégorie…]   (options dotnet test supplémentaires après --)
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"

configuration=Debug
if [[ "${1:-}" == "-c" ]]; then configuration="$2"; shift 2; fi
categories=()
while [[ $# -gt 0 && "$1" != "--" ]]; do categories+=("$1"); shift; done
[[ "${1:-}" == "--" ]] && shift
if [[ ${#categories[@]} -eq 0 ]]; then
  echo "Usage : $0 [-c Release] Catégorie [Catégorie…] [-- options dotnet test]" >&2
  exit 1
fi

known=(Numeric Formula Data Build Engine Resources Regression Limits Status Trigger Modifier Shield Support Bastion Scenario Runtime Network Time)
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
  output=$(dotnet test --no-build -c "$configuration" --filter "Category=$category" "$@" 2>&1) || { echo "$output"; echo "Échec : catégorie $category" >&2; status=1; continue; }
  # Somme des « Total: N » de chaque projet de tests (projets sans test correspondant : aucune ligne).
  total=$( (grep -oE 'Total: *[0-9]+' <<<"$output" || true) | (grep -oE '[0-9]+' || true) | awk '{s += $1} END {print s + 0}')
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
    count=$( (grep -oE "Total: *[0-9]+, Duration: [^-]* - $name\.dll" <<<"$output" || true) | (grep -oE 'Total: *[0-9]+' || true) \
      | (grep -oE '[0-9]+' || true) | awk '{s += $1} END {print s + 0}')
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
