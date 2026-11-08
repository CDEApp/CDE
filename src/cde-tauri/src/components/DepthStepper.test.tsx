// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { DepthStepper } from "./DepthStepper";

afterEach(cleanup);

function stepper(value: number) {
  const onChange = vi.fn();
  render(<DepthStepper value={value} min={1} max={5} onChange={onChange} />);
  return {
    onChange,
    minus: screen.getByRole("button", { name: "Decrease depth" }) as HTMLButtonElement,
    plus: screen.getByRole("button", { name: "Increase depth" }) as HTMLButtonElement,
    group: screen.getByRole("group", { name: "Depth" }),
  };
}

describe("DepthStepper", () => {
  it("shows the value and steps it down and up by one", () => {
    const s = stepper(3);
    expect(s.group.textContent).toContain("3");
    fireEvent.click(s.minus);
    fireEvent.click(s.plus);
    expect(s.onChange.mock.calls).toEqual([[2], [4]]);
  });

  it("disables the button that would leave the range", () => {
    expect(stepper(1).minus.disabled).toBe(true);
    cleanup();
    expect(stepper(5).plus.disabled).toBe(true);
  });

  it("wheel up increases and wheel down decreases", () => {
    const s = stepper(3);
    fireEvent.wheel(s.group, { deltaY: -100 });
    fireEvent.wheel(s.group, { deltaY: 100 });
    expect(s.onChange.mock.calls).toEqual([[4], [2]]);
  });

  it("ignores a wheel step past either end", () => {
    const high = stepper(5);
    fireEvent.wheel(high.group, { deltaY: -100 });
    expect(high.onChange).not.toHaveBeenCalled();
    cleanup();
    const low = stepper(1);
    fireEvent.wheel(low.group, { deltaY: 100 });
    expect(low.onChange).not.toHaveBeenCalled();
  });

  it("brings an out-of-range value back into range on the next step", () => {
    const s = stepper(9);
    fireEvent.click(s.minus);
    expect(s.onChange).toHaveBeenCalledWith(5);
  });
});
