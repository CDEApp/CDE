// @vitest-environment jsdom
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SearchBar } from "./SearchBar";

afterEach(cleanup);

function bar(prefill?: { pattern: string; nonce: number }) {
  return (
    <SearchBar
      busy={false}
      onSearch={vi.fn()}
      onCancel={vi.fn()}
      history={[]}
      defaults={{}}
      filtersExpanded={false}
      onFiltersExpandedChange={vi.fn()}
      prefill={prefill}
    />
  );
}

describe("SearchBar prefill", () => {
  it("fills the pattern, turns Everything on and focuses the box with the caret at the end", () => {
    const { rerender } = render(bar());
    const box = screen.getByPlaceholderText("Search pattern…") as HTMLInputElement;
    expect(box.value).toBe("");
    expect((screen.getByLabelText("Everything") as HTMLInputElement).checked).toBe(false);

    rerender(bar({ pattern: "catalog:x path:/a/ ", nonce: 1 }));

    expect(box.value).toBe("catalog:x path:/a/ ");
    expect((screen.getByLabelText("Everything") as HTMLInputElement).checked).toBe(true);
    expect(document.activeElement).toBe(box);
    expect(box.selectionStart).toBe(box.value.length);
  });

  it("applies again for a second request with the same text", () => {
    const { rerender } = render(bar({ pattern: "path:/a/ ", nonce: 1 }));
    const box = screen.getByPlaceholderText("Search pattern…") as HTMLInputElement;
    box.blur();
    rerender(bar({ pattern: "path:/a/ ", nonce: 2 }));
    expect(document.activeElement).toBe(box);
  });
});
