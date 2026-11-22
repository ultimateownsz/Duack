"""Behavior tests for tools/plan_split.py (stdlib only, no external deps)."""

import sys
import tempfile
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))

from plan_split import PlanSplitError, split_plan  # noqa: E402

FIXTURE = """# Test Plan

Preamble line.

## 1. Evidence

Evidence body.

## 2. Decisions

ADR-1 body.

## 3. Slice A — Voice limiting

Scope A.

## 4. Slice B — Spatial sound

Scope B.

## 5. Math

Formula.
"""


class SplitPlanTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.plan = self.root / "PLAN.md"
        self.plan.write_text(FIXTURE, encoding="utf-8")
        self.out = self.root / "split"
        self.out.mkdir()

    def split(self, **kwargs):
        return split_plan(self.plan, self.out, **kwargs)

    def test_shared_keeps_preamble_and_headings_but_drops_slice_bodies(self):
        self.split()
        shared = (self.out / "SHARED.md").read_text(encoding="utf-8")
        self.assertIn("# Test Plan", shared)
        self.assertIn("Preamble line.", shared)
        self.assertIn("## 1. Evidence", shared)
        self.assertIn("## 5. Math", shared)
        self.assertNotIn("## 3. Slice A", shared)
        self.assertNotIn("Scope A.", shared)

    def test_creates_one_plan_per_slice_with_links(self):
        self.split()
        a = (self.out / "voice-limiting" / "PLAN.md").read_text(encoding="utf-8")
        b = (self.out / "spatial-sound" / "PLAN.md").read_text(encoding="utf-8")
        self.assertIn("Scope A.", a)
        self.assertIn("Scope B.", b)
        self.assertIn("`../README.md`", a)
        self.assertIn("`../SHARED.md`", a)
        self.assertIn("Slice A", a)
        self.assertNotIn("Scope B.", a)

    def test_index_lists_every_slice_with_draft_status(self):
        self.split()
        index = (self.out / "README.md").read_text(encoding="utf-8")
        self.assertIn("voice-limiting/", index)
        self.assertIn("spatial-sound/", index)
        self.assertEqual(index.count("| draft |"), 2)
        self.assertIn("SHARED.md", index)

    def test_refuses_to_overwrite_without_force(self):
        self.split()
        with self.assertRaises(PlanSplitError) as ctx:
            self.split()
        self.assertIn("README.md", str(ctx.exception))

    def test_force_allows_overwrite(self):
        self.split()
        self.split(force=True)
        self.assertIn("Scope A.", (self.out / "voice-limiting" / "PLAN.md").read_text(encoding="utf-8"))

    def test_duplicate_slug_is_rejected(self):
        self.plan.write_text(
            FIXTURE.replace("Slice B — Spatial sound", "Slice B — Voice limiting"),
            encoding="utf-8",
        )
        with self.assertRaises(PlanSplitError) as ctx:
            self.split()
        self.assertIn("voice-limiting", str(ctx.exception))

    def test_no_slice_sections_is_rejected(self):
        self.plan.write_text("# Test Plan\n\n## 1. Evidence\n\nBody.\n", encoding="utf-8")
        with self.assertRaises(PlanSplitError) as ctx:
            self.split()
        self.assertIn("no slice", str(ctx.exception).lower())

    def test_slug_override(self):
        self.split(slugs={"A": "voice-concurrency"})
        self.assertTrue((self.out / "voice-concurrency" / "PLAN.md").exists())
        self.assertFalse((self.out / "voice-limiting").exists())

    def test_fenced_heading_is_not_a_section(self):
        fenced = FIXTURE.replace(
            "Formula.",
            "Example plan fragment:\n\n```\n## 9. Slice Z — Fake\n\nNot a real slice.\n```\n\nFormula.",
        )
        self.plan.write_text(fenced, encoding="utf-8")
        summary = self.split()
        self.assertEqual([s[0] for s in summary["slices"]], ["voice-limiting", "spatial-sound"])
        shared = (self.out / "SHARED.md").read_text(encoding="utf-8")
        self.assertIn("## 9. Slice Z — Fake", shared)
        self.assertFalse((self.out / "fake").exists())

    def test_input_is_kept_by_default_and_removed_on_request(self):
        self.split()
        self.assertTrue(self.plan.exists())
        self.split(force=True, remove_input=True)
        self.assertFalse(self.plan.exists())

    def test_summary_reports_what_was_written(self):
        summary = self.split()
        self.assertEqual([s[0] for s in summary["slices"]], ["voice-limiting", "spatial-sound"])
        self.assertEqual(summary["shared"].name, "SHARED.md")
        self.assertEqual(summary["index"].name, "README.md")

    def cli(self, *args):
        import subprocess
        return subprocess.run(
            [sys.executable, str(TOOLS / "plan_split.py"), str(self.plan), "--out", str(self.out), *args],
            capture_output=True, text=True,
        )

    def test_cli_accepts_slug_override(self):
        result = self.cli("--slug", "A=voice-concurrency")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue((self.out / "voice-concurrency" / "PLAN.md").exists())

    def test_cli_rejects_overwrite_with_nonzero_exit(self):
        self.assertEqual(self.cli().returncode, 0)
        result = self.cli()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("refusing to overwrite", result.stderr)


if __name__ == "__main__":
    unittest.main()
