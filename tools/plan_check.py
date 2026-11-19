#!/usr/bin/env python3
"""Walk-test consistency check for the Duack planning workspace.

Run from the repository root:

    python tools/plan_check.py

Checks the chain of evidence the planning files describe: index rows point
at real phase docs, reserved ID ranges match the phase docs that exist,
links and internal paths in plan/ and design/ resolve, experiment docs
keep their protocol shape, and statuses use the plan vocabulary. Standard
library only. Prints "pass", or "fail:" lines and exits 1: a fail stops
the session there, per the ICM check rule.
"""

import re
import sys
from pathlib import Path

ROOT = Path.cwd()
PROBLEMS = []
SEEN = set()

DOC_PATH_ROOTS = (
    "plan/", "design/", "tools/", "AGENTS.md", "CONTRIBUTING.md",
    "CHANGELOG.md",
)
STATUSES = ("Not started", "In progress", "Blocked", "Landed")
EXPERIMENT_OUTCOMES = ("Adopted", "Parked", "Rejected")
RESULT_RE = re.compile(r"Result:?\s*\**\s*(\w+)")
RANGE_LINE_RE = re.compile(
    r"^-\s*phase-(\d+):\s*D(\d+)\s*to\s*D(\d+),\s*G(\d+)\s*to\s*G(\d+)\s*$"
)


def fail(where, why):
    PROBLEMS.append(f"fail: {where}: {why}")


def is_separator(cell):
    return bool(cell) and set(cell) <= {"-", ":", " "}


def strip_ticks(cell):
    return cell.strip().strip("`").strip()


def check_path(path_text, where, base=ROOT):
    if (where, path_text) in SEEN:
        return
    SEEN.add((where, path_text))
    if not (base / Path(path_text)).exists():
        fail(where, f"path does not resolve: {path_text}")


def check_links_in(text, file_path):
    """Markdown links resolve relative to the file; backticked internal
    paths resolve from the repository root."""
    where = str(file_path.relative_to(ROOT))
    for m in re.finditer(r"(?<!\!)\[[^\]]*\]\(([^)]+)\)", text):
        target = m.group(1).split("#")[0].strip()
        if target and not target.startswith(("http://", "https://", "mailto:")):
            check_path(target, where, base=file_path.parent)
    for m in re.finditer(r"`([^`]+)`", text):
        token = m.group(1).strip()
        if token.startswith(DOC_PATH_ROOTS) and not token.endswith("/"):
            check_path(token, where)


def row_cells(line):
    cells = [strip_ticks(c) for c in line.strip().strip("|").split("|")]
    return cells if len(cells) == 4 else []


def check_plan_index():
    index = ROOT / "plan" / "plan.md"
    where = "plan/plan.md"
    if not index.exists():
        fail(where, "missing; the planning workspace is not installed")
        return
    text = index.read_text(encoding="utf-8")
    check_links_in(text, index)
    for line in text.splitlines():
        if not line.lstrip().startswith("|"):
            continue
        cells = row_cells(line)
        if len(cells) != 4 or cells[0] in ("Initiative",) or is_separator(cells[0]) or cells[0].startswith("("):
            continue
        initiative, phase, doc, status = cells
        if not doc or not status:
            continue
        if doc != "(none yet)":
            check_path(doc, where)
        if not any(status.startswith(s) for s in STATUSES):
            fail(where, f"unknown status '{status}' (row: {initiative} {phase})")
        if status.startswith("Blocked") and "(" not in status:
            fail(where, f"a Blocked row states its blocker (row: {initiative} {phase})")


def check_initiative(folder):
    overview = folder / "overview.md"
    if not overview.exists():
        fail(str(folder), "initiative folder has no overview.md")
        return
    where = str(overview.relative_to(ROOT))
    text = overview.read_text(encoding="utf-8")
    check_links_in(text, overview)
    indexed = set()
    for line in text.splitlines():
        m = RANGE_LINE_RE.match(line)
        if m:
            indexed.add(int(m.group(1)))
    existing = set()
    for f in sorted(folder.glob("phase-*.md")):
        m = re.match(r"phase-(\d+)", f.name)
        existing.add(int(m.group(1)))
        ptext = f.read_text(encoding="utf-8")
        check_links_in(ptext, f)
        for needle in ("- Problem:", "- Shape:", "## Work"):
            if needle not in ptext:
                fail(str(f.relative_to(ROOT)), f"phase doc lost its template shape (missing {needle})")
    for num in sorted(indexed - existing):
        fail(where, f"ID ranges reserve phase-{num:02d} but no phase doc exists")
    for num in sorted(existing - indexed):
        fail(where, f"phase-{num:02d} exists but overview reserves no ID block for it")


def check_experiments():
    exp_dir = ROOT / "design" / "experiments"
    if not exp_dir.exists():
        return
    for f in sorted(exp_dir.glob("*.md")):
        if f.name == "README.md":
            continue
        where = str(f.relative_to(ROOT))
        if re.match(r"^\d+[-_]", f.name) is None:
            fail(where, "experiment doc must be named <NN>-<slug>.md")
        text = f.read_text(encoding="utf-8")
        for field in ("Question:", "Criteria:", "Time box:"):
            if field not in text:
                fail(where, f"missing field '{field}'")
        m = RESULT_RE.search(text)
        if m and m.group(1) not in EXPERIMENT_OUTCOMES:
            outcome = m.group(1)
            fail(where, f"result must be Adopted, Parked, or Rejected (saw '{outcome}')")
        check_links_in(text, f)


def check_deferred():
    deferred = ROOT / "design" / "deferred.md"
    if deferred.exists():
        check_links_in(deferred.read_text(encoding="utf-8"), deferred)


def scan_workspace_docs():
    """Link-check every planning doc, including subsystem write-ups an
    agent adds later under design/."""
    for base in (ROOT / "plan", ROOT / "design"):
        if not base.exists():
            continue
        for f in sorted(base.rglob("*.md")):
            if "_templates" in f.parts:
                continue
            check_links_in(f.read_text(encoding="utf-8"), f)


def main():
    if not (ROOT / "plan").exists():
        print("fail: plan/ is missing; the planning workspace is not installed")
        sys.exit(1)
    check_plan_index()
    for folder in sorted(p for p in (ROOT / "plan").iterdir() if p.is_dir() and p.name != "_templates"):
        check_initiative(folder)
    check_experiments()
    scan_workspace_docs()
    if PROBLEMS:
        print("\n".join(PROBLEMS))
        print(f"fail: {len(PROBLEMS)} problem(s). Fix the files, not the explanation.")
        sys.exit(1)
    print("pass")


if __name__ == "__main__":
    main()
