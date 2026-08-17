# [Project Name TBD] — Agent Rulebook

These rules apply to all AI coding agents working in this repository. Adhere to
them strictly on every invocation. This is a single unified rulebook (context
rules + scope/safety + coding standards + git discipline) — there is
intentionally no separate playbook file, to guarantee it always gets read.

---

## 1. Context Acquisition

**Always read, every session, regardless of task size:**
- `INDEX.md` — current project state, read this FIRST. Its File Manifest tells
  you exactly which files matter for a given feature area, so you should
  rarely need to open unrelated files to orient yourself.

**Read only when the task touches that area — don't pay the full cascade for
a small fix:**

| If your task involves... | Also read |
|---|---|
| A backend/frontend architectural decision, or the reasoning behind an existing split | `ARCHITECTURE.md` (once it exists — see §6) |
| A single, well-scoped bug fix in one known file | Nothing beyond INDEX.md — its File Manifest should be enough |

If you're unsure whether a task needs wider context, say so and ask, rather
than guessing either direction.

> This table currently only covers two cases and is expected to grow as the
> project grows (e.g. once a real backend/frontend split exists, add a row
> for "task touches sensor polling → also read [x]"). Don't treat the table
> above as exhaustive — if you add a new area of the codebase, add a row
> here for it as part of that same task.

---

## 2. Sync Order (prevents INDEX.md drift across branches)

This project uses branch-per-feature (see §5). The following order is
**mandatory** to prevent INDEX.md log entries from diverging or silently
overwriting each other across branches:

1. **Pull `main` before branching.** Every new branch starts from a freshly
   pulled `main` — never off a stale local copy.
2. **Code first, INDEX.md update last.** Do not update INDEX.md mid-task and
   then keep coding. The INDEX.md update (Current Focus + File Manifest +
   Log Entries) is the closing move of a task, done after the code change
   is complete.
3. **INDEX.md updates ship in the same commit/PR as the code they describe.**
   Never a standalone "docs sync" commit or PR that lands separately from
   the feature it documents.
4. **Merge to `main` before starting the next branch.** Do not stack a new
   branch on top of an unmerged one. If the lead developer hasn't
   reviewed/merged yet, either wait or work on something explicitly
   unrelated — never start a second parallel branch that will also need to
   touch INDEX.md.
   - **Narrow exception:** a trivial, non-functional fix (typo, comment,
     README wording) that does **not** touch INDEX.md's File Manifest or
     Log Entries may be committed directly to `main` without a branch, if
     the lead developer explicitly approves it in the moment. This is an
     exception granted per-instance, not a standing permission — if there's
     any doubt whether a fix is "trivial," it isn't; treat it as a normal
     branch instead.
5. **On merge conflict in INDEX.md's Log Entries table:** always append,
   never overwrite. If a conflict appears there, treat it as a signal that
   rule 4 was violated somewhere — flag it, don't just pick a side.

---

## 3. Scope and Authority

| You CAN | You CANNOT |
| :--- | :--- |
| Write code for local hardware polling and the display/UI layer. | Install heavy dependencies without asking. |
| Ask for clarification if a feature is ambiguous. | Hardcode API keys or credentials directly into scripts. |
| | Merge to `main` yourself — the lead developer reviews and merges personally. |
| | Bundle unrelated fixes into an active branch — flag them and ask first (see §5). |

---

## 4. Technology & Coding Standards

- Favor built-in/lightweight libraries over heavy dependencies given this is
  a low-overhead, always-on monitoring tool — performance profile matters
  more here than in a typical app.
- **Split files early, by responsibility.** Sensor polling, API/data layer,
  and UI rendering should never share a file. Rule of thumb: if a file is
  handling more than one clearly distinct responsibility (e.g. "reads GPU
  sensors" AND "formats output for the UI"), split it before it grows
  further — don't wait until it's a "god object" to fix it.
- Keep the frontend structure flat — avoid deep nesting
  (e.g. `src/frontend/src/renderer/src/components/`-style chains). Prefer
  pulling `components/`, `hooks/`, etc. close to the project root.

---

## 5. Git & Version Control

- **One feature, one branch.** Before starting a task, create a branch off a
  freshly pulled `main` (e.g. `feature/gpu-temp-poll`,
  `fix/tray-icon-crash`). Never commit directly to `main`.
- **One feature, one commit (or a tight series of small commits).** Don't
  bundle unrelated changes into a single commit.
  Format: `<type>: <short description>` — e.g. `feat: add GPU temp polling`.
- **Test before merging.** Confirm the app runs and the specific
  feature/fix works before merging. "Tested" means, at minimum:
  - [ ] App launches with no unhandled errors/crashes on startup.
  - [ ] The specific feature/fix was exercised manually and produced the
    expected result (state what you did and what you observed — not just
    "tested, works").
  - [ ] For sensor/hardware-reading code specifically: confirm behavior on
    a missing-sensor case too (e.g. no discrete GPU, or a metric
    unavailable) — it should degrade gracefully (show "N/A" or similar),
    not crash the whole app.
  - An agent's self-report of "tested" is not sufficient on its own —
    it must state what was actually run/observed, above.
- **INDEX.md updates are part of the feature's diff** — see §2 Sync Order.
  This is not a separate/optional step.
- **Open a PR, even solo.** Push the branch, print the PR link, and stop.
  Do not create, fill out, submit, or merge the PR yourself, by browser
  automation or any other method, unless explicitly asked.
- **Never commit secrets.** Config files, API keys, and any local databases
  must stay gitignored. If a secret is ever accidentally committed and
  pushed, treat it as compromised — rotate it immediately.

### 5a. If Something Breaks (Rollback / Incident Procedure)

Git mistakes are almost always recoverable — nothing is destroyed just
because it looks broken. If any of the below happens, **stop and do not
try more commands to "fix" it** until the state is confirmed:

- **`main` gets broken by a bad merge:** don't force-push, don't merge
  again on top to "cover" it. Identify the last good commit on `main`
  (`git log`), and revert the bad merge commit (`git revert -m 1
  <merge-commit-hash>`) rather than resetting history, since `main` is
  shared. Flag this to the lead developer immediately — this is a "stop
  and ask" situation, not a "silently fix and continue" one.
- **A branch has diverged badly / local and remote disagree:** do not
  force-push to reconcile. Stop, run `git status` and `git log
  --oneline --graph --all`, and report the output rather than guessing
  at a fix.
- **An agent is unsure whether an action is destructive** (anything
  involving `--force`, `reset --hard`, or rewriting shared history):
  treat it as destructive by default and ask first.

---

## 6. Documentation Structure — What Exists and Why

- `INDEX.md` — state, File Manifest, truncated log (last 10 entries only).
  The primary document. Always current.
- `CHANGELOG.md` — archive for INDEX.md log entries once they exceed 10.
  Relocated verbatim, not summarized.
- `AGENTS.md` — this file. All rules in one place.
- `ARCHITECTURE.md` — **intentionally not created yet.** INDEX.md's File
  Manifest covers "where things are" for now. Once there's a real
  architectural split (e.g. sensor-polling backend vs. display frontend,
  possibly across different language ecosystems), create a lightweight
  ARCHITECTURE.md to record the *why* behind that decision — not before.
  This is deliberate, not an oversight: don't propose creating it just
  because "most projects have one."

## 7. Definition of Done

Before considering a task complete, verify:
- [ ] No debug print statements left in production code.
- [ ] Any new config/environment variables are documented.
- [ ] `INDEX.md` (Current Focus, File Manifest, Log Entries) is updated in
  the same commit/PR — see §2 Sync Order. Not optional.
- [ ] If INDEX.md's Log Entries table now exceeds 10 rows, the oldest
  entries have been moved to `CHANGELOG.md`.
