import type { ReactNode } from "react";
import { createTheme, MantineProvider, type CSSVariablesResolver } from "@mantine/core";
import { Notifications } from "@mantine/notifications";
// Only the component styles this app uses (modal + toasts), not the all-in-one stylesheet: that one
// also carries a global "baseline" that would restyle the rest of the (non-Mantine) UI.
// default-css-variables.css is required with per-component imports: MantineProvider only injects the
// variables that differ from the defaults, so without it --mantine-spacing-*, font sizes and sizes are
// undefined (no padding or gap, unsized buttons, a close icon that fills its box).
import "@mantine/core/styles/default-css-variables.css";
import "@mantine/core/styles/global.css";
import "@mantine/core/styles/ModalBase.css";
import "@mantine/core/styles/Modal.css";
import "@mantine/core/styles/Overlay.css";
import "@mantine/core/styles/Paper.css";
import "@mantine/core/styles/CloseButton.css";
import "@mantine/core/styles/UnstyledButton.css";
import "@mantine/core/styles/Button.css";
import "@mantine/core/styles/Group.css";
import "@mantine/core/styles/Text.css";
import "@mantine/core/styles/Loader.css";
import "@mantine/core/styles/Notification.css";
import "@mantine/notifications/styles.css";

/**
 * Mantine is used ONLY for the rescan "can't be reached" modal and the success/failure toasts.
 * It is themed from the app's own CSS variables (styles.css) so it follows whatever palette the app
 * defines, and uses the app's font.
 */
const theme = createTheme({
  fontFamily: '"Segoe UI", system-ui, sans-serif',
  fontFamilyMonospace: "Consolas, monospace",
  primaryColor: "cde",
  // Shades built around the app accent (#0e639c); index 6/7 are the filled-button shades.
  colors: {
    cde: ["#e6f3fb", "#c4e2f5", "#9bcdee", "#6fb6e5", "#3f9ad4", "#1c7fbc", "#0e639c", "#0b5282", "#094168", "#06304d"],
  },
  defaultRadius: 2,
});

// Map Mantine's surface/text/border variables onto the app's, for whichever scheme is active.
const appVariables: Record<string, string> = {
  "--mantine-color-body": "var(--panel)",
  "--mantine-color-text": "var(--fg)",
  "--mantine-color-dimmed": "color-mix(in srgb, var(--fg) 65%, transparent)",
  "--mantine-color-default-border": "var(--border)",
  "--mantine-color-default": "var(--content-header)",
  "--mantine-color-default-hover": "var(--row-hover)",
  "--mantine-color-default-color": "var(--fg)",
};
const resolveVariables: CSSVariablesResolver = () => ({
  variables: {},
  light: appVariables,
  dark: appVariables,
});

/** The app currently ships a single (dark) palette; Mantine is pinned to match it. */
const COLOR_SCHEME = "dark" as const;

export function AppMantineProvider({ children }: { children: ReactNode }) {
  return (
    <MantineProvider theme={theme} forceColorScheme={COLOR_SCHEME} cssVariablesResolver={resolveVariables}>
      <Notifications position="bottom-right" limit={5} />
      {children}
    </MantineProvider>
  );
}
