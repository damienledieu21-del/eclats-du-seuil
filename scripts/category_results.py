#!/usr/bin/env python3
"""Tests plus rapides (octobre 2026) : recette d'une catégorie à partir des résultats d'une exécution complète.

Usage : python3 scripts/category_results.py DOSSIER_RESULTATS CONFIGURATION < liste

L'entrée standard est la sortie de « dotnet test --no-build -c CONFIGURATION --list-tests --filter Category=X » : une ligne
« Test run for CHEMIN.dll (…) » par projet de tests, puis les tests de la catégorie, un par ligne, en retrait de quatre espaces.
DOSSIER_RESULTATS contient les fichiers TRX écrits par l'exécution complète de la même construction (« dotnet test --no-build
-c CONFIGURATION --logger trx --results-directory DOSSIER_RESULTATS »), un par projet de tests.

Écrit, pour chaque projet de tests, une ligne « Projet N » : le nombre de résultats de la catégorie dans ce projet, c'est-à-dire ce
que « dotnet test --filter Category=X » aurait exécuté (un filtre de catégorie choisit des méthodes : tous les cas d'une théorie
ont les catégories de leur méthode). Échoue (code 1, message sur la sortie d'erreur) si les résultats sont absents, en double pour
un projet, d'une autre construction que celle listée (autre chemin, autre configuration) ou plus anciens qu'elle, si un nom de test
ne se rattache pas à sa méthode, si un test de la catégorie n'a pas de résultat, ou si un résultat de la catégorie n'a pas réussi.
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
# Issues acceptées : réussi, ou ignoré (« dotnet test » n'échoue pas sur un test ignoré ; aucun test du dépôt ne l'est).
ACCEPTED = ("Passed", "NotExecuted")
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
    """Début de l'exécution (élément Times, attribut start), en secondes depuis l'origine Unix."""
    times = root.find("t:Times", NS)
    text = times.get("start") if times is not None else None
    if not text:
        raise Refused(f"{file.name} : début de l'exécution absent (Times start).")
    # Sept décimales dans les fichiers TRX : six suffisent (microsecondes) et toute version de Python 3 les lit.
    text = re.sub(r"(\.\d{6})\d+", r"\1", text)
    try:
        return datetime.fromisoformat(text).timestamp()
    except ValueError:
        raise Refused(f"{file.name} : début de l'exécution illisible ({text}).") from None


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
        root = ET.parse(file).getroot()
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
            key = tests[result.get("testId") or ""]
            if name != key and not name.startswith(key + "("):
                raise Refused(f"{file.name} : nom de test « {name} » sans rapport avec sa méthode {key}.")
            results.append((name, key, outcome))
        assemblies[path] = (file.name, run_start(root, file), results)
    return assemblies


def parse_listing(listing: str) -> tuple[list[str], list[str]]:
    """Assemblages (« Test run for CHEMIN.dll (…) ») et noms des tests (lignes en retrait de quatre espaces) d'une liste de tests."""
    dlls, names = [], []
    for line in listing.split("\n"):
        line = line.rstrip("\r")
        match = TEST_RUN.match(line)
        if match:
            dlls.append(match.group(1).replace("\\", "/"))
        elif line.startswith("    ") and line[4:5].strip():
            names.append(line[4:])
    return dlls, names


def check_same_build(assemblies: dict[str, tuple[str, float, list[tuple[str, str, str]]]], dlls: list[str]) -> None:
    """Les résultats sont ceux des assemblages listés (mêmes chemins, aucun de plus ni de moins), écrits après leur construction."""
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
        try:
            built = os.stat(path).st_mtime
        except OSError:
            raise Refused(f"{file} : assemblage {path} introuvable.") from None
        if built > start:
            raise Refused(f"{file} : exécution commencée avant la dernière construction de {Path(path).name} (résultats périmés).")


def recipe(directory: Path, configuration: str, listing: str) -> dict[str, int]:
    """Nombre de résultats de la catégorie listée par projet de tests (nom de l'assemblage sans « .dll ») ; lève Refused sinon."""
    assemblies = read_results(directory, configuration)
    dlls, names = parse_listing(listing)
    check_same_build(assemblies, dlls)
    members = [as_in_trx(name) for name in names]
    keys = {method_key(name) for name in members}
    by_name = {name for _, _, results in assemblies.values() for name, _, _ in results}
    by_key = {key for _, _, results in assemblies.values() for _, key, _ in results}
    missing = [name for name in members if name not in by_name and name not in by_key]
    if missing:
        raise Refused(f"{len(missing)} test(s) de la catégorie sans résultat dans l'exécution complète, dont « {missing[0]} ».")
    failed = [(name, outcome) for _, _, results in assemblies.values() for name, key, outcome in results
              if key in keys and outcome not in ACCEPTED]
    if failed:
        raise Refused(f"{len(failed)} test(s) de la catégorie sans réussite, dont « {failed[0][0]} » ({failed[0][1]}).")
    counts: collections.Counter[str] = collections.Counter()
    for path, (_, _, results) in assemblies.items():
        counts[Path(path).stem] += sum(1 for _, key, _ in results if key in keys)
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
    except (Refused, ET.ParseError, OSError, KeyError, UnicodeDecodeError) as error:
        print(f"Résultats inutilisables : {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
