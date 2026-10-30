"""Tests plus rapides : contrôles de scripts/category_results.py (Python 3, bibliothèque standard seulement, hors ligne).

Fichiers de résultats TRX et listes de tests construits dans un dossier temporaire ; aucun appel à dotnet.
"""
from __future__ import annotations

import os
from pathlib import Path
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

    def listing(self, names: list[str], dlls: list[str] | None = None) -> str:
        lines = []
        for dll in (dlls if dlls is not None else [self.data, self.domain]):
            lines += [f"Test run for {dll} (.NETCoreApp,Version=v10.0)", "VSTest version 18.0.1 (x64)", "",
                      "The following Tests are available:"]
        return "\n".join(lines + ["    " + name for name in names]) + "\n"

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
        with self.assertRaisesRegex(cr.Refused, "avant la dernière construction de EdS.Domain.Tests.dll"):
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
            cr.recipe(self.results, "Release", "    EdS.Domain.Tests.Autre.Hors\n")

    def test_nom_sans_rapport_avec_sa_methode_refuse(self):
        self.trx("domain.trx", self.domain, [("EdS.Domain.Tests.FixedTests", "Division", "EdS.Domain.Tests.Autre.Division", "Passed")])
        self.trx("data.trx", self.data, [("EdS.Data.Tests.Textes", "Admis", 'EdS.Data.Tests.Textes.Admis(json: "0")', "Passed")])
        with self.assertRaisesRegex(cr.Refused, "sans rapport avec sa méthode"):
            cr.recipe(self.results, "Release", self.listing(["EdS.Domain.Tests.Autre.Division"]))

    def test_nom_affiche_comme_dans_le_trx(self):
        self.assertEqual('a("\\ud83d\\ude00")', cr.as_in_trx('a("😀")'))
        self.assertEqual("a(\\u0001)", cr.as_in_trx("a(\x01)"))
        self.assertEqual("é\t", cr.as_in_trx("é\t"))
        self.assertEqual("Espace.Classe.Methode", cr.method_key('Espace.Classe.Methode(x: "(")'))


if __name__ == "__main__":
    unittest.main()
