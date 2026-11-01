# DESIGN.md — 窗格 visual system

Distilled from the implementation: [`FluentTheme`](窗格/FluentTheme.cs), the fence painter
(`FenceWindow` / [`FenceRenderer`](窗格/FenceRenderer.cs)), the settings controls, and the menus.
This document describes intent, tokens and component specs. Where the code disagrees with it, the
gap is listed in §10 rather than silently tolerated.

---

## 1. Principles

1. **Content first.** The fence is a frame for the user's files; chrome recedes, items carry the
   visual weight.
2. **Translucency with restraint.** Fences float above the desktop using DWM blur plus a tint. The
   sheen and inner shadow are the only face gradients; the pointer halo is the only animated light.
3. **One accent.** The accent color marks selection and primary actions only. It is never used for
   decoration.
4. **Calm chrome.** No animations. Buttons use restrained hover (6%) and press (12%) tints of
   their own surface rather than bright color changes, so state is visible without flicker.
5. **Fluent as reference, not dependency.** No UI framework is added; effects are drawn by hand or
   delegated to DWM.
6. **Use translucency where it helps.** Fences and menus use the glass finish; settings use opaque
   canvas and cards so controls remain legible regardless of the desktop wallpaper.

---

## 2. Color

### 2.1 Surface tokens

| Token | Light | Dark | Used for |
|---|---|---|---|
| Canvas | `#EDF0F6` | `#141416` | Settings and dialog background |
| CardSurface | `#FFFFFF` | `#242428` | Section cards and elevated panels |
| CardBorder | `#E0E4EC` | `#34343A` | Card tone reference; outlines are drawn with `GlassEdge` |
| Surface | `#FFFFFF` | `#2C2C2C` | Buttons, inputs and menus |
| Text | `#201F1E` | `#F3F3F3` | Primary text, fence labels, icons on surface |
| SecondaryText | `#605E5C` | `#C8C8C8` | Hints, value labels, disabled text |
| Border | `#D2D0CE` | `#505050` | Stock control borders and menu separators |
| Accent | `#0078D4` or system/user color | same | Selection, primary button, focus ring |

Text drawn **on** the accent uses the luminance threshold 150: dark text above it, white below.

### 2.2 Semantic colors

| Token | Value | Notes |
|---|---|---|
| Missing item label | `#8A4B00` light / `#FFE0B0` dark | Amber reads on the dark overlay; light panels need the darker variant |
| Disabled button surface | `#F0F0F0` light / `#383838` dark | With SecondaryText |
| Dark input surface | — / `#242424` | Text boxes only in dark theme |

### 2.3 Translucent overlays (fence)

Fence colors are flat tints over the DWM blur, not opaque fills.

| Layer | Dark | Light | Meaning |
|---|---|---|---|
| Body overlay | black α 92 | white α 174 | Base panel; user-adjustable via overlay sliders |
| Title overlay | black α 36 | white α 92 | Lighter strip above the content |
| Scroll bar | Text α 150 | Text α 150 | Pill, width 4 logical px |
| Selection fill | Accent α 64 | Accent α 42 | Rounded card behind the selected item |
| Selection border | Accent α 210 | Accent α 210 | 1 px rounded outline |
| Hover fill | Text α 26 | Text α 18 | Rounded card, weaker than selection |
| Label shadow | black α 82 | black α 40 | Blurred text shadow for legibility over wallpapers |

Rule: the overlay sliders scale the body and title alpha proportionally; a user setting of 50%
halves both. Scroll bar, hover and selection alphas are constant.

---

## 3. Typography

| Role | Font | Size | Style |
|---|---|---|---|
| Body | Segoe UI | 9 pt | Regular |
| Emphasis | Segoe UI | 9 pt | Bold |
| Page heading | Segoe UI | 16 pt | Bold |
| Window title | Segoe UI | 11 pt | Regular |
| Fence title | Segoe UI | `titleHeight / 2` pt | Regular |
| Item label | Segoe UI | 9 pt × content scale | Regular |
| Dialog title | system | system | Native |

Rules: Segoe UI only; no italics, no underline. Labels are single-line with character ellipsis
(`EllipsisCharacter`), centered under the icon. The title uses ellipsis and is vertically centered
in its strip. The title font is proportional to the strip height so that resizing the title keeps
the text proportionate.

---

## 4. Space and shape

All fence metrics are **logical units**, converted with content scale first, then monitor DPI.
Settings chrome converts with DPI only.

### 4.1 Fence metrics

| Metric | Value |
|---|---|
| Item cell width | 88 |
| Icon | 32 |
| Label area height | 36 |
| Cell padding (gap between cells) | 12 |
| Icon-to-label gap | 4 |
| Selection/hover card inset | 4 |
| Card corner radius | 8 |
| Title text inset (left/right) | 12 |
| Scroll bar width / right offset | 4 / 7 |
| Scroll bar minimum length | 12 |
| Selection card corner radius | 8 |

### 4.2 Settings metrics

| Metric | Value |
|---|---|
| Form padding | 28 |
| Section card padding | 20 |
| Gap between cards | 14 |
| Window title bar | 48 |
| Row gap inside a card | 4 |
| Heading-to-content gap | 8–12 |
| Standard control height | 30–34 |
| Button minimum size | 96 × 34 |
| Color picker / wide button | 104 × 30, 112 × 34, 144 × 34 |
| Slider label column | 148 or 188 |
| Slider value column | 48 |

### 4.3 Radii

| Element | Radius | Source |
|---|---|---|
| Item card | 8 | `FenceRenderer` |
| Context menu | 8 | `FluentMenuRenderer` + `WindowUtil.ApplyRoundedCorners` |
| Fence window | system (8 on Windows 11) | DWM corner preference |
| Settings card | 14 | `SettingsSectionControl` |
| Button | 9 | `GlassButton` |
| Input | 0 | Stock WinForms field; square by platform |

---

## 5. Effects and elevation

- **Fence elevation:** DWM drop shadow, extended frame of 1 px. The window has no native caption;
  the shadow and blur are the only system chrome.
- **Backdrop blur:** `ACCENT_ENABLE_BLURBEHIND` via `BlurUtil`. It is only visible because the
  fence paints translucent pixels directly to the window surface — see §9.
- **Icon glow:** a blurred, icon-colored halo behind each icon. Radius 2–18, intensity 0–100
  (defaults 9 / 80). Recomputed only when the icon, size or radius changes.
- **Pointer halo:** radial gradient, base alpha 42 dark / 30 light scaled by intensity, focus 0.32,
  radius 32–128 (default 76), clipped to the content area, throttled to 16 ms.
- **Label shadow:** blur radius 3, offset 1, black at 82 dark / 40 light.

### 5.1 Solid Glass material

The material starts from [deeplethe/solid-glass](https://github.com/deeplethe/solid-glass) and adds
the three light layers across the face, above the blur:

1. **Finish at the edge and a light wash on the face.** Rim, inner hairline and outline, plus a
   sheen and an inner shadow across the surface.
2. **Heavy, not airy.** Neutral tints; the blur softens the backdrop without tinting it.
3. **Depth is thickness.** Panel < card < popover; the thicker the pane, the more opaque it is.
4. **One state.** Hovered, chosen and held-open share the same small pane of clear glass; the state
   does not change colour.

| Layer | Dark | Light | Where |
|---|---|---|---|
| Sheen | white α 18 → 0 | white α 28 → 0 | Fades across the full face |
| Inner shadow | 0 → black α 22 | 0 → black α 12 | Fades across the full face |
| Rim | white α 41 | white α 230 | 1 px line, 1 px inside the top edge, inset by the radius |
| Inner hairline | white α 10 | white α 89 | 1 px inside the outline |
| Outline | white α 15 | black α 26 | 1 px on the outline |
| Grain | white noise, up to α 8 | black noise, up to α 8 | 64 px tile, over the face |

**Order:** background tint/blur → sheen → inner shadow → rim and hairlines → grain → content.
Fences, which are rounded by DWM rather than by a path, omit the edge highlight: a square 1 px
line would cut the corners and read as a seam. They get the two face washes and the grain.

**Thickness:** cards use the card tone with the full finish; menus use the near-opaque
`PopoverSurface`; fences use their blurred body.

**State:** a menu row under the pointer or the keyboard, and a fence item under the pointer, are
filled with `GlassState`, closed by the outline and lit by the rim — the same pane everywhere.
Selection keeps the accent tint: it is meaningful state, not hover chrome.

**Intensity:** two Advanced settings sliders. *Glass lighting* (0–200%, default 100) scales the
sheen, inner shadow, rim and hairlines; *Glass texture* (0–100%, default 30) scales the grain.

**Scope:** fences, section cards, menus, and the settings window background. The window tint
reuses the body overlay opacity (92 dark / 174 light); there is no separate window tint token.

- **No animations, no per-item transitions.**

---

## 6. Components

### 6.1 Fence window

- Borderless, translucent, glued to the desktop, hidden from Alt+Tab.
- States: normal, locked (no drag/drop, no resize), minified (title strip only), titles hidden
  (Alt+drag to move).
- Title strip: optional, tinted lighter than the body, name at 12 px inset; no divider below it.
- Glass: the sheen, inner shadow and grain are painted over the body and title fills and under the
  title text. The edge highlight is left to path-rounded panes, because the fence's corners are
  rounded by DWM.
- Content area: grid of item cells, scrollable with a thin pill scroll bar when rows overflow.

### 6.2 Item card states

| State | Fill | Border | Notes |
|---|---|---|---|
| Default | none | none | Icon + label only |
| Hover | `GlassState` (white 7% dark / black 9% light) | none | Only when not selected |
| Selected | Accent α 64/42 | Accent α 210, 1 px | Click selection; drag keeps it |
| Missing | none | none | Warning icon replaces thumbnail; label uses the missing color |
| Missing + selected | accent fill | accent border | Same as selected |

### 6.3 Settings

- **Borderless window**: DWM-rounded corners, the system drop shadow, a DWM hairline border, and an
  opaque theme canvas. A 48 px custom title bar carries the product name and section title and
  closes through the glyph or Esc; the bar drags the window. No dividing rule separates it from
  the content.
- A single column of **cards** (CardSurface, 14 px radius, 20 px padding, 14 px gap) sits on the
  canvas. Nested layouts use the same solid card color; a subtle border and restrained shadow
  separate each card without competing with its controls.
- Fixed page header outside the scrolling area: heading + one secondary line, no separating rule.
- Controls: labels left, control right, values in a 48 px column.
- Buttons use `GlassButton`: a solid themed surface with restrained hover/press states; the
  primary action uses the accent color.
- Footer: Close (secondary) and Apply (primary, accent) right-aligned, with the executable build
  timestamp on the left; no rule above it.

### 6.4 Dialogs

- Standard titled window, 24 px padding, 96 × 34 buttons.
- Title bar follows the app light/dark mode via the immersive dark-mode attribute.

### 6.5 Menus

- Fence and tray menus use `FluentMenuRenderer`, the thickest pane: near-opaque `PopoverSurface`
  with the Solid Glass finish (rim, inner hairline, outline), 8 px rounded corners, body font,
  10/5 item padding, Text-colored labels and SecondaryText when disabled.
- A row under the pointer or the keyboard is the one neutral state: `GlassState` fill, outline and
  rim — no accent tint. Checks are drawn in the text color.
- The shell item menu is the OS menu (unstyled by design).

### 6.6 Widget set

The settings panel favors opaque, familiar WinForms controls for legibility:

Cards use `SettingsSectionControl` with an opaque CardSurface fill, subtle border and restrained
shadow. Inputs, sliders and checkboxes use stock WinForms controls with the app palette. Dialog
actions use `GlassButton`; menus retain the glass renderer.

---

## 7. Theming

- Follow Windows by default (app mode + accent), or a manual light/dark mode with a custom accent.
- Overlay opacity is adjustable per theme; glow effects can be disabled entirely.
- Glass lighting and texture tune the translucent fence/menu finish, not settings cards.
- Changes apply on **Apply**; the title bar of the settings window updates live when the theme
  changes underneath it.
- Fences repaint content on theme change: fonts, brushes and effect caches are rebuilt.

---

## 8. Scaling and platform

- Per-monitor V2 DPI; every fence metric goes through `ContentToDeviceUnits` (content scale × DPI).
- Content scale 50–200% affects thumbnails, labels, spacing and card radii, but **not** saved fence
  dimensions until the user runs "Modify all".
- Windows 11 rounds fence corners via `DWMWA_WINDOW_CORNER_PREFERENCE`; Windows 10 keeps square
  corners (no clipping region, so the shadow survives).
- Long paths are supported; the shell menu is the exception (OS limit).

---

## 9. Non-negotiables (learned the hard way)

1. **Fences paint directly to the window surface.** Never route content through an opaque cached
   bitmap: that removes the per-pixel alpha the DWM blur and theme tint depend on.
2. **No native caption.** Every `WM_NCCALCSIZE` pass must be suppressed; letting one through makes
   Windows paint a caption with the window text over the fence.
3. **The window text is the product name, not the fence name.** Windows can render window text in
   ghosts and switchers even with the title hidden.
4. **One accent source.** New UI must read `FluentTheme.Accent`, never hardcode a blue.
5. **No new fonts or font sizes outside this table** without updating this document.

---

## 10. Known deviations

These were the places where the implementation did not match the system. All five were resolved in
code; the table is kept so the decisions stay traceable.

| ID | Deviation | Resolution | Status |
|---|---|---|---|
| D1 | Settings cards were square and borderless | 8 px radius + 1 px Border on section cards | Resolved |
| D2 | Buttons had no state feedback | 6% hover / 12% press surface tints; buttons stay square | Resolved |
| D3 | Missing label used one color in both themes | `#8A4B00` light, `#FFE0B0` dark | Resolved |
| D4 | Spacing mixed 4/5/7/8 | Icon gap 4, card radius 8 | Resolved |
| D5 | Native menus ignored the custom accent | `FluentMenuRenderer` paints menus with the app palette | Resolved |

---

## 11. Changing the system

1. Update the token or component spec here first.
2. Implement through `FluentTheme` (colors/fonts) or the component owner (`FenceRenderer` for items,
   `SettingsSectionControl` for cards, `WindowUtil` for chrome).
3. Verify: light and dark, 100% and 200% content scale, per-monitor DPI move, titles shown/hidden,
   selection, hover, drag, and one stale-content sweep.

---

## 12. Revision history

| Date | Change |
|---|---|
| 2026-11-01 | Initial distillation from the implementation. |
| 2026-11-01 | D1–D5 resolved: rounded bordered section cards, button state tints, theme-aware missing color, 4/8 metric normalization, and the Fluent menu renderer. |
| 2026-11-01 | Glass added as a core principle; the three-layer lighting (sheen, inner shadow, edge highlight) applied to fences, section cards and menus. |
| 2026-11-01 | Settings rewritten: elevation tokens (CardSurface/CardBorder), tinted canvas, 12 px cards with 20 px padding, fixed ruled header and footer rule. |
| 2026-11-01 | Settings window redesigned: borderless DWM-rounded shell, 48 px custom title bar, 14 px shadowed cards, stronger canvas/card contrast, build marker, and a Glass lighting intensity setting (0–200%, default 100). |
| 2026-11-01 | Settings window background turned to glass: DWM blur behind a translucent tint, with containers that clear to transparent before repainting. |
| 2026-11-01 | Adopted the Solid Glass material (deeplethe/solid-glass): edge-only finish, thickness levels, neutral menu state; removed the face gradients. |
| 2026-11-01 | Rolled back the translucent settings background: the window is opaque Canvas again. The Solid Glass finish, cards and menus stay. |
| 2026-11-01 | Adopted the Solid Glass button widget (`GlassButton`) across settings and dialogs; stock controls otherwise, mapped in §6.6. |
| 2026-11-01 | Three-layer glass lighting (sheen, inner shadow, highlight) restored across all surfaces, with tiled grain; settings background blurred again; two sliders (Glass lighting, Glass texture). |
| 2026-11-01 | Removed the title-bar, header and footer rules; card faces repaint the glass across nested layouts; DWM hairline window border; doc corrections (CardBorder usage, window title role, window tint reuse). |
| 2026-11-01 | Dropped the square edge highlight from the fence: it drew a white seam at the DWM-rounded corners. Fences keep the sheen, inner shadow and grain. |
| 2026-11-02 | Simplified settings to an opaque canvas and solid cards; removed backdrop blur, card grain and heavy shadows for legible controls over any wallpaper. |
| 2026-11-02 | Extended fence lighting gradients across the full surface to avoid a visible seam at the sheen band edge. |
