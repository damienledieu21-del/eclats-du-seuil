#!/usr/bin/env bash
# Lance les tests d'une ou plusieurs catégories et échoue si l'une d'elles n'exécute aucun test (correctif 6.1).
# « dotnet test --filter » réussit silencieusement quand le filtre ne correspond à rien : une recette de lot pouvait
# donc passer au vert sans avoir rien testé.
# Sous-lot 8.1 : un projet de tests qui déclare la catégorie dans ses sources doit aussi en exécuter au moins un test (une
# catégorie mal orthographiée dans tout un projet passait inaperçue tant que les autres projets en avaient).
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

status=0
for category in "${categories[@]}"; do
  output=$(dotnet test --no-build -c "$configuration" --filter "Category=$category" "$@" 2>&1) || { echo "$output"; echo "Échec : catégorie $category" >&2; status=1; continue; }
  # Somme des « Total: N » de chaque projet de tests (projets sans test correspondant : aucune ligne).
  total=$( (grep -oE 'Total: *[0-9]+' <<<"$output" || true) | (grep -oE '[0-9]+' || true) | awk '{s += $1} END {print s + 0}')
  if [[ "$total" -eq 0 ]]; then
    echo "Échec : aucun test dans la catégorie « $category »." >&2
    status=1
    continue
  fi
  counts=()
  for project in "$root"/tests/*/; do
    name=$(basename "$project")
    grep -rqsF --include='*.cs' --exclude-dir=bin --exclude-dir=obj "Trait(\"Category\", \"$category\")" "$project" || continue
    count=$( (grep -oE "Total: *[0-9]+, Duration: [^-]* - $name\.dll" <<<"$output" || true) | (grep -oE 'Total: *[0-9]+' || true) \
      | (grep -oE '[0-9]+' || true) | awk '{s += $1} END {print s + 0}')
    if [[ "$count" -eq 0 ]]; then
      echo "Échec : $name déclare la catégorie « $category » mais n'en exécute aucun test." >&2
      status=1
    fi
    counts+=("$name $count")
  done
  echo "Catégorie $category : $total test(s) réussi(s) ($(IFS=,; echo "${counts[*]}" | sed 's/,/, /g'))."
done
exit $status
