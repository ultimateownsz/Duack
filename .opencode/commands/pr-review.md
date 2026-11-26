---
description: Review an open PR with qa, verify the findings, and record the verdict in the plan workspace
---

Review PR $ARGUMENTS. If no number is supplied, use the PR for the current branch.

1. Resolve it with `gh pr view <n> --json number,title,state,headRefOid,headRefName,baseRefName,commits`. Confirm it is open and record the head SHA.
2. Launch the `qa` subagent with the PR number, head SHA, and branch names. Ask it to compare the PR diff against `AGENTS.md` and `CONTRIBUTING.md`, issue a good or blocked verdict, and give exact file:line proof for every finding. Tell it not to edit files and not to post a GitHub review unless asked.
3. Independently verify every blocking finding with `gh pr diff <n>` and `gh api repos/<owner>/<repo>/contents/<file>?ref=<headSHA>` before relaying it. A reviewer can be wrong.
4. Record the verdict in `docs/plans/vnext/REVIEWS.md`: a dated entry with PR number, head SHA, base, verdict, each finding (severity, rule, proof, required fix, status), and the checked-and-clear list. Keep every finding as a snapshot of that head even after fixes land. Link the record from `docs/plans/vnext/README.md` if it is newly created.
5. Report the verdict to the user. Do not fix findings or post a GitHub review unless explicitly asked.
6. When the verdict is good and the PR completes one or more checkboxes in a
   plan phase doc, land a small docs commit on the PR branch in the same pass:
   tick those checkboxes, move their plan index rows (Blocked / In progress /
   Landed) to match, and end the commit with the usual `Refs: #<issue>` footer.
   The boxes stay unticked while a verdict is pending or blocked. If the
   verdict clears the last blocking gate but acceptance items remain (human
   listening, samples), tick nothing and retarget the remainder like #44.
