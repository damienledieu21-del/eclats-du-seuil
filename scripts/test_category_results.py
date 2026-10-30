"""Tests plus rapides : contrôles de scripts/category_results.py (Python 3, bibliothèque standard seulement, hors ligne).

Fichiers de résultats TRX et listes de tests construits dans un dossier temporaire ; aucun appel à dotnet.
"""
from __future__ import annotations

import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from xml.sax.saxutils import quoteattr

import category_results as cr

START = "2026-10-29T21:45:04.4214431+01:00"
START_EPOCH = 1793306704.421443


class CategoryResultsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.results = self.root / "resultats"
        self.results.mkdir()
        self.domain = self.dll("EdS.Domain.Tests")
        self.data = self.dll("EdS.Data.Tests")

    def tearDown(self):
        self.temp.cleanup()

    def dll(self, project: str, configuration: str = "release", built: float = START_EPOCH - 60, worktree: str = "") -> str:
        path = self.root / worktree / "artifacts" / "bin" / project / configuration / f"{project}.dll"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(b"MZ")
        os.utime(path, (built, built))
        return str(path)

    def trx(self, file: str, dll: str, results: list[tuple[str, str, str, str]], start: str = START):
        """results : (classe, méthode, nom affiché, issue) ; un UnitTest par résultat, comme l'écrit le journal TRX."""
        units, rows = [], []
        for i, (cls, method, name, outcome) in enumerate(results):
            uid = f"00000000-0000-0000-0000-{i:012d}"
            units.append(f'<UnitTest name={quoteattr(name)} id="{uid}"><TestMethod codeBase={quoteattr(dll)} '
                         f'className={quoteattr(cls)} name={quoteattr(method)} /></UnitTest>')
            rows.append(f'<UnitTestResult testId="{uid}" testName={quoteattr(name)} outcome="{outcome}" />')
        text = ('<?xml version="1.0" encoding="utf-8"?>'
                '<TestRun id="1" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                f'<Times creation="{start}" queuing="{start}" start="{start}" finish="{start}" />'
                f'<Results>{"".join(rows)}</Results><TestDefinitions>{"".join(units)}</TestDefinitions></TestRun>')
        (self.results / file).write_text(text, encoding="utf-8")

    def listing(self, names: list[str], dlls: list[str] | None = None,
                sections: list[tuple[str, list[str]]] | None = None) -> str:
        """
        Liste comme l'écrit « dotnet test -m:1 --list-tests » : une section par assemblage. Sans sections explicites, un nom
        « EdS.Data.… » va dans la section de EdS.Data.Tests, les autres dans la première autre section.
        """
        if sections is None:
            dlls = dlls if dlls is not None else [self.data, self.domain]
            others = [dll for dll in dlls if dll != self.data] or dlls
            owner = lambda name: self.data if name.startswith("EdS.Data.") and self.data in dlls else others[0]
            sections = [(dll, [name for name in names if owner(name) == dll]) for dll in dlls]
        lines = []
        for dll, members in sections:
            lines += [f"Test run for {dll} (.NETCoreApp,Version=v10.0)", "VSTest version 18.0.1 (x64)", "",
                      "The following Tests are available:"] + ["    " + name for name in members]
        return "\n".join(lines) + "\n"

    def standard(self, numeric_outcome: str = "Passed", other_outcome: str = "Passed"):
        self.trx("domain.trx", self.domain, [
            ("EdS.Domain.Tests.FixedTests", "Division", "EdS.Domain.Tests.FixedTests.Division", numeric_outcome),
            ("EdS.Domain.Tests.FixedTests", "Arrondi", 'EdS.Domain.Tests.FixedTests.Arrondi(x: "0.5")', "Passed"),
            ("EdS.Domain.Tests.FixedTests", "Arrondi", 'EdS.Domain.Tests.FixedTests.Arrondi(x: "-0.5")', "Passed"),
            ("EdS.Domain.Tests.Autre", "Hors", "EdS.Domain.Tests.Autre.Hors", other_outcome),
        ])
        self.trx("data.trx", self.data, [
            ("EdS.Data.Tests.Textes", "Admis", 'EdS.Data.Tests.Textes.Admis(json: "\\"é\\ud83d\\ude00\\"")', "Passed"),
            ("EdS.Data.Tests.Textes", "Admis", 'EdS.Data.Tests.Textes.Admis(json: "0")', "Passed"),
            ("EdS.Data.Tests.Graphe", "Cas", "EdS.Data.Tests.Graphe.Cas(effect: Effet { A = 1 })", "Passed"),
            ("EdS.Data.Tests.Graphe", "Cas", "EdS.Data.Tests.Graphe.Cas(effect: Effet { A = 2 })", "Passed"),
            ("EdS.Data.Tests.Graphe", "Cas", "EdS.Data.Tests.Graphe.Cas(effect: Effet { A = 3 })", "Passed"),
        ])

    def test_comptes_par_projet_comme_le_filtre(self):
        self.standard()
        names = [
            "EdS.Domain.Tests.FixedTests.Division",
            'EdS.Domain.Tests.FixedTests.Arrondi(x: "0.5")',
            'EdS.Domain.Tests.FixedTests.Arrondi(x: "-0.5")',
            'EdS.Data.Tests.Textes.Admis(json: "\\"é😀\\"")',   # caractère hors du plan de base : écrit \uXXXX\uXXXX dans le TRX
            'EdS.Data.Tests.Textes.Admis(json: "0")',
            "EdS.Data.Tests.Graphe.Cas",                         # théorie listée en une ligne, exécutée en trois cas
        ]
        self.assertEqual({"EdS.Data.Tests": 5, "EdS.Domain.Tests": 3}, cr.recipe(self.results, "Release", self.listing(names)))

    def test_projet_sans_test_de_la_categorie_compte_zero(self):
        self.standard()
        counts = cr.recipe(self.results, "Release", self.listing(["EdS.Domain.Tests.Autre.Hors"]))
        self.assertEqual({"EdS.Data.Tests": 0, "EdS.Domain.Tests": 1}, counts)

    def test_echec_dans_la_categorie_refuse(self):
        self.standard(numeric_outcome="Failed")
        with self.assertRaisesRegex(cr.Refused, r"1 test\(s\) de la catégorie sans réussite.*Division.*Failed"):
            cr.recipe(self.results, "Release", self.listing(["EdS.Domain.Tests.FixedTests.Division"]))

    def test_echec_hors_de_la_categorie_ignore_comme_avec_le_filtre(self):
        self.standard(other_outcome="Failed")
        counts = cr.recipe(self.results, "Release", self.listing(["EdS.Domain.Tests.FixedTests.Division"]))
        self.assertEqual({"EdS.Data.Tests": 0, "EdS.Domain.Tests": 1}, counts)

    def test_cas_d_une_theorie_en_echec_refuse_meme_listee_en_une_ligne(self):
        self.standard()
        self.trx("data.trx", self.data, [
            ("EdS.Data.Tests.Graphe", "Cas", "EdS.Data.Tests.Graphe.Cas(effect: Effet { A = 1 })", "Passed"),
            ("EdS.Data.Tests.Graphe", "Cas", "EdS.Data.Tests.Graphe.Cas(effect: Effet { A = 2 })", "Error"),
        ])
        with self.assertRaisesRegex(cr.Refused, "sans réussite.*A = 2"):
            cr.recipe(self.results, "Release", self.listing(["EdS.Data.Tests.Graphe.Cas"]))

    def test_test_sans_resultat_refuse(self):
        self.standard()
        with self.assertRaisesRegex(cr.Refused, "sans résultat dans l'exécution complète.*Absent"):
            cr.recipe(self.results, "Release", self.listing(["EdS.Domain.Tests.FixedTests.Absent"]))

    def test_cas_de_theorie_sans_resultat_refuse(self):
        self.standard()
        with self.assertRaisesRegex(cr.Refused, "sans résultat"):
            cr.recipe(self.results, "Release", self.listing(['EdS.Domain.Tests.FixedTests.Arrondi(x: "1.5")']))

    def test_deux_fichiers_pour_un_assemblage_refuses(self):
        self.standard()
        self.trx("copie.trx", self.domain, [("EdS.Domain.Tests.Autre", "Hors", "EdS.Domain.Tests.Autre.Hors", "Passed")])
        with self.assertRaisesRegex(cr.Refused, "plusieurs fichiers de résultats pour EdS.Domain.Tests"):
            cr.recipe(self.results, "Release", self.listing(["EdS.Domain.Tests.Autre.Hors"]))

    def test_autre_configuration_refusee(self):
        self.standard()
        with self.assertRaisesRegex(cr.Refused, "autre configuration que Debug"):
            cr.recipe(self.results, "Debug", self.listing(["EdS.Domain.Tests.Autre.Hors"]))

    def test_dossier_sans_resultats_refuse(self):
        with self.assertRaisesRegex(cr.Refused, "aucun fichier de résultats"):
            cr.recipe(self.results, "Release", self.listing(["EdS.Domain.Tests.Autre.Hors"]))

    def test_resultats_anterieurs_a_la_construction_refuses(self):
        self.standard()
        os.utime(self.domain, (START_EPOCH + 1, START_EPOCH + 1))
        with self.assertRaisesRegex(cr.Refused, r"EdS.Domain.Tests.dll \(dossier de EdS.Domain.Tests.dll\) modifié après le début"):
            cr.recipe(self.results, "Release", self.listing(["EdS.Domain.Tests.Autre.Hors"]))

    def test_resultats_d_une_autre_construction_refuses(self):
        self.standard()
        ailleurs = self.dll("EdS.Domain.Tests", worktree="ailleurs")   # même projet, autre copie de travail
        with self.assertRaisesRegex(cr.Refused, "aucun résultat pour .*ailleurs.*autre construction"):
            cr.recipe(self.results, "Release", self.listing(["EdS.Domain.Tests.Autre.Hors"], [self.data, ailleurs]))
        with self.assertRaisesRegex(cr.Refused, "assemblage absent de la liste"):
            cr.recipe(self.results, "Release", self.listing(["EdS.Domain.Tests.Autre.Hors"], [self.data]))

    def test_liste_sans_assemblage_refusee(self):
        self.standard()
        with self.assertRaisesRegex(cr.Refused, "aucun assemblage de tests dans la liste"):
            cr.recipe(self.results, "Release", "No test is available.\n")

    def test_nom_avant_toute_section_refuse(self):
        self.standard()
        with self.assertRaisesRegex(cr.Refused, "« EdS.Domain.Tests.Autre.Hors » listé avant toute ligne « Test run for"):
            cr.recipe(self.results, "Release", "    EdS.Domain.Tests.Autre.Hors\n" + self.listing([]))

    def test_nom_sans_rapport_avec_sa_methode_refuse(self):
        self.trx("domain.trx", self.domain, [("EdS.Domain.Tests.FixedTests", "Division", "EdS.Domain.Tests.Autre.Division", "Passed")])
        self.trx("data.trx", self.data, [("EdS.Data.Tests.Textes", "Admis", 'EdS.Data.Tests.Textes.Admis(json: "0")', "Passed")])
        with self.assertRaisesRegex(cr.Refused, "sans rapport avec sa méthode"):
            cr.recipe(self.results, "Release", self.listing(["EdS.Domain.Tests.Autre.Division"]))

    # Revue de la PR #73 (sondes P1 à P9), changées en tests.

    def test_test_liste_sans_resultat_dans_son_assemblage_refuse_meme_si_un_homonyme_en_a(self):
        # P1 : le test listé pour EdS.Domain.Tests n'y a pas de résultat ; un test de même nom complet en a un dans EdS.Data.Tests.
        self.trx("domain.trx", self.domain, [("Partage.C", "Autre", "Partage.C.Autre", "Passed")])
        self.trx("data.trx", self.data, [("Partage.C", "M", "Partage.C.M", "Passed")])
        with self.assertRaisesRegex(cr.Refused, r"1 test\(s\) de la catégorie sans résultat.*« Partage.C.M \(EdS.Domain.Tests\) »"):
            cr.recipe(self.results, "Release", self.listing([], sections=[(self.data, []), (self.domain, ["Partage.C.M"])]))

    def test_methode_dans_deux_assemblages_refusee_plutot_que_comptee_deux_fois(self):
        # P1b : même nom complet dans deux assemblages, les deux exécutés : il serait compté dans les deux projets.
        self.trx("domain.trx", self.domain, [("Partage.C", "M", 'Partage.C.M(x: 1)', "Passed")])
        self.trx("data.trx", self.data, [("Partage.C", "M", 'Partage.C.M(x: 1)', "Passed")])
        with self.assertRaisesRegex(cr.Refused, r"« Partage.C.M » a des résultats dans plusieurs assemblages \(EdS.Data.Tests, EdS.Domain.Tests\)"):
            cr.recipe(self.results, "Release", self.listing([], sections=[(self.data, []), (self.domain, ['Partage.C.M(x: 1)'])]))

    def test_nom_liste_dans_la_section_d_un_autre_assemblage_refuse(self):
        # Lignes entremêlées (liste sans « -m:1 ») : un test de EdS.Data.Tests dans la section de EdS.Domain.Tests est refusé.
        self.standard()
        with self.assertRaisesRegex(cr.Refused, r"sans résultat.*EdS.Data.Tests.Graphe.Cas \(EdS.Domain.Tests\)"):
            cr.recipe(self.results, "Release",
                      self.listing([], sections=[(self.data, []), (self.domain, ["EdS.Data.Tests.Graphe.Cas"])]))

    def test_homonyme_hors_categorie_dans_un_autre_assemblage_sans_effet(self):
        # Les noms complets ne se répètent pas d'un assemblage à l'autre dans le dépôt ; un homonyme non listé ne gêne pas.
        self.standard()
        self.trx("data.trx", self.data, [("EdS.Domain.Tests.Autre", "Hors", "EdS.Domain.Tests.Autre.Hors", "Passed"),
                                          ("EdS.Data.Tests.Textes", "Admis", 'EdS.Data.Tests.Textes.Admis(json: "0")', "Passed")])
        counts = cr.recipe(self.results, "Release", self.listing(['EdS.Data.Tests.Textes.Admis(json: "0")']))
        self.assertEqual({"EdS.Data.Tests": 1, "EdS.Domain.Tests": 0}, counts)

    def test_tests_ignores_refuses(self):
        # P2 : une catégorie dont tous les tests sont ignorés (Skip) ne passe plus.
        self.trx("domain.trx", self.domain, [("A.C", "M1", "A.C.M1", "NotExecuted"), ("A.C", "M2", "A.C.M2", "NotExecuted")])
        self.trx("data.trx", self.data, [("B.C", "M", "B.C.M", "Passed")])
        with self.assertRaisesRegex(cr.Refused, r"2 test\(s\) de la catégorie ignoré\(s\) \(Skip\).*A.C.M1"):
            cr.recipe(self.results, "Release", self.listing(["A.C.M1", "A.C.M2"]))

    def test_un_seul_test_ignore_refuse(self):
        self.standard(numeric_outcome="NotExecuted")
        with self.assertRaisesRegex(cr.Refused, "ignoré"):
            cr.recipe(self.results, "Release", self.listing(["EdS.Domain.Tests.FixedTests.Division"]))

    def test_donnees_copiees_apres_le_debut_refusees(self):
        # P3 : données modifiées et recopiées (PreserveNewest) sans recompiler la DLL des tests.
        self.standard()
        data = Path(self.data).parent / "scenarios" / "zones" / "x.json"
        data.parent.mkdir(parents=True)
        data.write_text("{}")
        os.utime(data, (START_EPOCH + 3600,) * 2)
        with self.assertRaisesRegex(cr.Refused, r"scenarios/zones/x.json \(dossier de EdS.Data.Tests.dll\) modifié après le début"):
            cr.recipe(self.results, "Release", self.listing(["EdS.Domain.Tests.Autre.Hors"]))

    def test_dependance_recompilee_apres_le_debut_refusee(self):
        # P3 : corps d'une méthode changé dans une dépendance (même assemblage de référence : la DLL des tests n'est pas recompilée).
        self.standard()
        dependency = Path(self.domain).parent / "EdS.Domain.dll"
        dependency.write_bytes(b"MZ")
        os.utime(dependency, (START_EPOCH + 1,) * 2)
        with self.assertRaisesRegex(cr.Refused, r"EdS.Domain.dll \(dossier de EdS.Domain.Tests.dll\) modifié après le début"):
            cr.recipe(self.results, "Release", self.listing(["EdS.Domain.Tests.Autre.Hors"]))

    def test_fichiers_anterieurs_au_debut_acceptes(self):
        self.standard()
        for name in ("EdS.Domain.dll", "data/v0/rules.json"):
            path = Path(self.domain).parent / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b"{}")
            os.utime(path, (START_EPOCH - 1,) * 2)
        self.assertEqual({"EdS.Data.Tests": 0, "EdS.Domain.Tests": 1},
                         cr.recipe(self.results, "Release", self.listing(["EdS.Domain.Tests.Autre.Hors"])))

    def test_fichier_de_resultats_tronque_refuse(self):
        # P4b
        self.standard()
        text = (self.results / "domain.trx").read_text(encoding="utf-8")
        (self.results / "domain.trx").write_text(text[: len(text) // 2], encoding="utf-8")
        with self.assertRaisesRegex(cr.Refused, "domain.trx : fichier de résultats illisible"):
            cr.recipe(self.results, "Release", self.listing(["EdS.Domain.Tests.Autre.Hors"]))

    def test_autres_issues_refusees(self):
        # P8
        for outcome in ("Inconclusive", "Timeout", "Aborted", "Error", "PassedButRunAborted", "Pending", "Warning", "Completed",
                        "InProgress", "Failed", ""):
            with self.subTest(outcome=outcome):
                self.standard(numeric_outcome=outcome)
                with self.assertRaisesRegex(cr.Refused, "sans réussite"):
                    cr.recipe(self.results, "Release", self.listing(["EdS.Domain.Tests.FixedTests.Division"]))

    def test_debut_sans_fuseau_refuse(self):
        # P9
        self.trx("domain.trx", self.domain, [("A.C", "M", "A.C.M", "Passed")], start="2026-10-29T20:45:04.4214431")
        self.trx("data.trx", self.data, [("B.C", "M", "B.C.M", "Passed")])
        with self.assertRaisesRegex(cr.Refused, "sans fuseau horaire"):
            cr.recipe(self.results, "Release", self.listing(["A.C.M"]))

    def test_main_ecrit_les_comptes_ou_le_refus(self):
        self.standard()
        listing = self.listing(["EdS.Domain.Tests.FixedTests.Division"]).encode("utf-8")
        for expected_code, stdout, stderr, args in (
                (0, "EdS.Data.Tests 0\nEdS.Domain.Tests 1\n", "", ["Release"]),
                (1, "", "Résultats inutilisables : ", ["Debug"])):
            with self.subTest(args=args):
                run = subprocess.run([sys.executable, "-I", str(Path(cr.__file__)), str(self.results), *args],
                                     input=listing, capture_output=True)
                self.assertEqual(expected_code, run.returncode)
                self.assertEqual(stdout, run.stdout.decode("utf-8"))
                self.assertTrue(run.stderr.decode("utf-8").startswith(stderr))

    def test_nom_affiche_comme_dans_le_trx(self):
        self.assertEqual('a("\\ud83d\\ude00")', cr.as_in_trx('a("😀")'))
        self.assertEqual("a(\\u0001)", cr.as_in_trx("a(\x01)"))
        self.assertEqual("é\t", cr.as_in_trx("é\t"))
        self.assertEqual("Espace.Classe.Methode", cr.method_key('Espace.Classe.Methode(x: "(")'))


if __name__ == "__main__":
    unittest.main()
