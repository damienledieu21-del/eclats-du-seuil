"""Deterministic CI self-report writer checks (Python 3 stdlib only).

Run in the same CI job BEFORE generating the report. All test files are
temporary, no network/GitHub writes or model calls. Tests are not signed.
"""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import stat
import tempfile
import unittest

import make_ci_provenance as writer

HEAD="1"*40
MERGE="2"*40
WORKFLOW=b"name: CI\non:\n  pull_request:\njobs:\n  build-test:\n    runs-on: ubuntu-latest\n"


class SelfReportTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory()
        self.root=Path(self.temp.name)
        self.workspace=self.root/"repo"
        self.runnertmp=self.root/"run"
        (self.workspace/".github/workflows").mkdir(parents=True)
        self.runnertmp.mkdir()
        (self.workspace/writer.WORKFLOW).write_bytes(WORKFLOW)

    def tearDown(self):
        self.temp.cleanup()

    def env(self,event="pull_request",head=HEAD,checkout=MERGE):
        return {
            "GITHUB_REPOSITORY":writer.REPOSITORY,
            "GITHUB_EVENT_NAME":event,
            "GITHUB_SHA":checkout,
            "EDS_SOURCE_SHA":head,
            "EDS_CI_JOB_KEY":"build-test",
            "GITHUB_RUN_ID":"56789",
            "GITHUB_RUN_ATTEMPT":"1",
            "GITHUB_WORKSPACE":str(self.workspace),
            "RUNNER_TEMP":str(self.runnertmp),
        }

    def test_pr_head_distinct_from_tested_checkout(self):
        info=writer.write_self_report(self.env())
        self.assertEqual("UNSIGNED_CI_SELF_REPORT_WRITTEN",info["status"])
        self.assertFalse(info["independent_ci_attestation"])
        self.assertFalse(info["release_authorized"])
        body=(self.runnertmp/writer.ARTIFACT_FILENAME).read_bytes()
        manifest=json.loads(body)
        self.assertEqual(2,manifest["schema_version"])
        self.assertEqual(HEAD,manifest["source_commit"])
        self.assertEqual(MERGE,manifest["tested_checkout_sha"])
        self.assertNotEqual(manifest["source_commit"],manifest["tested_checkout_sha"])
        self.assertEqual(
            hashlib.sha1(f"blob {len(WORKFLOW)}\0".encode()+WORKFLOW).hexdigest(),
            manifest["workflow_blob_sha"],
        )
        self.assertEqual(0o600,stat.S_IMODE(
            (self.runnertmp/writer.ARTIFACT_FILENAME).stat().st_mode
        ))

    def test_push_head_and_checkout_must_match(self):
        with self.assertRaisesRegex(writer.SelfReportBlocked,"push_head_checkout_sha_mismatch"):
            writer.build_manifest(self.env(event="push"),WORKFLOW)
        data=writer.build_manifest(
            self.env(event="push",head=HEAD,checkout=HEAD),WORKFLOW
        )
        self.assertEqual(HEAD,data["tested_checkout_sha"])

    def test_unknown_repository_event_or_job_rejected(self):
        for name,bad in (
            ("GITHUB_REPOSITORY","other/repo"),
            ("GITHUB_EVENT_NAME","workflow_dispatch"),
            ("EDS_CI_JOB_KEY","setup"),
        ):
            with self.subTest(name=name):
                env=self.env();env[name]=bad
                with self.assertRaisesRegex(writer.SelfReportBlocked,"wrong_repository_event_or_job"):
                    writer.build_manifest(env,WORKFLOW)

    def test_bad_head_or_checkout_sha_rejected(self):
        for name,bad in (
            ("EDS_SOURCE_SHA","wrong"),
            ("GITHUB_SHA","wrong"),
            ("GITHUB_SHA",None),
        ):
            with self.subTest(name=name):
                env=self.env();env[name]=bad
                with self.assertRaisesRegex(writer.SelfReportBlocked,"missing_or_invalid_commit_binding"):
                    writer.build_manifest(env,WORKFLOW)

    def test_malformed_run_ids_rejected(self):
        for name,bad in (
            ("GITHUB_RUN_ID","0"),
            ("GITHUB_RUN_ID","-5"),
            ("GITHUB_RUN_ATTEMPT","2.1"),
            ("GITHUB_RUN_ID",True),
        ):
            with self.subTest(name=name):
                env=self.env();env[name]=bad
                with self.assertRaisesRegex(writer.SelfReportBlocked,"invalid_github_run_identifier"):
                    writer.build_manifest(env,WORKFLOW)

    def test_missing_workflow_refused(self):
        (self.workspace/writer.WORKFLOW).unlink()
        with self.assertRaisesRegex(writer.SelfReportBlocked,"workflow_not_available"):
            writer.write_self_report(self.env())

    def test_workflow_symlink_refused(self):
        src=self.workspace/writer.WORKFLOW
        source=src.read_bytes();src.unlink()
        other=self.root/"outside";other.write_bytes(source)
        src.symlink_to(other)
        with self.assertRaisesRegex(writer.SelfReportBlocked,"workflow_not_available"):
            writer.write_self_report(self.env())

    def test_disallow_output_overwrite(self):
        writer.write_self_report(self.env())
        with self.assertRaisesRegex(writer.SelfReportBlocked,"self_report_already_exists_no_overwrite"):
            writer.write_self_report(self.env())

    def test_runner_temp_symlink_refused(self):
        symlink=self.root/"fake-temp"
        symlink.symlink_to(self.runnertmp,target_is_directory=True)
        env=self.env();env["RUNNER_TEMP"]=str(symlink)
        with self.assertRaisesRegex(writer.SelfReportBlocked,"unsafe_runner_temp_directory"):
            writer.write_self_report(env)

    def test_workflow_modification_changes_blob_sha(self):
        old=writer.build_manifest(self.env(),WORKFLOW)
        new=writer.build_manifest(self.env(),WORKFLOW+b"# changed\n")
        self.assertNotEqual(old["workflow_blob_sha"],new["workflow_blob_sha"])

    def test_strict_phases_and_unsigned_kind(self):
        result=writer.build_manifest(self.env(),WORKFLOW)
        self.assertEqual(writer.KIND,result["evidence_kind"])
        self.assertEqual(set(writer.PHASES),set(result["phase_results"]))
        self.assertTrue(all(v=="success" for v in result["phase_results"].values()))


if __name__=="__main__":
    unittest.main()
