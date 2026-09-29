---
name: ALEX Orçamentos Desktop Enterprise System
colors:
  surface: '#f7f9fc'
  surface-dim: '#d8dadd'
  surface-bright: '#f7f9fc'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f2f4f7'
  surface-container: '#eceef1'
  surface-container-high: '#e6e8eb'
  surface-container-highest: '#e0e3e6'
  on-surface: '#191c1e'
  on-surface-variant: '#43474e'
  inverse-surface: '#2d3133'
  inverse-on-surface: '#eff1f4'
  outline: '#74777f'
  outline-variant: '#c4c6cf'
  surface-tint: '#485f82'
  primary: '#00152f'
  on-primary: '#ffffff'
  primary-container: '#0f2a4a'
  on-primary-container: '#7a92b7'
  inverse-primary: '#b0c8f0'
  secondary: '#006398'
  on-secondary: '#ffffff'
  secondary-container: '#49b3fe'
  on-secondary-container: '#004368'
  tertiary: '#06152b'
  on-tertiary: '#ffffff'
  tertiary-container: '#1c2a41'
  on-tertiary-container: '#8391ad'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#d4e3ff'
  primary-fixed-dim: '#b0c8f0'
  on-primary-fixed: '#001c3a'
  on-primary-fixed-variant: '#304869'
  secondary-fixed: '#cce5ff'
  secondary-fixed-dim: '#93ccff'
  on-secondary-fixed: '#001d31'
  on-secondary-fixed-variant: '#004b73'
  tertiary-fixed: '#d6e3ff'
  tertiary-fixed-dim: '#b9c7e4'
  on-tertiary-fixed: '#0d1c32'
  on-tertiary-fixed-variant: '#39475f'
  background: '#f7f9fc'
  on-background: '#191c1e'
  surface-variant: '#e0e3e6'
typography:
  headline-lg:
    fontFamily: Inter
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 26px
    letterSpacing: -0.01em
  headline-md:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '600'
    lineHeight: 22px
    letterSpacing: -0.005em
  headline-sm:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '600'
    lineHeight: 18px
  body-lg:
    fontFamily: Inter
    fontSize: 13px
    fontWeight: '500'
    lineHeight: 18px
  body-md:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 16px
  body-sm:
    fontFamily: Inter
    fontSize: 11px
    fontWeight: '400'
    lineHeight: 14px
  label-lg:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
  label-md:
    fontFamily: Inter
    fontSize: 11px
    fontWeight: '500'
    lineHeight: 14px
  label-sm:
    fontFamily: Inter
    fontSize: 10px
    fontWeight: '600'
    lineHeight: 12px
    letterSpacing: 0.03em
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  gutter: 0.5rem
  margin: 0.5rem
  space-xs: 0.125rem
  space-sm: 0.25rem
  space-md: 0.5rem
  space-lg: 0.75rem
  space-xl: 1rem
---

## Brand & Style

### Brand Personality & Core Intent
This design system is tailored for an enterprise Windows desktop environment (C# / .NET / WPF) focused on high-efficiency commercial quoting, technical budgeting, and IT services management. The visual language conveys engineering precision, rock-solid dependability, and uninterrupted operational flow for professionals spending 8+ hours a day inside the software.

### Aesthetic Movement: Corporate Desktop Ergonomics
Drawing directly from industrial software paradigms (Microsoft 365 Ribbon, Visual Studio Shell, SAP Business One), the style balances pixel-sharp structural density with modern digital cleanliness:
- **Zero Visual Noise:** Elimination of consumer-web gimmickry, heavy decorative drop shadows, and oversized padding.
- **Fitts's Law Optimization:** Instant-access Ribbon menus, rapid keyboard mnemonics (Alt-keys, F-keys, Ctrl shortcuts), and predictable tab-stops.
- **Information Density:** High information yield per screen square inch without cognitive overload, utilizing calibrated tabular data grids, contextual toolbars, and dockable panels.

## Colors

The palette directly honors the corporate identity while engineering an eye-comfort workspace:

### Primary & Action Channels
- **Deep Enterprise Navy (`#0A192F` / `#0F2A4A`):** Anchors the master Chrome, Title Bar, and Primary Ribbon headers. Signifies institutional reliability and structural stability.
- **Vibrant Precision Cyan (`#0090D9` / `#00A3FF`):** Dedicated focal color for primary action triggers (Commit Budget, Quick Approve), active tab underlines, focus rings, and selection indicators.

### Work Surfaces & Contrast Scales
- **Window Base Canvas (`#F3F5F8`):** Off-white, low-glare backdrop preventing eye fatigue under indoor office lighting.
- **Panel & Grid Surface (`#FFFFFF`):** High-readability substrate for editable fields, data rows, and inspector forms.
- **Structural Dividing Borders (`#D9DFE7` / `#B8C5D3`):** 1px crisp lines that delineate grids, splitters, status zones, and Ribbon groups without heavy visual mass.
- **Text & Data Contrast:**
  - Value / Primary Header Text: `#1E293B` (Slate 800)
  - Secondary Metadata & Labels: `#475569` (Slate 600)
  - Disabled / Watermark: `#94A3B8` (Slate 400)

### Enterprise Workflow Badges & Statuses
- **Rascunho (Draft):** Neutral Surface `#ECEFF3`, Border `#CBD5E1`, Text `#475569`.
- **Aguardando Aprovação (Pending):** Surface `#FEF3C7`, Border `#FCD34D`, Text `#92400E`.
- **Aprovado (Approved):** Surface `#E0F2FE`, Border `#7DD3FC`, Text `#0369A1`.
- **Em Execução (In Progress):** Surface `#E0E7FF`, Border `#A5B4FC`, Text `#3730A3`.
- **Finalizado (Completed):** Surface `#DCFCE7`, Border `#86EFAC`, Text `#166534`.
- **Cancelado (Canceled):** Surface `#FEE2E2`, Border `#FCA5A5`, Text `#991B1B`.

## Typography

Typography focuses on high-legibility tabular figures, sharp rendering under native DirectWrite / WPF ClearType rasterization, and compact vertical baselines:

- **Primary Font Family:** `Inter` (with native OS fallbacks: `Aptos`, `Segoe UI`, `Tahoma`).
- **Tabular Figures:** Numbers in pricing, taxes, SKUs, and margins must strictly enable OpenType tabular numbers (`tnum`) and slashed zeros (`zero`) to guarantee vertical decimal alignment in WPF DataGrids.
- **Visual Scale Restraint:** The typographic hierarchy operates between `10px` and `20px`. Giant web headings are avoided; structural hierarchy is provided by weight (`600` vs `400`), background surface shading, and spatial zoning rather than sheer size.

## Layout & Spacing

### WPF Window Architecture
1. **Title Bar (32px fixed):** Deep navy background (`#0A192F`), 16px brand icon, unified title typography with active document identifier, and classic Windows window control glyphs (Min/Max/Close with `#E81123` close hover state).
2. **Compact Ribbon Toolbar (96px fixed):**
   - **Tab Bar (28px):** Flat tabs with cyan underline (`#0090D9`) for the selected module (e.g., *Início*, *Orçamentos*, *Clientes*, *Peças & Serviços*, *Relatórios*, *Configurações*).
   - **Action Deck (68px):** Divided into logical vertical groups separated by 1px dividers (`#D9DFE7`). Group labels positioned in small 10px uppercase type at the bottom.
3. **Master-Detail / Grid Canvas (Variable):** Resizable splitters with 4px hit area, allowing fluid layout between left-side tree navigation/filters and the main data grid workspace.
4. **Status Bar (24px fixed):** Docked to the bottom edge (`#0F2A4A`), providing persistent system telemetries: Current Operator, Database Profile, Active Connection State (green/red dot), and Build Version.

### Density Philosophy
- Standard data row height: `28px` (High-density mode: `24px`; Roomy mode: `32px`).
- Form field height: `26px` to fit complex multi-input budget forms on standard 1080p and 1440p desktop displays without continuous scrolling.

## Elevation & Depth

Visual hierarchy is maintained through flat structural containment and subtle edge boundaries rather than diffused shadows:

- **Level 0 (App Canvas):** `#F3F5F8` base floor.
- **Level 1 (Panels & Grids):** `#FFFFFF` surfaces bounded by `#D9DFE7` solid 1px borders. No box-shadow; distinction is achieved through contrasting background planes.
- **Level 2 (Popovers, Combo Dropdowns, Context Menus):** `#FFFFFF` background with a crisp 1px border (`#B8C5D3`) and a crisp utility shadow (`0 4px 10px rgba(10, 25, 47, 0.12)`).
- **Level 3 (Modal Dialogs - e.g., Confirmar Emissão, Cadastro Rápido):** Full backdrop dimmer (`rgba(10, 25, 47, 0.45)`) with an elevated dialog container bordered by `#0090D9` and a prominent perimeter shadow (`0 8px 24px rgba(10, 25, 47, 0.20)`).

## Shapes

The design system adopts a crisp, disciplined radius convention appropriate for native desktop tools:
- **Interactive Controls (Buttons, Inputs, Selects):** `2px` to `3px` corner radius.
- **Badges & Tags:** `3px` corner radius for an architectural, non-pill look that resembles professional tags.
- **Window Chrome & Modals:** Standard Windows 11 `4px` top corners or sharp `0px` in full maximization.
- **Ribbon Group Containers:** `2px` subtle outer contours.

## Components

### 1. Classic Windows Title Bar
- Height: `32px`. Background: `#0A192F`. Text: `#FFFFFF` (12px Semi-Bold).
- Left: Embedded 16x16 vector glyph of the ALEX T.I. badge + title `ALEX Orçamentos - [Orçamento #2024-0891 - TechCorp Brasil]`.
- Right: Window control cluster (Minimize, Restore/Maximize, Close) with 46x32px hit zones; standard interactive hover cues.

### 2. Compact Office-Style Ribbon
- Tab Header: Tab buttons with `#475569` text, transitioning on selection to `#0090D9` with a 2px bottom accent bar and white background continuation.
- Action Buttons (Large): 48x60px vertically stacked icon (24px vector) with dual-line label (11px). Hover creates `#E2E8F0` tint with 1px border.
- Action Buttons (Small): 22px horizontal rows with 16px icon and inline label.
- Vertical Dividers: 1px width, color `#D9DFE7`, inset 6px top and bottom.

### 3. DataGrid Enterprise
- Header Row: Height `28px`, background `#ECEFF3`, text `#1E293B` (11px Bold, uppercase tracking). Includes column sorting arrows, right-click context menu, and column filter glyph triggers.
- Cells: Vertical borders `#EDF2F7`, horizontal row border `#E2E8F0`. Padding: `4px 8px`.
- Row Alternation: Even rows `#FFFFFF`, Odd rows `#F8FAFC`.
- Selection State: Background `#E0F2FE`, left border 3px solid `#0090D9`, text `#0F2A4A`.
- Numeric Cells (Preço Unitário, Desconto, Total): Right-aligned with strict monospace numeral fonts.

### 4. Status Badges
- Dimensions: Height `20px`, padding `0 6px`, font size `10.5px`, semi-bold weight.
- Borders: 1px solid tinted border matching status identity for visual clarity under quick scanning.

### 5. Input Fields & Form Controls
- Height: `26px`. Border: 1px solid `#CBD5E1`. Background: `#FFFFFF`. Text: 12px `#1E293B`.
- Focus State: Border color `#0090D9`, subtle glow border `1px solid #0090D9`.
- Error State: Border color `#DC2626`, background tint `#FEF2F2`.

### 6. Informative Bottom Status Bar
- Height: `24px`. Background: `#0F2A4A`. Text: `#D9DFE7` (11px).
- Status Segments: Separated by 1px vertical borders `#1E3A5F`. Left: Status message (e.g., "Pronto", "Calculando totais com ICMS/ST..."). Center: Document locking telemetry. Right: `Operador: alex.santos` | `Filial: Matriz` | `DB: SQL-PROD-01` | `v2.4.12`.