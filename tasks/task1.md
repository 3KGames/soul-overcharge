# Task 1: Resolve merge conflicts (ai ← main)

## Goal
Merge `main` into `ai` branch (via integration branch), resolve all conflicts, ensure Unity project compiles and builds.

## Context
- Current branch: `ai` (4 commits ahead of merge base)
- Target: `main` (1 commit ahead: f9f671d — procedural terrain generation)
- Merge base: `ec90b23`
- Conflicting files identified:
  1. `Assets/Game/Level/Runtime/RoadGenerator.cs` — textual + semantic conflicts
  2. `Assets/Game/Scenes/LevelSceneEGOR.unity` — YAML scene merge conflict

## Acceptance Criteria
- [ ] `git checkout -b integration/ai-main ai` executed
- [ ] `git merge main --no-commit` executed, conflicts present in working tree
- [ ] **RoadGenerator.cs resolved:**
  - [ ] Base = `ai` version (gridSettings architecture, rewritten PlaceSegment/LinkViews)
  - [ ] Merged from `main`: events SegmentPlaced/SegmentRemoved/GeneratorReset, methods WorldToCell/WorldToGrid/GetPlacedSegments/RoadSegmentInfo, flag _gridReady/IsGridReady
  - [ ] All `cellSize` references replaced with `gridSettings.cellSize` (with null-check)
  - [ ] Single segment-destruction event (unify NotifyRemoved + OnSegmentDestroying)
  - [ ] No compile errors in Unity
- [ ] **LevelSceneEGOR.unity resolved:**
  - [ ] Opens in Unity without errors
  - [ ] Contains terrain objects from `main` (TerrainGenerator, terrain chunks)
  - [ ] Contains AI spawners/debug setup from `ai`
  - [ ] Valid YAML (no merge markers)
- [ ] Unity build passes: `Unity -batchmode -quit -projectPath . -executeMethod BuildScript.Build` (or equivalent) → exit code 0
- [ ] `git add . && git commit -m "resolve: merge main into ai"`
- [ ] `git push origin integration/ai-main`

## Constraints
- Do NOT push to `main` or `ai` directly
- Work only on `integration/ai-main`
- Max 3 edit-build-fix iterations for compilation errors
- If scene resolution fails via YAML editing → use Unity CLI `-batchmode -executeMethod SceneMergeTool.Resolve`

## References
- `main` commit `f9f671d` — procedural terrain (28 files, ~3386 lines added)
- `ai` commits: c679ca8, 00a063b, 44b3b9c, f11b339
- RoadGenerator.cs conflict detail: DestroyNode() + cellSize API removal

## Definition of Done
All acceptance criteria checked ✓ + `@qa-agent` verdict `pass` on verification.
