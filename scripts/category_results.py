#!/usr/bin/env python3
"""Tests plus rapides (octobre 2026) : recette d'une catégorie à partir des résultats d'une exécution complète.

Usage : python3 -I scripts/category_results.py DOSSIER_RESULTATS CONFIGURATION < liste

L'entrée standard est la sortie de « dotnet test --no-build -c CONFIGURATION -m:1 --list-tests --filter Category=X » : pour chaque
projet de tests, une ligne « Test run for CHEMIN.dll (…) » puis les tests de la catégorie dans ce projet, un par ligne, en retrait de
quatre espaces. « -m:1 » liste les projets l'un après l'autre : sans lui, ils sont listés en parallèle et leurs lignes s'entremêlent
(un nom ne suit plus la ligne « Test run for » de son projet). Chaque nom listé est rapproché des résultats de son propre assemblage ;
une méthode listée qui a des résultats dans plusieurs assemblages est refusée (ambiguë, aucun cas dans le dépôt).
DOSSIER_RESULTATS contient les fichiers TRX écrits par l'exécution complète de la même construction (« dotnet test --no-build
-c CONFIGURATION --logger trx --results-directory DOSSIER_RESULTATS »), un par projet de tests.

Écrit, pour chaque projet de tests, une ligne « Projet N » : le nombre de résultats de la catégorie dans ce projet, c'est-à-dire ce
que « dotnet test --filter Category=X » aurait exécuté (un filtre de catégorie choisit des méthodes : tous les cas d'une théorie
ont les catégories de leur méthode). Échoue (code 1, message sur la sortie d'erreur) si :
- les résultats sont absents, illisibles, en double pour un projet, ou d'une autre construction que celle listée (autre chemin,
  autre configuration) ;
- un fichier du dossier de sortie d'un assemblage de tests (la DLL, ses dépendances, les données copiées à côté, sous-dossiers
  compris) a été modifié après le début de l'exécution (résultats périmés) ;
- un nom de test ne se rattache pas à sa méthode, un nom listé précède toute ligne « Test run for », ou une méthode de la
  catégorie a des résultats dans plusieurs assemblages ;
- un test de la catégorie n'a pas de résultat dans son assemblage, a été ignoré (Skip), ou n'a pas réussi.
"""
from __future__ import annotations

import collections
from datetime import datetime
import os
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
# Seule issue acceptée : réussi. Un test ignoré (« NotExecuted », Skip) est refusé : une recette exige des tests exécutés ; aucun test du
# dépôt n'est ignoré (revue de la PR #73).
PASSED = "Passed"
SKIPPED = "NotExecuted"
TEST_RUN = re.compile(r"^Test run for (.+?\.dll) \(")


class Refused(Exception):
    pass


def as_in_trx(name: str) -> str:
    """
    Nom affiché tel que l'écrit le journal TRX : un caractère qu'il ne peut pas écrire tel quel en XML (caractère hors du plan de base,
    écrit en paire de substitution, contrôle, U+FFFE, U+FFFF) y est remplacé par sa séquence « \\uXXXX » (UTF-16, minuscules) ; la
    liste des tests l'affiche tel quel.
    """
    out = []
    for char in name:
        code = ord(char)
        if code > 0xFFFF:
            code -= 0x10000
            out.append("\\u%04x\\u%04x" % (0xD800 + (code >> 10), 0xDC00 + (code & 0x3FF)))
        elif (code < 0x20 and char not in "\t\n\r") or 0xD800 <= code <= 0xDFFF or code in (0xFFFE, 0xFFFF):
            out.append("\\u%04x" % code)
        else:
            out.append(char)
    return "".join(out)


def method_key(display_name: str) -> str:
    """Nom complet de la méthode d'un test : nom affiché sans ses arguments (« Espace.Classe.Méthode(x: 1) »)."""
    cut = display_name.find("(")
    return display_name if cut < 0 else display_name[:cut]


def run_start(root: ET.Element, file: Path) -> float:
    """Début de l'exécution (élément Times, attribut start, avec son fuseau), en secondes depuis l'origine Unix."""
    times = root.find("t:Times", NS)
    text = times.get("start") if times is not None else None
    if not text:
        raise Refused(f"{file.name} : début de l'exécution absent (Times start).")
    # Sept décimales dans les fichiers TRX : six suffisent (microsecondes) et toute version de Python 3 les lit.
    text = re.sub(r"(\.\d{6})\d+", r"\1", text)
    try:
        start = datetime.fromisoformat(text)
    except ValueError:
        raise Refused(f"{file.name} : début de l'exécution illisible ({text}).") from None
    if start.tzinfo is None:
        raise Refused(f"{file.name} : début de l'exécution sans fuseau horaire ({text}).")
    return start.timestamp()


def read_results(directory: Path, configuration: str) -> dict[str, tuple[str, float, list[tuple[str, str, str]]]]:
    """
    Résultats par assemblage de tests (chemin de la DLL) : (fichier, début de l'exécution, [(nom affiché, méthode, issue)]). Un seul
    fichier TRX par assemblage, de la configuration demandée.
    """
    files = sorted(directory.glob("*.trx"))
    if not files:
        raise Refused(f"aucun fichier de résultats (*.trx) dans {directory}.")
    segment = "/" + configuration.lower() + "/"
    assemblies: dict[str, tuple[str, float, list[tuple[str, str, str]]]] = {}
    for file in files:
        try:
            root = ET.parse(file).getroot()
        except ET.ParseError as error:
            raise Refused(f"{file.name} : fichier de résultats illisible ({error}).") from None
        tests: dict[str, str] = {}
        paths: set[str] = set()
        for unit in root.iterfind(".//t:UnitTest", NS):
            method = unit.find("t:TestMethod", NS)
            if method is None:
                raise Refused(f"{file.name} : test sans méthode.")
            code_base = (method.get("codeBase") or "").replace("\\", "/")
            if segment not in code_base.lower():
                raise Refused(f"{file.name} : résultats d'une autre configuration que {configuration} ({code_base}).")
            paths.add(code_base)
            tests[unit.get("id") or ""] = f"{method.get('className')}.{method.get('name')}"
        if len(paths) != 1:
            raise Refused(f"{file.name} : {len(paths)} assemblage(s) de tests dans un même fichier de résultats, un seul attendu.")
        path = paths.pop()
        if path in assemblies:
            raise Refused(f"plusieurs fichiers de résultats pour {Path(path).stem} dans {directory} (résultats d'une autre exécution ?).")
        results = []
        for result in root.iterfind(".//t:UnitTestResult", NS):
            name, outcome = result.get("testName") or "", result.get("outcome") or ""
            test_id = result.get("testId") or ""
            if test_id not in tests:
                raise Refused(f"{file.name} : résultat « {name} » sans définition de test.")
            key = tests[test_id]
            if name != key and not name.startswith(key + "("):
                raise Refused(f"{file.name} : nom de test « {name} » sans rapport avec sa méthode {key}.")
            results.append((name, key, outcome))
        assemblies[path] = (file.name, run_start(root, file), results)
    return assemblies


def parse_listing(listing: str) -> list[tuple[str, list[str]]]:
    """
    Sections d'une liste de tests : assemblage (« Test run for CHEMIN.dll (…) ») et noms des tests listés après lui (lignes en retrait
    de quatre espaces), jusqu'à la section suivante.
    """
    sections: list[tuple[str, list[str]]] = []
    for line in listing.split("\n"):
        line = line.rstrip("\r")
        match = TEST_RUN.match(line)
        if match:
            sections.append((match.group(1).replace("\\", "/"), []))
        elif line.startswith("    ") and line[4:5].strip():
            if not sections:
                raise Refused(f"test « {line[4:]} » listé avant toute ligne « Test run for … ».")
            sections[-1][1].append(line[4:])
    return sections


def newest_file(folder: Path) -> tuple[float, Path] | None:
    """Fichier le plus récemment modifié du dossier, sous-dossiers compris (date de modification, chemin)."""
    newest = None
    for directory, _, files in os.walk(folder):
        for name in files:
            path = Path(directory) / name
            modified = os.stat(path).st_mtime
            if newest is None or modified > newest[0]:
                newest = (modified, path)
    return newest


def check_same_build(assemblies: dict[str, tuple[str, float, list[tuple[str, str, str]]]], dlls: list[str]) -> None:
    """
    Les résultats sont ceux des assemblages listés (mêmes chemins, aucun de plus ni de moins), et rien n'a changé dans leurs dossiers de
    sortie depuis le début de l'exécution : ni la DLL des tests, ni ses dépendances (une dépendance recompilée sans changer son
    assemblage de référence ne fait pas recompiler la DLL des tests), ni les données copiées à côté (fichiers « PreserveNewest »).
    """
    if not dlls:
        raise Refused("aucun assemblage de tests dans la liste (ligne « Test run for … » attendue).")
    listed = set(dlls)
    without = sorted(listed - set(assemblies))
    if without:
        raise Refused(f"aucun résultat pour {without[0]} (assemblage listé) : résultats d'une autre construction ?")
    extra = sorted(set(assemblies) - listed)
    if extra:
        raise Refused(f"résultats pour {extra[0]}, assemblage absent de la liste : résultats d'une autre construction ?")
    for path, (file, start, _) in sorted(assemblies.items()):
        if not os.path.isfile(path):
            raise Refused(f"{file} : assemblage {path} introuvable.")
        folder = Path(path).parent
        found = newest_file(folder)
        if found is None:
            raise Refused(f"{file} : dossier de {Path(path).name} vide.")
        modified, newest = found
        if modified > start:
            raise Refused(f"{file} : {newest.relative_to(folder)} (dossier de {Path(path).name}) modifié après le début de l'exécution "
                          "(résultats périmés).")


def recipe(directory: Path, configuration: str, listing: str) -> dict[str, int]:
    """Nombre de résultats de la catégorie listée par projet de tests (nom de l'assemblage sans « .dll ») ; lève Refused sinon."""
    assemblies = read_results(directory, configuration)
    sections = parse_listing(listing)
    check_same_build(assemblies, [dll for dll, _ in sections])
    where: dict[str, set[str]] = collections.defaultdict(set)    # méthode -> assemblages qui en ont des résultats
    names_in: dict[str, set[str]] = collections.defaultdict(set)  # assemblage -> noms affichés de ses résultats
    for path, (_, _, results) in assemblies.items():
        for name, key, _ in results:
            where[key].add(path)
            names_in[path].add(name)
    selected: set[tuple[str, str]] = set()   # (assemblage, méthode) de la catégorie
    missing = []
    for dll, names in sections:
        for member in (as_in_trx(name) for name in names):
            key = method_key(member)
            if len(where.get(key, ())) > 1:
                raise Refused(f"« {key} » a des résultats dans plusieurs assemblages "
                              f"({', '.join(sorted(Path(p).stem for p in where[key]))}) : rapprochement ambigu.")
            if dll not in where.get(key, ()) or (member != key and member not in names_in[dll]):
                missing.append(f"{member} ({Path(dll).stem})")
                continue
            selected.add((dll, key))
    if missing:
        raise Refused(f"{len(missing)} test(s) de la catégorie sans résultat dans l'exécution complète, dont « {missing[0]} ».")
    members = [(path, name, outcome) for path, (_, _, results) in assemblies.items() for name, key, outcome in results
               if (path, key) in selected]
    skipped = [name for _, name, outcome in members if outcome == SKIPPED]
    if skipped:
        raise Refused(f"{len(skipped)} test(s) de la catégorie ignoré(s) (Skip) dans l'exécution complète, dont « {skipped[0]} » : "
                      "une recette exige des tests exécutés et réussis.")
    failed = [(name, outcome) for _, name, outcome in members if outcome != PASSED]
    if failed:
        raise Refused(f"{len(failed)} test(s) de la catégorie sans réussite, dont « {failed[0][0]} » ({failed[0][1]}).")
    counts: collections.Counter[str] = collections.Counter({Path(path).stem: 0 for path in assemblies})
    for path, _, _ in members:
        counts[Path(path).stem] += 1
    return dict(sorted(counts.items()))


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        print("Usage : category_results.py DOSSIER_RESULTATS CONFIGURATION < liste", file=sys.stderr)
        return 1
    try:
        counts = recipe(Path(argv[1]), argv[2], sys.stdin.buffer.read().decode("utf-8"))
        for project, count in counts.items():
            print(f"{project} {count}")
        return 0
    except (Refused, OSError, UnicodeDecodeError) as error:
        print(f"Résultats inutilisables : {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
