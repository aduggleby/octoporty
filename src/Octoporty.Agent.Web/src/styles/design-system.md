# Octoporty Design System

## Aesthetic Direction: Industrial Control Panel

The Octoporty Agent UI draws inspiration from industrial control rooms, network operations centers, and flight deck instrumentation. This isn't a typical SaaS dashboard - it's a **mission-critical tunneling control interface**.

### Key Visual Principles

1. **Industrial Heritage**: Hardware LED indicators, terminal-style typography, rack-mounted equipment aesthetics
2. **High Contrast Dark Mode**: Deep blacks with carefully chosen accent colors
3. **Data-Dense but Clear**: Monospace fonts for technical data, clear status indicators
4. **Purposeful Animation**: Connection pulses, data flow indicators that communicate system state

---

## Color Palette

### Base Surfaces (Dark Theme)
```css
--color-surface-0: #050709;   /* Deepest background */
--color-surface-1: #0a0f14;   /* Card backgrounds */
--color-surface-2: #101820;   /* Elevated surfaces */
--color-surface-3: #18232e;   /* Interactive elements */
--color-surface-4: #1e2d3d;   /* Hover states */
```

### Accent Colors

| Color   | Use Case                          | Base      | Glow               |
|---------|-----------------------------------|-----------|-------------------|
| Cyan    | Primary actions, data flow, links | #22d3ee   | rgba(34,211,238,0.15) |
| Emerald | Success, connected, active        | #10b981   | rgba(16,185,129,0.15) |
| Amber   | Warnings, attention, connecting   | #f59e0b   | rgba(245,158,11,0.15) |
| Rose    | Errors, danger, disconnected      | #f43f5e   | rgba(244,63,94,0.15)  |

---

## Typography

### Font Stack
```css
--font-display: 'Outfit', system-ui, sans-serif;
--font-mono: 'JetBrains Mono', ui-monospace, monospace;
```

### Usage Guidelines

| Element           | Font         | Size    | Weight | Tracking |
|-------------------|--------------|---------|--------|----------|
| Page titles       | Outfit       | 28px    | 600    | -0.02em  |
| Panel titles      | JetBrains    | 11px    | 600    | 0.1em    |
| Body text         | Outfit       | 14px    | 400    | normal   |
| Labels            | JetBrains    | 10px    | 600    | 0.1em    |
| Data values       | JetBrains    | 14px    | 500    | normal   |
| Large data values | JetBrains    | 28px    | 700    | -0.02em  |
| Badges            | JetBrains    | 10px    | 600    | 0.05em   |
| Buttons           | JetBrains    | 12px    | 600    | 0.02em   |

---

## Components

### LED Status Indicator
Hardware-inspired status indicator with glow effect.

```tsx
// Connected (green, pulsing)
<div className="led led-connected" />

// Disconnected (red, blinking)
<div className="led led-disconnected" />

// Connecting (amber, fast blink)
<div className="led led-connecting" />
```

### Badges
Compact status labels with colored backgrounds.

```tsx
<span className="badge badge-success">ACTIVE</span>
<span className="badge badge-warning">DISABLED</span>
<span className="badge badge-error">OFFLINE</span>
<span className="badge badge-info">HTTPS</span>
```

### Buttons

```tsx
// Primary - cyan gradient, for main actions
<button className="btn btn-primary">Create Mapping</button>

// Secondary - subtle, for secondary actions
<button className="btn btn-secondary">Cancel</button>

// Ghost - transparent, for tertiary actions
<button className="btn btn-ghost">Edit</button>

// Danger - red, for destructive actions
<button className="btn btn-danger">Delete</button>

// Sizes
<button className="btn btn-primary btn-sm">Small</button>
<button className="btn btn-primary btn-lg">Large</button>
```

### Inputs

```tsx
// Standard input
<input type="text" className="input" placeholder="Enter value" />

// With error state
<input type="text" className="input input-error" />

// Select dropdown
<select className="select">
  <option>Option 1</option>
</select>
```

### Panels
Primary container component with header and body.

```tsx
<div className="panel">
  <div className="panel-header">
    <svg className="w-4 h-4 text-cyan-base">...</svg>
    <span className="panel-title">Panel Title</span>
  </div>
  <div className="panel-body">
    Content here
  </div>
</div>
```

### Toggle Switch

```tsx
<button
  className="toggle"
  data-checked={isEnabled}
  onClick={() => setIsEnabled(!isEnabled)}
/>
```

### Mapping Card
Displays port mapping information with status and actions.

```tsx
<MappingCard
  mapping={mapping}
  onEdit={handleEdit}
  onDelete={handleDelete}
  onToggle={handleToggle}
/>
```

### Connection Status
Shows real-time tunnel connection status.

```tsx
// Full panel version
<ConnectionStatus
  status="Connected"
  gatewayUrl="wss://gateway.example.com/tunnel"
  onReconnect={handleReconnect}
/>

// Compact badge version
<ConnectionStatusBadge status="Connected" />
```

### Modal

```tsx
<Modal
  isOpen={isOpen}
  onClose={handleClose}
  title="Modal Title"
  size="md" // sm | md | lg
>
  Modal content
</Modal>

// Confirmation dialog variant
<ConfirmDialog
  isOpen={showConfirm}
  onClose={() => setShowConfirm(false)}
  onConfirm={handleConfirm}
  title="Delete Item"
  message="Are you sure you want to delete this item?"
  variant="danger" // danger | warning | info
  confirmLabel="Delete"
  cancelLabel="Cancel"
/>
```

### Toast Notifications

```tsx
const { addToast } = useToast()

// Success
addToast('success', 'Saved', 'Your changes have been saved')

// Error
addToast('error', 'Error', 'Something went wrong')

// Warning
addToast('warning', 'Warning', 'Please check your input')

// Info
addToast('info', 'Info', 'Connection restored')
```

---

## Layout Structure

```
┌─────────────────────────────────────────────────────────────┐
│ Sidebar (240px)          │ Main Content                     │
│                          │                                   │
│ ┌─────────────────────┐  │ ┌─────────────────────────────┐  │
│ │ Logo + Version      │  │ │ Page Header                 │  │
│ └─────────────────────┘  │ └─────────────────────────────┘  │
│                          │                                   │
│ ┌─────────────────────┐  │ ┌─────────────────────────────┐  │
│ │ Connection Status   │  │ │ Content Panels              │  │
│ └─────────────────────┘  │ │                             │  │
│                          │ │                             │  │
│ ┌─────────────────────┐  │ │                             │  │
│ │ Navigation Links    │  │ │                             │  │
│ │ - Dashboard         │  │ │                             │  │
│ │ - Mappings          │  │ │                             │  │
│ └─────────────────────┘  │ └─────────────────────────────┘  │
│                          │                                   │
│ ┌─────────────────────┐  │                                   │
│ │ Logout              │  │                                   │
│ └─────────────────────┘  │                                   │
└─────────────────────────────────────────────────────────────┘
```

---

## Animation Guidelines

### Timing Functions
```css
--ease-out-expo: cubic-bezier(0.16, 1, 0.3, 1);
--ease-out-back: cubic-bezier(0.34, 1.56, 0.64, 1);
--ease-in-out-circ: cubic-bezier(0.85, 0, 0.15, 1);
```

### Key Animations

1. **Page Transitions**: Fade + slight Y translation (150-200ms)
2. **Card Hover**: Scale 1.02 + shadow elevation (150ms)
3. **LED Pulse**: Connected state pulses every 2s
4. **Data Flow**: Animated dot traveling across connection arrows
5. **Loading Spinners**: Continuous rotation for async states
6. **Toast Entrance**: Spring animation from bottom-right

---

## Responsive Breakpoints

```css
/* Mobile */
@media (max-width: 768px) {
  .sidebar { transform: translateX(-100%); }
  .main-content { margin-left: 0; padding: 20px; }
}

/* Tablet */
@media (min-width: 768px) and (max-width: 1024px) {
  /* Grid adjusts to 2 columns */
}

/* Desktop */
@media (min-width: 1024px) {
  /* Full layout with sidebar visible */
}
```

---

## File Structure

```
src/
├── api/
│   └── client.ts          # API client with auth handling
├── components/
│   ├── ConnectionStatus.tsx
│   ├── Layout.tsx
│   ├── MappingCard.tsx
│   ├── MappingForm.tsx
│   ├── Modal.tsx
│   └── index.ts
├── hooks/
│   ├── useSignalR.ts      # Real-time connection
│   ├── useToast.tsx       # Toast notifications
│   └── index.ts
├── pages/
│   ├── Dashboard.tsx
│   ├── Login.tsx
│   ├── MappingDetail.tsx
│   └── Mappings.tsx
├── styles/
│   └── design-system.md   # This file
├── App.tsx
├── index.css              # Tailwind + custom styles
├── main.tsx
└── types.ts               # TypeScript definitions
```
