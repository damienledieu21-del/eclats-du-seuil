#!/bin/bash
# Rend les vues une par une (un processus par vue, relançable : en mode final, saute les vues déjà rendues).
#   bash scripts/run_renders.sh essai|final [vues...]
cd "$(dirname "$0")/.."
MODE=${1:-essai}; shift
VUES=${@:-01_facade_orthographique 02_cote_droit_orthographique 03_cote_gauche_orthographique 04_arriere_orthographique 05_vue_trois_quarts 06_vue_de_dessus_toiture 07_interieur_boutique}
mkdir -p logs
for v in $VUES; do
  if [ "$MODE" = "final" ] && [ -f "renders/$v.png" ] && [ -z "$FORCE" ]; then echo "déjà fait : $v"; continue; fi
  /tmp/bvenv/bin/python -u -I scripts/render_view.py "$v" "$MODE" > "logs/rendu_${MODE}_$v.log" 2>&1
  grep -E "RENDU|Error" "logs/rendu_${MODE}_$v.log"
done
