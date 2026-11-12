// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SearchBar, type SearchPrefill } from "./SearchBar";

afterEach(cleanup);

function bar(prefill: SearchPrefill | null, onSearch = vi.fn()) {
  return (
    <SearchBar
      busy={false}
      onSearch={onSearch}
      onCancel={vi.fn()}
      history={[]}
      defaults={{}}
      filtersExpanded={true}
      onFiltersExpandedChange={vi.fn()}
      prefill={prefill}
    />
  );
}

const dateFrom = () => within(screen.getByRole("group", { name: /Date modified/ })).getByRole("checkbox", { name: "From" }) as HTMLInputElement;
const sizeFrom = () => within(screen.getByRole("group", { name: /Size/ })).getByRole("checkbox", { name: "From" }) as HTMLInputElement;

describe("SearchBar prefill that clears the filters", () => {
  it("unchecks the size and date filters, so the next Search runs what the shortcut ran", () => {
    const onSearch = vi.fn();
    const { rerender } = render(bar(null, onSearch));
    fireEvent.click(dateFrom());
    fireEvent.click(sizeFrom());
    expect(dateFrom().checked).toBe(true);

    rerender(bar({ pattern: "hash:00", nonce: 1, clearFilters: true }, onSearch));

    expect(dateFrom().checked).toBe(false);
    expect(sizeFrom().checked).toBe(false);
    fireEvent.click(screen.getByRole("button", { name: "Search" }));
    expect(onSearch).toHaveBeenLastCalledWith(
      expect.objectContaining({ pattern: "hash:00", fromDateEnable: false, fromSizeEnable: false, notOlderThanEnable: false }),
    );
  });

  it("leaves the filters alone for a plain prefill", () => {
    const { rerender } = render(bar(null));
    fireEvent.click(dateFrom());
    rerender(bar({ pattern: "path:/a/ ", nonce: 1 }));
    expect(dateFrom().checked).toBe(true);
  });
});
