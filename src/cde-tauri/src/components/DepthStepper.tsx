interface Props {
  value: number;
  min: number;
  max: number;
  onChange: (value: number) => void;
}

/** The treemap Depth control: − / + buttons around the value, and the mouse wheel over it (up is deeper). */
export function DepthStepper({ value, min, max, onChange }: Props) {
  const step = (delta: number) => {
    const next = Math.min(max, Math.max(min, value + delta));
    if (next !== value) onChange(next);
  };

  return (
    <div
      className="depth-stepper"
      role="group"
      aria-label="Depth"
      title="Use the − / + buttons or the mouse wheel"
      onWheel={(e) => {
        if (e.deltaY !== 0) step(e.deltaY < 0 ? 1 : -1);
      }}
    >
      <span>Depth</span>
      <button aria-label="Decrease depth" disabled={value <= min} onClick={() => step(-1)}>
        −
      </button>
      <output className="depth-value">{value}</output>
      <button aria-label="Increase depth" disabled={value >= max} onClick={() => step(1)}>
        +
      </button>
    </div>
  );
}
