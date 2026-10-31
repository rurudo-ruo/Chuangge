# Development plan — 窗格 (NoFences fork)

Tracking document for the fixes identified in the code review of 2026-10-31. Every finding from
that review appears below with a status, a phase and a verification expectation.

- **Created:** 2026-10-31
- **Source:** code review of the tree at `DevelopPlan.md` creation time (`Program.cs`, `FenceWindow`, `FenceLayout`, `FenceViewModel`, `FenceManager`, `ThumbnailProvider`, `Win32/*`, settings/theme, `build.ps1`, `release.yml`).
- **Build status:** **not verified (2026-10-31).** The agent still cannot spawn processes (`Windows Job runner exited with code 1`) and the ripgrep provider is unavailable, so none of the changes recorded here have been compiled or run. `.\build.ps1` on a normal machine is the first gate for every stage in §12.

---

## 1. Conventions

### Status values

| Status | Meaning |
|---|---|
| **Todo** | Identified, not started. |
| **Doing** | Implementation in progress in the current batch. |
| **Done** | Change applied and reviewed by inspection (or verified as already correct by the existing code). |
| **Won't fix** | Deliberately declined; rationale recorded in §7. |

### Phases

- **Phase A — no observable behavior change.** Refactors, internal performance work, resource
  ownership, dead-code removal, build configuration. The success path must produce *identical*
  pixels, files and window messages. Anything that changes staleness, timing, smoothing,
  failure UX or file layout belongs in Phase B or C.
- **Phase B — behavior fixes.** Changes that alter what a user observes, but only to correct a
  defect (stale "missing" state, distorted thumbnails, mangled paths, crash paths).
- **Phase C — hardening and process.** Security hardening, logging, CI, signing, accessibility.

### Ordering rule

Phase A first, then B, then C. Within a phase, higher impact first. A Phase A item that cannot be
proved behavior-neutral moves to Phase B rather than being forced.

### Definition of done

1. Change applied and self-reviewed against the listing in the review.
2. No new analyzer warning introduced (`WarningLevel 4` is the current baseline).
3. Manual smoke test covers: load existing layout, add/remove/reorder, drag between fences,
   resize/move/close, apply settings, move across DPI monitors.
4. `DevelopPlan.md` updated in the same change.

---

## 2. Status overview

| Phase | Items | Done | Doing | Todo | Won't fix |
|---|---|---|---|---|---|
| A — no observable behavior change | 19 | 15 | 4 | 0 | 0 |
| B — behavior fixes | 11 | 10 | 0 | 1 | 0 |
| C — hardening and process | 8 | 7 | 0 | 1 | 0 |
| D — already satisfied (verified) | 10 | 10 | 0 | 0 | 0 |
| W — won't fix | 2 | 0 | 0 | 0 | 2 |
| **Total** | **50** | **42** | **4** | **2** | **2** |

---

## 3. Phase A — no observable behavior change

Rationale: these changes remove work and risk from the hot path without changing output. They are
safe to land behind a build + smoke test and they de-risk Phase B (which edits the same regions).

| ID | Item | Where | Effort | Status |
|---|---|---|---|---|
| A1 | Cache one `XmlSerializer` instead of constructing per read/write | [FenceManager.cs:194–205](窗格/Model/FenceManager.cs#L194-L205), [269–278](窗格/Model/FenceManager.cs#L269-L278) | S | Done |
| A2 | Atomic metadata save: write `.tmp`, then `File.Replace` (no backup file, same file set) | [FenceManager.cs:269–278](窗格/Model/FenceManager.cs#L269-L278) | S | Done |
| A3 | One shared `StringFormat`; cache `MeasureString` result per item | [FenceWindow.cs:799–858](窗格/FenceWindow.cs#L799-L858) | M | Done |
| A4 | Invalidate only affected render-cache entries instead of clearing the world | [FenceWindow.cs:967–972](窗格/FenceWindow.cs#L967-L972) + call sites | S | Done |
| A5 | Drop redundant `SetClip`, invalidate the glow union once | [FenceWindow.cs:690](窗格/FenceWindow.cs#L690), [560–564](窗格/FenceWindow.cs#L560-L564) | S | Done |
| A6 | Build `FenceEntry.Name` once instead of per access | [FenceEntry.cs:17–23](窗格/Model/FenceEntry.cs#L17-L23) | S | Done |
| A7 | Precompute layout geometry (`columnCount`, strides) | [FenceLayout.cs:15–37](窗格/FenceLayout.cs#L15-L37) | S | Done |
| A8 | `ShellContextMenu`: `IDisposable`, destroy hidden window, guard `QueryInterface`, drop `catch { throw; }` | [ShellContextMenu.cs:36–49](窗格/Win32/ShellContextMenu.cs#L36-L49), [500–504](窗格/Win32/ShellContextMenu.cs#L500-L504) | M | Done |
| A9 | `IconUtil`: check `SHGetStockIconInfo` HRESULT, thread-safe lazy init | [IconUtil.cs:11–26](窗格/Win32/IconUtil.cs#L11-L26) | S | Done |
| A10 | Share one bold font instead of allocating one per label | [FluentTheme.cs](窗格/FluentTheme.cs), [AdvancedSettingsForm.cs](窗格/AdvancedSettingsForm.cs), [SettingsSectionControls.cs](窗格/SettingsSectionControls.cs) | S | Done |
| A11 | 64-bit-correct window-long access for `GWL_HWNDPARENT` (values preserved) | [DesktopUtil.cs:13–43](窗格/Win32/DesktopUtil.cs#L13-L43) | S | Done |
| A12 | Remove unused hook scaffolding and `catch { throw; }` noise | [ShellContextMenuSupport.cs:45–109](窗格/Win32/ShellContextMenuSupport.cs#L45-L109) | S | Done |
| A13 | Add `.editorconfig` documenting the existing conventions | new file | S | Done |
| A14 | Remove `HeightDialog`: csproj entries are gone; the five files still need deleting | [HeightDialog.cs](窗格/HeightDialog.cs) | M | Doing |
| A15 | Remove `Extensions.cs`: csproj entry is gone; the file still needs deleting | [Extensions.cs](窗格/Util/Extensions.cs) | S | Doing |
| A16 | Dispose designer controls before `Controls.Clear()` in `EditDialog` | [EditDialog.cs:60](窗格/EditDialog.cs#L60) | S | Done |
| A17 | Analyzers and `<TreatWarningsAsErrors>`. Warnings-as-errors is on; the analyzer package is deferred to §12 Stage 5 | [窗格.csproj](窗格/窗格.csproj) | M | Doing |
| A18 | Extract `FenceRenderer` / `FenceDragController` from the 1.3k-line window. Renderer is extracted (A18a); drag controller remains (A18b) | [FenceRenderer.cs](窗格/FenceRenderer.cs), [FenceWindow.cs](窗格/FenceWindow.cs) | L | Doing |
| A19 | Collapse duplicated save-error ladders into shared helpers | [FenceManager.cs:48–126](窗格/Model/FenceManager.cs#L48-L126), [FenceViewModel.cs:22–34](窗格/FenceViewModel.cs#L22-L34) | S | Done |

### Phase A notes

- **A2** intentionally produces **no backup file**, so the persisted file set under
  `%LOCALAPPDATA%\NoFences\<id>` is unchanged. A `.tmp` exists only for the duration of a save and
  is removed on failure. Last-known-good retention is a Phase C decision.
- **A3** covered the `StringFormat` and the measurement only; per-item brushes/pens were still
  allocated per frame at the time and are now handled by **B10**.
- **A4** keeps a full clear for DPI and settings changes, because item sizes, radii and fonts
  genuinely change there. DnD, delete and replace now evict just the affected paths.
- **A6** freezes the localized "missing" suffix at construction. `Type` is immutable, so the
  produced string is identical to the lazily computed one.
- **A8** changes only resource lifetime; the menu's normal rendering path is untouched.
- **A11** preserves every style value; only the width of the handle used for `GWL_HWNDPARENT`
  changes.
- **A14/A15** are out of the project file, but the six files are still on disk: this session has no
  delete tool. Remove them with
  `git rm 窗格/HeightDialog.cs 窗格/HeightDialog.Designer.cs 窗格/HeightDialog.cs.resx 窗格/HeightDialog.resx 窗格/HeightDialog.zh-CN.resx 窗格/Util/Extensions.cs`.
  Until then the build ignores them, so they cannot break compilation.

---

## 4. Phase B — behavior fixes

| ID | Item | Where | Effort | Status |
|---|---|---|---|---|
| B1 | Cache resolved entries; refresh on pointer entry and after content changes instead of per paint | [FenceWindow.cs:1048–1102](窗格/FenceWindow.cs#L1048-L1102), [FenceEntry.cs:26–33](窗格/Model/FenceEntry.cs#L26-L33) | M | Done |
| B2 | Throttle the pointer glow and pin its draw position to the invalidated region | [FenceWindow.cs:1131–1141](窗格/FenceWindow.cs#L1131-L1141) | M | Done |
| B3 | Case-insensitive extension matching (fixes `.PNG` falling back to a shell icon) | [ThumbnailProvider.cs:26–36](窗格/Util/ThumbnailProvider.cs#L26-L36) | S | Done |
| B4 | Correct the legacy JSON fallback so `\t`, `\n`, `\b`, `\f`, `\r` paths are not silently mangled | [FenceManager.cs:204–246](窗格/Model/FenceManager.cs#L204-L246) | M | Done |
| B5 | Harden cross-thread marshalling (check-then-`BeginInvoke` race could terminate the process) | [FenceWindow.cs:1396–1436](窗格/FenceWindow.cs#L1396-L1436) | S | Done |
| B6 | Case-insensitive `ReplaceItem` lookup (was ordinal and silently missed) | [FenceViewModel.cs:186–190](窗格/FenceViewModel.cs#L186-L190) | S | Done |
| B7 | Window-message correctness: suppress every `WM_NCCALCSIZE` pass, remove `WM_NCMOUSELEAVE`, mask `SC_MAXIMIZE` | [FenceWindow.cs:206–224](窗格/FenceWindow.cs#L206-L224) | S | Done |
| B8 | Bound the thumbnail cache (LRU) and drain replaced icons on the UI thread | [ThumbnailProvider.cs](窗格/Util/ThumbnailProvider.cs) | M | Done |
| B9 | Stop forcing a synchronous repaint on every resize/minify | [FenceWindow.cs:544–554](窗格/FenceWindow.cs#L544-L554), [691–700](窗格/FenceWindow.cs#L691-L700) | S | Done |
| B10 | Hoist per-item brushes/pens into theme-scoped fields | [FenceWindow.cs:871–900](窗格/FenceWindow.cs#L871-L900) | S | Done |
| B11 | Composite fence content into an offscreen bitmap so glow-only paints blit instead of re-rendering items | [FenceWindow.cs](窗格/FenceWindow.cs) | M | Todo |

### Phase B acceptance notes

- **B1** is implemented as a per-path entry cache, refreshed when the pointer enters the fence
  (throttled to once per second) and after add/remove/replace. The plan's 2–5 s timer was dropped:
  on an offline UNC share it would reintroduce a periodic UI-thread stall. A path that is not yet
  cached still costs one probe on its first paint. File changes are picked up on the next entry.
- **B2** is a 16 ms throttle that draws the halo only at a position that was just invalidated, so
  the previous frame is always erased. Frame cost during a glow repaint still includes the items
  under the halo; **B11** is the follow-up that removes that entirely.
- **B4** retries with all backslashes doubled only when strict parsing fails or a parsed path
  contains a control character. Mixed-escape legacy files remain best effort and are documented
  as such.
- **B8** keeps the retirement list (disposing an icon from a worker thread could race a paint) but
  drains it on the UI thread from the thumbnail-loaded handler. Pending entries are never evicted.
- **B11** needs pixel-level regression testing, which this session cannot run; it is therefore
  not started.
- **B7** needed a correction after it shipped. The first version let the `wParam == 0`
  `WM_NCCALCSIZE` pass reach the default handler, so Windows reserved and painted a native
  caption containing the window text over every fence. Every pass is suppressed again — that is
  what the pre-review code did unconditionally.

---

## 5. Phase C — hardening and process

| ID | Item | Where | Effort | Status |
|---|---|---|---|---|
| C1 | Startup shortcut via `IShellLink`/`IPersistFile` (no Windows Script Host) | [StartupRegistration.cs](窗格/Util/StartupRegistration.cs), [ShellLink.cs](窗格/Win32/ShellLink.cs) | M | Done |
| C2 | Cap metadata read size before parsing (`fence.json`, XML) | [FenceManager.cs:204–240](窗格/Model/FenceManager.cs#L204-L240) | S | Done |
| C3 | Global exception boundary + file logging; replace silent `Console.WriteLine` | [Program.cs](窗格/Program.cs), [AppLogger.cs](窗格/Util/AppLogger.cs) | M | Done |
| C4 | Publish SHA-256 checksums with releases | [release.yml](.github/workflows/release.yml), [README.md](README.md) | M | Done |
| C5 | Keyboard and UI Automation access for fence items | [FenceWindow.cs](窗格/FenceWindow.cs), [WindowUtil.cs:148–154](窗格/Win32/WindowUtil.cs#L148-L154) | L | Todo |
| C6 | Test project (`FenceLayout`, resource keys) + pull-request workflow | [NoFences.Tests.csproj](tests/NoFences.Tests/NoFences.Tests.csproj), [build.yml](.github/workflows/build.yml) | L | Done |
| C7 | Harden `build.ps1` error handling (CIM query and process-exit race) | [build.ps1](build.ps1) | S | Done |
| C8 | Launch via `ProcessStartInfo` with no shell argument parsing | [ShellLaunch.cs](窗格/Util/ShellLaunch.cs) | S | Done |

### Phase C acceptance notes

- **C1** removes the `WScript.Shell` ProgID from the trust path, but the interop can only be
  exercised by pressing **Apply** with the startup option in a running build. Check the created
  `.lnk` target and working directory after the first run.
- **C3** logs exceptions, save failures, startup load counts and skipped paths to
  `%LOCALAPPDATA%\NoFences\logs`. Item paths are included, truncated to 1000 characters. Unhandled
  UI exceptions are logged, reported with the log folder, and the app exits through the normal
  close path so fences save.
- **C4** publishes `SHA256SUMS.txt`; the release step uploads `release-assets/*`, so no extra
  wiring was needed.
- **C6** package versions and `dotnet test` against the legacy (non-SDK) app project are
  **unverified** — the first real check is the new `Build and test` workflow. The test project is
  not in `窗格.sln`, so `build.ps1` and the release pipeline are unaffected.
- **C5** is the only remaining Phase C item; it needs an interaction design (keyboard focus mode,
  item navigation, UIA peers), not just code.

---

## 6. Already satisfied (verified done)

Findings from the review that the current code already handles correctly. These need no work; they
are recorded so a future pass does not "fix" them into a regression.

| ID | Property | Evidence | Status |
|---|---|---|---|
| D1 | Legacy XML parsing is not XXE-exposed | `XmlSerializer` does not process DTDs ([FenceManager.cs:194–205](窗格/Model/FenceManager.cs#L194-L205)) | Done |
| D2 | JSON parsing binds to a fixed type (no type confusion) | `Deserialize<FenceInfo>` ([FenceManager.cs:212–214](窗格/Model/FenceManager.cs#L212-L214)) | Done |
| D3 | Fence deletion cannot escape the data directory | GUID-scoped `Path.Combine` ([FenceManager.cs:251–267](窗格/Model/FenceManager.cs#L251-L267)) | Done |
| D4 | Close is cancelled when the final save fails | [FenceWindow.cs:283–306](窗格/FenceWindow.cs#L283-L306) | Done |
| D5 | In-memory rollback on save failure, original exception preserved | [FenceViewModel.cs:129–237](窗格/FenceViewModel.cs#L129-L237) | Done |
| D6 | Debounce re-arms, flushes on close, restores pending work on error | [DebouncedAction.cs](窗格/Util/DebouncedAction.cs) | Done |
| D7 | Image decoding concurrency is bounded | `SemaphoreSlim(4)` ([ThumbnailProvider.cs:44](窗格/Util/ThumbnailProvider.cs#L44)) | Done |
| D8 | Painting is culled to the visible item range | [FenceLayout.cs:64–86](窗格/FenceLayout.cs#L64-L86) | Done |
| D9 | Per-monitor V2 DPI and long-path awareness are declared | [app.manifest:49–53](窗格/app.manifest#L49-L53) | Done |
| D10 | Release workflow verifies the Microsoft installer signature | [release.yml:65–69](.github/workflows/release.yml#L65-L69) | Done |

---

## 7. Won't fix

| ID | Item | Rationale |
|---|---|---|
| W1 | Rename the `fenceInfo` property to PascalCase | [FenceWindow.cs:28](窗格/FenceWindow.cs#L28). The constructor parameter of the same name is captured by the property after assignment; a mechanical rename can silently rebind it. Cosmetic only — revisit if/when **A18** splits the window. |
| W2 | Replace the Gaussian blur with a box blur / SIMD implementation | The blur is cached per item and runs on change, not per frame. **A4** removes the remaining repeated invocations; a kernel rewrite is disproportionate to the measured cost. |

---

## 8. Findings recorded against the plan

Every review finding, the defect it describes, and the plan item that closes it.

| Review ID | Finding | Plan item(s) |
|---|---|---|
| P1 | `File.Exists`/`Directory.Exists` run for every visible item on every paint; on a slow or offline share this blocks the UI thread | A6, B1 |
| P2 | A cosmetic pointer glow repaints the items underneath at mouse-move frequency (enabled by default) | A5, B2 |
| P3 | Per-item, per-frame `StringFormat`/`Brush`/`Pen`/`GraphicsPath` allocation plus `MeasureString` | A3, B10 |
| P4 | Every drop, reorder and delete clears the whole render cache, so blur is recomputed inline during paint | A4 |
| P5 | `new XmlSerializer` on every read and every debounced save | A1 |
| P6 | Icon cache keyed by path×size with no eviction; replaced icons retained in `retiredIcons` until the window closes | B8 |
| P7 | Culture-sensitive `EndsWith` plus LINQ in the icon hot path; `.PNG` is treated as unsupported | B3 |
| P8 | `Refresh()` inside `Resize`/`Minify` forces a synchronous full repaint per size event | B9 |
| P9 | Layout object allocated per probe; column count divided again for every item | A7 |
| B1 | Metadata written in place; an interrupted write truncates the only copy and the fence is dropped on next load | A2 |
| B2 | The JSON "repair" regex leaves valid escapes (`\t`, `\n`, `\b`, `\f`, `\r`) intact, silently corrupting legacy paths | B4 |
| B3 | Checking `IsDisposed`/`IsHandleCreated` before `BeginInvoke` races with teardown; the pool-thread exception terminates the process | B5 |
| B4 | 32-bit `SetWindowLong` used for `GWL_HWNDPARENT` on an x64 build; `WM_NCCALCSIZE`, `WM_NCMOUSELEAVE` and `SC_MAXIMIZE` handled incorrectly | A11, B7 |
| B5 | 1,366-line window with ~40 fields; the same save-error ladder repeated three times | A18, A19 |
| B6 | COM released from a finalizer, hidden window never destroyed, unchecked `QueryInterface`, unchecked icon HRESULT, per-label font allocation, orphaned designer controls | A8, A9, A10, A16 |
| B7 | ~400 lines with no callers | A12, A14, A15 |
| B8 | Property named like a field; culture-sensitive comparisons where identity is meant; magic literals; failures written to a non-existent console | A12, B6, C3, W1 |
| B9 | No global exception handler and no logging; startup can die with an opaque `TypeInitializationException` | C3, A17 |
| B10 | No tests, analyzers, warnings-as-errors or pull-request CI; brittle build-script error paths | A13, A17, C6, C7 |
| B11 | Fully custom-drawn, never-focused window with no keyboard or UI Automation path | C5 |
| S1 | `WScript.Shell` resolved through a user-writable ProgID and instantiated in-process on Apply | C1 |
| S2 | Unbounded `ReadAllText` before JSON parsing; oversized metadata is a startup DoS | C2 |
| S3 | Shell context-menu COM lifetime can crash the process instead of failing the menu | A8, B5 |
| S4 | Release ships an unsigned executable and publishes no checksum | C4 |
| S5 | Raw strings passed to `explorer.exe` without explicit quoting or `ProcessStartInfo` | C8 |

---

## 9. Verification checklist

Because the project has no automated tests, every phase closes on this manual pass:

1. `.\build.ps1` succeeds (Release, x64).
2. Existing layout loads: fences, names, positions, sizes, item order.
3. Add items by drag from Explorer; remove; reorder within a fence; move between fences.
4. Resize, move, close and reopen a fence; reopen the app and confirm geometry persisted.
5. Apply every Advanced setting; confirm fences redraw and settings survive a restart.
6. Drag a fence across monitors with different DPI; confirm size and hit targets.
7. Missing-item flow: delete a referenced file, reopen the fence, locate a replacement.
8. Inspect `%LOCALAPPDATA%\NoFences\<id>`: only `__fence_metadata.xml` is present after a save
   (no `.tmp` left behind).
9. Sweep the pointer across a fence continuously: the halo follows without leaving trails, and
   dragging an item still repaints immediately.
10. Delete a referenced file while the fence is open, leave and re-enter it: the item turns amber
    (B1 refresh path). Add the file back and re-enter: it renders normally again.
11. Toggle "Start NoFences when I sign in", press **Apply**, and confirm the shortcut exists in the
    Startup folder with the correct target and working directory (C1).
12. Make a fence folder read-only, change a fence, and confirm a log file appears under
    `%LOCALAPPDATA%\NoFences\logs` and the fence stays open with an error dialog (C3).

Phase A adds one extra check: **compare before/after screenshots** of a fence with a mixed set of
items (image, exe, folder, missing) — the rendering must be pixel-identical.

---

## 10. Decision log

| Date | Decision | Reason |
|---|---|---|
| 2026-10-31 | Status is a table column (`Todo`/`Doing`/`Done`/`Won't fix`), not checkboxes | Chosen during planning; tabular form suits the traceability and phase columns. |
| 2026-10-31 | Phase A boundary = strictly identical observable behavior | Perf caches and glow throttling change staleness/smoothness, so they are deferred to Phase B. |
| 2026-10-31 | Scope of the first sitting: plan + full Phase A batch, then stop for review | Keeps the reviewable diff focused on provably safe changes. |
| 2026-10-31 | A14/A15 (file deletion) deferred | No compiler available; file/csproj removal cannot be verified by inspection. |
| 2026-10-31 | B1 refreshes on pointer entry, not on a timer | A timer would block the UI thread on an offline share every interval; entry/refresh-on-change keeps the steady-state paint path free of I/O. |
| 2026-10-31 | B2 throttles instead of compositing | Offscreen compositing needs pixel-level regression testing that this session cannot run; recorded as B11. |
| 2026-10-31 | B8 keeps the retirement list but drains it on the UI thread | Preserves the original cross-thread safety while bounding retained icon handles. |
| 2026-10-31 | C4 is satisfied with published SHA-256 checksums, not code signing | No signing certificate is available; checksums give verifiable integrity without a signing identity. |
| 2026-10-31 | The diagnostic log includes item paths, truncated to 1000 characters | Chosen for supportability; the log stays on the local machine. |
| 2026-10-31 | C1 applied without runtime verification | The `IShellLink` path can only be exercised by pressing Apply in a running build. |
| 2026-10-31 | C6 package versions and `dotnet test` against the legacy project are unverified | No compiler or SDK access in this session; the pull-request workflow is the first real check. |

---

## 11. Changelog

| Date | Change |
|---|---|
| 2026-10-31 | Plan created. |
| 2026-10-31 | Phase A implemented (A1–A13). Build and smoke test still outstanding — see §9. |
| 2026-10-31 | Phase B implemented (B1–B10). B11 added for offscreen compositing. Build and smoke test still outstanding — see §9. |
| 2026-10-31 | Phase C implemented (C1–C4, C6–C8); C5 remains. Test project and CI added but never executed here. |
| 2026-10-31 | Fixed: fence names could surface through the window text while titles were hidden. The window text is now the product name; the fence name is drawn and edited from the model only. |
| 2026-10-31 | Fixed regression from B7: suppressing only one `WM_NCCALCSIZE` pass let Windows paint a native caption over every fence. All passes are suppressed again. |
| 2026-10-31 | Stage 1 applied: shared load and save-error helpers (A19), dialog control disposal (A16), dead files out of the project (A14/A15), warnings-as-errors (A17a). Physical file deletion and the first build remain. |
| 2026-10-31 | A18a applied: `FenceRenderer` owns item drawing, the effect caches, the icon font and item brushes; renderer calls now pass `itemHeight` and the selection flag. A18b (drag controller) remains. |

---

## 12. Remaining work plan

**Backlog: 4 in progress (A14, A15, A17, A18a), 2 not started (B11, C5), 42 done, 2 won't fix.**
Stage 1 is applied except for the physical file deletion, which needs a shell. Every stage below
closes on a Release build; the agent's process runner is still broken in this session, so the build
is the user's step, not the agent's.

### Stage 1 — Cleanup batch (S–M, one build verifies all)

Order matters: A14 removes the second of A16's two sites, and deletions shrink the surface that
the later refactors touch.

1. **A19** — Collapse the three identical load ladders in `FenceManager.LoadFences` into one
   `TryLoad(Func<FenceInfo>)` helper, and share the save-error predicate between `FenceWindow`
   and `AdvancedSettingsForm`. No behavior change.
2. **A14** — Delete `HeightDialog` (`.cs`, `.Designer.cs`, three `.resx`, csproj entries). Confirm
   with Find Usages first; title size now lives in `FenceTitleSettingsControl`.
3. **A15** — Delete `Util\Extensions.cs` and its csproj entry; `Move`/`Shrink` have no callers.
4. **A16** — Dispose designer controls before `Controls.Clear()` in `EditDialog` (the
   `HeightDialog` half disappears with A14).
5. **A17a** — Enable `<TreatWarningsAsErrors>` at the current warning level, with no new packages
   and no restore step, and fix what it reports.

*Done when:* Release build clean with warnings-as-errors; both dialogs open and close normally; no
orphan files; this document updated.

### Stage 2 — Window structure (L, ~1–2 days)

**A18** — Split into two commits. **A18a** (done): per-item drawing, the effect caches, the icon
font and the item brushes moved into [`FenceRenderer`](窗格/FenceRenderer.cs); `FenceWindow` keeps
layout, background, title, scroll bar, drag marker and the pointer halo. **A18b** (remaining):
thumbnail drag, drop targeting and the drag effects move into `FenceDragController`.

*Done when:* the §9 checklist passes, plus before/after screenshots of a mixed fence (image, exe,
folder, missing item, selection, drag marker, scrollbar).

### Stage 3 — Compositing (M, ~1 day plus testing)

**B11** — Cache fence content (background, items, scrollbar, drag marker) in an offscreen bitmap;
a glow-only paint blits it and draws the halo. Invalidate that bitmap from every content-changing
path: settings, DPI, scroll, selection, DnD, drop/delete/replace, thumbnail load.

*Done when:* a pointer sweep leaves no trails and no stale content in any of those paths, and paint
time during a glow sweep drops measurably (temporary `Stopwatch` instrumentation).

### Stage 4 — Accessibility (L, ~2–3 days)

**C5** — After A18: (a) keyboard path — focus a fence from the tray or settings, arrow between
items, Enter opens, Menu key opens the item menu; (b) UI Automation peers for items; (c) document
the shortcuts. Needs one interaction decision first: how a fence takes focus without stealing it
from the desktop.

*Done when:* the app is operable with the keyboard alone, and UIA inspection reports one peer per
item.

### Stage 5 — Analyzers (S, ~0.5 day)

**A17b** — Add `Microsoft.CodeAnalysis.NetAnalyzers` as a `PackageReference`, add `-restore` to
`build.ps1`, and fix the first round of findings. Kept separate from A17a because it needs NuGet
restore, which the build script does not currently perform.

### Why this order

- Deletions and the error-ladder collapse are cheap and shrink everything that follows.
- A18 must precede B11 and C5; both touch code that is entangled in `FenceWindow` today.
- B11 before C5 because C5 adds focus rendering to the same paint path.
- Warnings-as-errors early (A17a) so Stages 2–4 cannot silently add warnings.

The two won't-fix items (W1 property rename, W2 blur kernel rewrite) stay declined; see §7.

| Stage | Items | Effort | Gate |
|---|---|---|---|
| 1 | A19, A14, A15, A16, A17a | S–M | one clean Release build |
| 2 | A18 | L | Stage 1 verified |
| 3 | B11 | M | A18 merged |
| 4 | C5 | L | A18 merged; focus design agreed |
| 5 | A17b | S | NuGet restore added to the build |
