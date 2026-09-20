# Development docs (TaskLists)

Planning + tracking docs for features, updates, and bugfix batches. Each doc is a single,
self-contained **HTML file** you can open straight in a browser (no server needed). They are
generated and maintained by the `dev-doc` Claude Code skill (`.claude/skills/dev-doc/`).

## Folders (a doc's overall status = the folder it's in)

| Folder | Meaning |
|---|---|
| `planned/` | Created, nothing started yet. |
| `in_progress/` | The first implementation group has been started. |
| `completed/` | Every group implemented and green-lit after smoketesting. |

## What's in a doc

- **Task grid** — each row is a task with Scope, Difficulty, Dependencies, and Status. Click a
  row's arrow to expand its description: a plain-language **UX** section, then a **Technical**
  section naming the files to touch and the concrete changes.
- **Implementation groups** — tasks bundled into efficient runs, ordered so dependencies come
  first. Each group ends with a short **smoketest**: what to click/verify in Unity after that
  group lands.

## Naming

- Doc/file: `TaskList_XX.html` (`XX` = 01, 02, … across all docs)
- Task: `Task_YY` locally, `Task_XX_YY` when referenced from elsewhere
- Group: `Group_Z` (A, B, C … = implementation order), `Group_XX_Z` when referenced from elsewhere

## Working with them

Give Claude a batch of ideas to get a new doc. To build one bundle of work, say e.g.
**"Implement Group_01_A"** — that starts every task in Group A of `TaskList_01`. After each group
Claude stops so you can refresh Unity and smoketest; once you approve, it marks things complete and
moves the doc along.
