---
name: dev-doc
description: >-
  Create and maintain Partyament development-planning docs ("TaskLists"). Use this whenever the
  user hands over multiple feature ideas, a large or multi-part feature, a batch of bugfixes, or
  any request big enough that it should be split into several tasks that need grouping, ordering,
  and smoketests. It produces a single self-contained HTML doc under
  documentation/development_docs/ with a collapsible task grid and ordered implementation groups.
  Also use it to advance that workflow: when the user says things like "Implement Group_01_A"
  (start a group's tasks) — which works even in a brand-new window — or when a finished group is
  green-lit and statuses/folders need updating (planned -> in_progress -> completed). You MAY
  trigger this yourself, without being asked, when an idea the user gives is clearly best handled
  by splitting it into grouped tasks. Do NOT use it for small one-off tweaks like "move this
  button up a little", "rename this label", or a single quick bugfix — just do those directly.
---

# Partyament development docs (TaskLists)

This skill runs a lightweight planning + tracking workflow for anything bigger than a one-off
tweak. The output is a browser-openable HTML doc that turns a pile of ideas into concrete tasks,
groups them into efficient implementation runs, and tracks status through to completion.

## Where things live

```
documentation/development_docs/
├── planned/        # freshly created docs, nothing started yet
├── in_progress/    # the first group has been started
└── completed/      # all groups implemented and green-lit by the user
```

A doc moves between these folders over its life (see **Lifecycle**). Move it with `git mv` when the
repo is clean, otherwise a plain move — the folder a doc sits in is the source of truth for its
overall status, so keep it in sync with the `data-doc-status` attribute and the header badge.

## Naming conventions

- **Doc / file:** `TaskList_XX.html` — `XX` iterates `01, 02, …` across *all* docs, in any folder.
  Pick the next number by scanning `documentation/development_docs/**/TaskList_*.html` for the
  highest `XX` and adding one.
- **Task (within a doc):** `Task_YY` — `YY` iterates `01, 02, …` within that doc.
- **Task (cross-doc reference):** `Task_XX_YY` (e.g. `Task_03_02` = Task_02 of TaskList_03).
- **Group (within a doc):** `Group_Z` — `Z` is the implementation letter `A, B, C, …`, i.e. the
  order groups are done in.
- **Group (cross-doc reference):** `Group_XX_Z` (e.g. `Group_01_A` = Group A of TaskList_01).

When the user references `Task_XX_YY` or `Group_XX_Z`, resolve `XX` to `TaskList_XX.html` (search
all three folders) and act on the named task/group inside it.

## Before writing a doc — resolve UX/UI questions first

If any **UX or UI** element of the request is unclear — how something should look, where it goes,
what the interaction feels like, what happens in an edge case the user hasn't described — **ask the
user before writing the doc.** Do not guess at UX; a wrong assumption costs a whole re-plan. Batch
the questions and use the AskUserQuestion tool when there are discrete options.

Keep **technical** questions to yourself and decide them from the code — unless a decision is
genuinely blocking, or it concerns something only visible **inside the Unity Editor** (a specific
GameObject, prefab, component wiring, scene layout). For those, drive the live Editor to find the
answer if it is reachable (see the `unity-cli` skill / `unity status`), and only ask the user if
that fails.

## Creating a doc

1. **Determine the number** `XX` (see conventions) and the theme/title.
2. **Research the codebase** so the technical sections are real, not hand-wavy. Read the relevant
   scripts, scenes, and CSVs; if the Unity Editor is running, inspect actual GameObjects/components
   rather than guessing. Every task's technical section should name the concrete files to touch and
   the actual changes.
3. **Copy the template** at `assets/template.html` (resolve this skill's own directory) to
   `documentation/development_docs/planned/TaskList_XX.html` and fill it in. Keep the file
   self-contained — no external assets — so it opens straight from disk.
4. **Write one task per discrete unit of work.** Each task in the grid carries: local id, short
   title, **Scope** (S/M/L), **Difficulty** (Easy/Medium/Hard), **Depends on** (task ids or `—`),
   and **Status**. The collapsible detail row holds the description, in this order:
   - **UX** — plain-language: what the player sees/does and how it should feel.
   - **Technical** — what actually changes, the files to touch (`Assets/Scripts/…`,
     `MainScene.unity`, CSVs, etc.), and a short bullet list of concrete changes. Note real
     dependencies on other tasks here too.
5. **Group the tasks** into implementation runs below the grid. Put together tasks that share
   scope, touch the same files, or can be done in one pass **without ballooning a single window's
   context/scope.** Order the groups so every dependency is satisfied by an earlier group. Groups
   are `Group_A`, `Group_B`, … in that order; mark the group to be done next with
   `class="group next"`.
6. **Add a VERY BRIEF smoketest** to each group — a few bullets naming exactly what the user should
   click/verify in Unity after that group lands. This is the whole verification contract, so keep
   it short and concrete. Assume the user will refresh Unity and smoketest between groups.
7. Set the doc's `data-doc-status="planned"`, the header badge to Planned, and the Created/Updated
   dates. Then tell the user the doc is ready and (optionally) open it in the browser so they can
   review before implementation.

## Implementing a group

Triggered by e.g. "Implement Group_01_A" (works cold, in a new window):

1. Open `TaskList_XX.html`, read the named group and every task in it (expand their details).
2. If this is the **first** group to be started for the doc and it still lives in `planned/`, move
   the doc to `in_progress/` and set `data-doc-status` + header badge to In Progress.
3. Set the group's badge and each of its tasks' status to **In Progress**, and bump the Updated
   date.
4. Implement all tasks in the group, respecting the dependency order recorded in the doc. Follow
   the repo's normal working style (drive the live Editor for scene/asset work when it's reachable;
   edit source directly otherwise — see [[feedback_yaml_editing]]).
5. When the code compiles cleanly, **stop and hand the group's smoketest to the user** verbatim
   from the doc, plus anything you changed that they should eyeball. Do **not** mark tasks
   Completed yet — wait for their green light.
6. On green light: set those tasks and the group to **Completed**, bump Updated. If **all** groups
   in the doc are now Completed, move the doc to `completed/` and set `data-doc-status` +
   header badge to Completed.

## Updating status in the HTML

Statuses are plain markup, edit them in place:
- Task/group badge: swap the class `st-planned` / `st-in_progress` / `st-completed` / `st-blocked`
  and the label text.
- Doc level: `data-doc-status` on `<html>`, the header badge, and the containing folder must agree.
- Always update the **Updated** date in the meta line when you change anything.

## Scope/difficulty guidance

- **Scope** is size of the change: S = a small localized edit; M = a few files / a feature slice;
  L = broad, cross-cutting, or new systems.
- **Difficulty** is how tricky/risky it is regardless of size: Easy = mechanical; Medium = some
  design or careful wiring; Hard = uncertain, easy to get wrong, or needs verification.
- Keep them honest — the grid is what the user scans to decide what to green-light.
