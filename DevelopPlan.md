# Development plan — 窗格 (NoFences fork)

Tracking document for the fixes identified in the code review of 2026-10-31. Every finding from
that review appears below with a status, a phase and a verification expectation.

- **Created:** 2026-10-31
- **Source:** code review of the tree at `DevelopPlan.md` creation time (`Program.cs`, `FenceWindow`, `FenceLayout`, `FenceViewModel`, `FenceManager`, `ThumbnailProvider`, `Win32/*`, settings/theme, `build.ps1`, `release.yml`).
- **Build status:** **not verified.** The review session had no working shell (`pwsh` and the ripgrep providers failed), so no change recorded here has been compiled or run. Run `.\build.ps1` before merging Phase A.

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
| A — no observable behavior change | 19 | 13 | 0 | 6 | 0 |
| B — behavior fixes | 10 | 0 | 0 | 10 | 0 |
| C — hardening and process | 8 | 0 | 0 | 8 | 0 |
| D — already satisfied (verified) | 10 | 10 | 0 | 0 | 0 |
| W — won't fix | 2 | 0 | 0 | 0 | 2 |
| **Total** | **49** | **23** | **0** | **24** | **2** |

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
| A14 | Delete `HeightDialog` (no callers found; title size lives in Advanced settings) | [HeightDialog.cs](窗格/HeightDialog.cs) + `.Designer.cs` + 3 `.resx` + csproj entries | M | Todo |
| A15 | Delete `Extensions.cs` (no callers for `Move`/`Shrink`) | [Extensions.cs](窗格/Util/Extensions.cs) + csproj entry | S | Todo |
| A16 | Dispose designer controls before `Controls.Clear()` in dialogs | [EditDialog.cs:60](窗格/EditDialog.cs#L60), [HeightDialog.cs:88](窗格/HeightDialog.cs#L88) | S | Todo |
| A17 | Add analyzers and `<TreatWarningsAsErrors>` | [窗格.csproj](窗格/窗格.csproj) | M | Todo |
| A18 | Extract `FenceRenderer` / `FenceDragController` from the 1.3k-line window | [FenceWindow.cs](窗格/FenceWindow.cs) | L | Todo |
| A19 | Collapse duplicated save-error ladders into shared helpers | [FenceManager.cs:58–177](窗格/Model/FenceManager.cs#L58-L177), [FenceWindow.cs:494–497](窗格/FenceWindow.cs#L494-L497) | S | Todo |

### Phase A notes

- **A2** intentionally produces **no backup file**, so the persisted file set under
  `%LOCALAPPDATA%\NoFences\<id>` is unchanged. A `.tmp` exists only for the duration of a save and
  is removed on failure. Last-known-good retention is a Phase C decision.
- **A3** covers the `StringFormat` and the measurement only; per-item brushes/pens are still
  allocated per frame and are tracked as **B10**.
- **A4** keeps a full clear for DPI and settings changes, because item sizes, radii and fonts
  genuinely change there. DnD, delete and replace now evict just the affected paths.
- **A6** freezes the localized "missing" suffix at construction. `Type` is immutable, so the
  produced string is identical to the lazily computed one.
- **A8** changes only resource lifetime; the menu's normal rendering path is untouched.
- **A11** preserves every style value; only the width of the handle used for `GWL_HWNDPARENT`
  changes.
- **A14/A15** are deferred rather than done because deleting files and csproj entries is the one
  class of change that cannot be validated by inspection alone, and this session had no compiler.

---

## 4. Phase B — behavior fixes

| ID | Item | Where | Effort | Status |
|---|---|---|---|---|
| B1 | Cache resolved entries (file existence) and refresh on activation/timer instead of per paint | [FenceWindow.cs:737–742](窗格/FenceWindow.cs#L737-L742), [FenceEntry.cs:26–33](窗格/Model/FenceEntry.cs#L26-L33) | M | Todo |
| B2 | Throttle or composite the pointer glow so it stops repainting items | [FenceWindow.cs:560–564](窗格/FenceWindow.cs#L560-L564) | M | Todo |
| B3 | Case-insensitive extension matching (fixes `.PNG` falling back to a shell icon) | [ThumbnailProvider.cs:51–54](窗格/Util/ThumbnailProvider.cs#L51-L54) | S | Todo |
| B4 | Correct the legacy JSON fallback so `\t`, `\n`, `\b`, `\f`, `\r` paths are not silently mangled | [FenceManager.cs:21–23](窗格/Model/FenceManager.cs#L21-L23), [207–216](窗格/Model/FenceManager.cs#L207-L216) | M | Todo |
| B5 | Harden cross-thread marshalling (check-then-`BeginInvoke` race can terminate the process) | [FenceWindow.cs:1227–1241](窗格/FenceWindow.cs#L1227-L1241), [1248–1259](窗格/FenceWindow.cs#L1248-L1259) | S | Todo |
| B6 | Case-insensitive `ReplaceItem` lookup (currently ordinal and silently misses) | [FenceViewModel.cs:186–205](窗格/FenceViewModel.cs#L186-L205) | S | Todo |
| B7 | Window-message correctness: `WM_NCCALCSIZE` return, `WM_NCMOUSELEAVE` removal, `SC_MAXIMIZE` value | [FenceWindow.cs:170–188](窗格/FenceWindow.cs#L170-L188) | S | Todo |
| B8 | Bound the thumbnail cache (LRU) and dispose replaced icons | [ThumbnailProvider.cs:44–46](窗格/Util/ThumbnailProvider.cs#L44-L46), [155–206](窗格/Util/ThumbnailProvider.cs#L155-L206) | M | Todo |
| B9 | Stop forcing a synchronous repaint on every resize/minify | [FenceWindow.cs:505–515](窗格/FenceWindow.cs#L505-L515), [636–645](窗格/FenceWindow.cs#L636-L645) | S | Todo |
| B10 | Hoist per-item brushes/pens into theme-scoped fields | [FenceWindow.cs:824–856](窗格/FenceWindow.cs#L824-L856) | S | Todo |

### Phase B acceptance notes

- **B1** must preserve the current "missing item turns amber without reopening the fence"
  behaviour; refresh on `WM_ACTIVATE` and on a 2–5 s timer, never inside `Paint`.
- **B4** must be validated against a real legacy `fence.json` containing a path with `\t` and one
  with `\n`; a wrong fallback silently points the fence at a nonexistent path.
- **B5** is failure-path only but is user-visible as a crash, hence Phase B.

---

## 5. Phase C — hardening and process

| ID | Item | Where | Effort | Status |
|---|---|---|---|---|
| C1 | Replace late-bound `WScript.Shell` with `IShellLink`/`IPersistFile` | [StartupRegistration.cs:28–46](窗格/Util/StartupRegistration.cs#L28-L46) | M | Todo |
| C2 | Cap metadata read size before parsing (`fence.json`, XML) | [FenceManager.cs:207–216](窗格/Model/FenceManager.cs#L207-L216) | S | Todo |
| C3 | Global exception boundary + file logging; replace silent `Console.WriteLine` | [Program.cs:18–63](窗格/Program.cs#L18-L63), [FenceEntry.cs:68–71](窗格/Model/FenceEntry.cs#L68-L71) | M | Todo |
| C4 | Sign the executable or publish SHA-256 checksums with releases | [release.yml:63–78](.github/workflows/release.yml#L63-L78) | M | Todo |
| C5 | Keyboard and UI Automation access for fence items | [FenceWindow.cs](窗格/FenceWindow.cs), [WindowUtil.cs:148–154](窗格/Win32/WindowUtil.cs#L148-L154) | L | Todo |
| C6 | Test project (`FenceLayout`, `FenceViewModel` rollback, resx key parity) + PR workflow | [窗格.csproj](窗格/窗格.csproj), `.github/workflows/` | L | Todo |
| C7 | Harden `build.ps1` error handling (CIM query and process-exit race) | [build.ps1:31](build.ps1#L31), [99–103](build.ps1#L99-L103) | S | Todo |
| C8 | Launch hardening: `ProcessStartInfo` with explicit quoting for explorer | [FenceEntry.cs:63–67](窗格/Model/FenceEntry.cs#L63-L67), [FenceWindow.cs:1315](窗格/FenceWindow.cs#L1315) | S | Todo |

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

---

## 11. Changelog

| Date | Change |
|---|---|
| 2026-10-31 | Plan created. |
| 2026-10-31 | Phase A implemented (A1–A13). Build and smoke test still outstanding — see §9. |
