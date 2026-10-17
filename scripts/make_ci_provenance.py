#!/usr/bin/env python3
"""Generate ONE unsigned GitHub Actions test self-report after the EdS CI steps.

The output is a factual, bounded *claim*, NOT an attestation. In particular,
PR head SHA and tested GitHub merge checkout SHA may be different. No network,
no shell execution, no reading credentials, no signing and no GitHub writes.
"""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import re
import stat

REPOSITORY="damienledieu21-del/eclats-du-seuil"
WORKFLOW=".github/workflows/ci.yml"
ARTIFACT_FILENAME="eds-provenance.json"
KIND="eds-ci-unsigned-self-report-v2"
PHASES=(
    "release_build","release_tests","category_tests",
    "simulation_and_replay","debug_build","debug_tests",
)
SHA40=re.compile(r"[0-9a-f]{40}\Z")
POSITIVE=re.compile(r"[1-9][0-9]{0,17}\Z")
MAX_WORKFLOW_BYTES=100_000

class SelfReportBlocked(ValueError):
    pass


def sha40(value:object)->bool:
    return type(value) is str and SHA40.fullmatch(value) is not None


def positive_int(value:object)->int:
    if type(value) is not str or POSITIVE.fullmatch(value) is None:
        raise SelfReportBlocked("invalid_github_run_identifier")
    return int(value)


def build_manifest(env:dict[str,str],workflow:bytes)->dict:
    if (type(env) is not dict
        or env.get("GITHUB_REPOSITORY")!=REPOSITORY
        or env.get("GITHUB_EVENT_NAME") not in ("push","pull_request")
        or env.get("EDS_CI_JOB_KEY")!="build-test"):
        raise SelfReportBlocked("wrong_repository_event_or_job")
    head=env.get("EDS_SOURCE_SHA")
    checkout=env.get("GITHUB_SHA")
    if not sha40(head) or not sha40(checkout):
        raise SelfReportBlocked("missing_or_invalid_commit_binding")
    if env["GITHUB_EVENT_NAME"]=="push" and head!=checkout:
        raise SelfReportBlocked("push_head_checkout_sha_mismatch")
    if type(workflow) is not bytes or not 0<len(workflow)<=MAX_WORKFLOW_BYTES:
        raise SelfReportBlocked("missing_or_oversized_workflow")
    blob_sha=hashlib.sha1(
        f"blob {len(workflow)}\0".encode("ascii")+workflow
    ).hexdigest()
    return {
        "schema_version":2,
        "evidence_kind":KIND,
        "repository":REPOSITORY,
        "event_name":env["GITHUB_EVENT_NAME"],
        "source_commit":head,
        "tested_checkout_sha":checkout,
        "workflow_blob_sha":blob_sha,
        "run_id":positive_int(env.get("GITHUB_RUN_ID")),
        "run_attempt":positive_int(env.get("GITHUB_RUN_ATTEMPT")),
        "job_key":"build-test",
        "workflow_path":WORKFLOW,
        # A claim emitted only after tests exit 0; NOT independently proven.
        "phase_results":{name:"success" for name in PHASES},
    }


def require_safe_dir(path:object,field:str)->Path:
    if type(path) is not str or not path.startswith("/"):
        raise SelfReportBlocked("invalid_"+field+"_path")
    p=Path(path)
    if p.is_symlink() or not p.is_dir():
        raise SelfReportBlocked("unsafe_"+field+"_directory")
    return p


def write_self_report(env:dict[str,str])->dict:
    workspace=require_safe_dir(env.get("GITHUB_WORKSPACE"),"workspace")
    runner_temp=require_safe_dir(env.get("RUNNER_TEMP"),"runner_temp")
    workflow_file=workspace/WORKFLOW
    if workflow_file.is_symlink() or not workflow_file.is_file():
        raise SelfReportBlocked("workflow_not_available")
    info=workflow_file.stat()
    if (not stat.S_ISREG(info.st_mode)
        or not 0<info.st_size<=MAX_WORKFLOW_BYTES):
        raise SelfReportBlocked("workflow_file_invalid")
    source=workflow_file.read_bytes()
    if len(source)!=info.st_size:
        raise SelfReportBlocked("workflow_changed_during_read")
    manifest=build_manifest(env,source)
    destination=runner_temp/ARTIFACT_FILENAME
    if destination.exists() or destination.is_symlink():
        raise SelfReportBlocked("self_report_already_exists_no_overwrite")
    body=(json.dumps(
        manifest,sort_keys=True,separators=(",",":"),ensure_ascii=False
    )+"\n").encode("utf-8")
    if len(body)>24_000:
        raise SelfReportBlocked("oversized_self_report")
    fd=os.open(
        destination,
        os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,
        0o600,
    )
    with os.fdopen(fd,"wb") as out:
        out.write(body)
        out.flush()
        os.fsync(out.fileno())
    return {
        "status":"UNSIGNED_CI_SELF_REPORT_WRITTEN",
        "path_in_runner_temp":ARTIFACT_FILENAME,
        "source_commit":manifest["source_commit"],
        "tested_checkout_sha":manifest["tested_checkout_sha"],
        "report_sha256":hashlib.sha256(body).hexdigest(),
        "independent_ci_attestation":False,
        "github_writes":0,
        "release_authorized":False,
    }


def main()->int:
    try:
        result=write_self_report(dict(os.environ))
        print(json.dumps(result,sort_keys=True))
        return 0
    except (SelfReportBlocked,OSError,UnicodeError,ValueError) as exc:
        print(json.dumps({
            "status":"UNSIGNED_CI_SELF_REPORT_BLOCKED",
            "error_class":type(exc).__name__,
            "independent_ci_attestation":False,
            "github_writes":0,
            "release_authorized":False,
        },sort_keys=True))
        return 2


if __name__=="__main__":
    raise SystemExit(main())
