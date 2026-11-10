#!/bin/bash
# Reconstruit tout le modèle (étapes A à D, chacune relançable seule) : environ 2 minutes.
cd "$(dirname "$0")/.."; mkdir -p logs
for s in build_archi build_interieur build_props build_scene; do
  /tmp/bvenv/bin/python -u -I scripts/$s.py > logs/$s.log 2>&1 || { echo "ÉCHEC $s"; tail -5 logs/$s.log; exit 1; }
  grep -E "^FIN" logs/$s.log
done
