# Responsive Redesign Design

## Context

The frontend currently works best on desktop widths and contains several PC-first components that degrade on mobile H5. The most visible gaps are global width constraints, dense toolbar layouts, table-heavy management views, and file-management interfaces that assume desktop interaction patterns.

The goal is to make the existing product suitable for both PC and mobile presentation without changing backend APIs or core business logic.

## Goal

Refactor the frontend so all major pages support both desktop and mobile H5, while also improving the visual quality of the product enough for stakeholder demos.

## Scope

- Keep existing routes, stores, APIs, and business behaviors.
- Remove desktop-only global layout constraints.
- Upgrade page and component responsiveness across:
  - `src/views/Layout.vue`
  - `src/views/Chat.vue`
  - `src/views/Knowledge.vue`
  - `src/views/KnowledgeDetail.vue`
  - `src/views/Login.vue`
  - `src/views/Profile.vue`
  - `src/views/InviteAccept.vue`
  - `src/components/KnowledgeCard.vue`
  - `src/components/DocumentList.vue`
  - `src/components/FolderTree.vue`
- Add a more polished and consistent visual system without introducing a new UI framework.

## Non-Goals

- No backend API changes.
- No route restructuring.
- No store/schema redesign.
- No separate mobile app.

## Product Direction

This is not a minimal media-query patch. The redesign should keep the current desktop workbench structure where it already works, but convert PC-only components into adaptive components that remain usable and visually coherent on mobile H5.

The visual direction is:

- light professional
- AI/workbench feel
- cleaner hierarchy
- softer card system
- more polished spacing and controls

## Breakpoints

- Desktop: `>= 1024px`
- Tablet: `769px - 1023px`
- Mobile H5: `<= 768px`

## UX Strategy

### Global Layout

- Remove the fixed centered app shell from `src/style.css`.
- Make the app full-width and viewport-aware.
- Standardize responsive page gutters, card spacing, modal widths, and sticky bottom action spacing.
- Preserve the desktop sidebar layout and mobile drawer behavior in `Layout.vue`, but improve touch sizing and content padding.

### Chat

- Keep desktop message alignment and action density.
- Reflow the mobile input area into stacked sections.
- Let model selection, upload, and knowledge-base selection wrap naturally on small screens.
- Reduce bubble widths, avatar sizes, and spacing for mobile.
- Improve the streaming state and action affordances so they remain easy to tap.

### Knowledge List

- Keep card-based listing on desktop.
- Use a single-column card flow on mobile.
- Convert the active knowledge-base detail area from desktop-friendly two-column descriptions to mobile-friendly single-column presentation.
- Rework member-management controls so mobile does not rely on dense table layouts.

### Knowledge Detail

This is the highest-priority page and the most PC-specific area.

- Reflow the header, breadcrumb, and toolbar for small screens.
- Preserve desktop file-list density.
- Replace or adapt the mobile file list into a card-first layout instead of forcing desktop columns into narrow widths.
- Convert batch actions into a mobile-friendly sticky bottom action bar.
- Make members and invitations readable and operable on mobile using card layouts or safe horizontal scroll wrappers where appropriate.
- Resize dialogs and form layouts to fit small screens.

### Shared Components

- `KnowledgeCard.vue`: improve spacing, wrapping, metadata behavior, and mobile height balance.
- `DocumentList.vue`: make toolbar and list actions wrap cleanly and remain tappable.
- `FolderTree.vue`: remove hover-only assumptions for mobile action access.

### Supporting Pages

- `Login.vue`, `Profile.vue`, and `InviteAccept.vue` should retain their purpose but align visually with the upgraded system and maintain small-screen usability.

## Visual System Changes

- Replace flat page backgrounds with a cleaner layered light background.
- Standardize card radius, borders, shadows, and hover transitions.
- Unify button sizing and touch targets, especially on mobile.
- Make form controls more consistent in height, spacing, and grouping.
- Refine tags, badges, and status colors so they feel intentional rather than default.
- Improve page polish without introducing an aggressive or risky stylistic departure.

## Technical Approach

- Prefer adapting existing components over creating parallel route-level mobile pages.
- Use shared responsive SCSS utilities in `src/styles/index.scss`.
- Add mobile-specific DOM branches only for areas where desktop structures fundamentally do not fit mobile, especially file lists and management tables.
- Keep desktop behavior stable while allowing mobile layout divergence.

## Risks

- `KnowledgeDetail.vue` is large and likely to be the main source of regression risk.
- Dialog-heavy and table-heavy sections may need selective mobile-specific rendering.
- Existing worktree changes mean commits must be tightly scoped to files touched for this effort.

## Verification

- Run the frontend build successfully.
- Validate major routes on desktop width and mobile width.
- Confirm no major horizontal overflow at mobile widths.
- Confirm dialogs fit within small screens.
- Confirm chat input, knowledge management, and file-management workflows remain operable on both desktop and mobile.
