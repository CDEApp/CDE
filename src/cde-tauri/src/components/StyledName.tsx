import type { CSSProperties, ReactNode } from "react";

/**
 * The entry name with its colour-rule "name" style (gradient text, #105). Without a name style it
 * renders the children as-is; otherwise in a shrink-wrapped `.name-text` span so the gradient spans
 * just the text. Pair with `splitRowAndNameStyle`.
 */
export function StyledName({ nameStyle, children }: { nameStyle: CSSProperties | undefined; children: ReactNode }) {
  if (!nameStyle) return <>{children}</>;
  return <span className="name-text" style={nameStyle}>{children}</span>;
}
