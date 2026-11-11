// @vitest-environment jsdom
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SearchBar } from "./SearchBar";

afterEach(cleanup);

function bar(queryLimit?: number) {
  return (
    <SearchBar
      busy={false}
      onSearch={vi.fn()}
      onCancel={vi.fn()}
      history={[]}
      defaults={{ limitResultCount: 10000 }}
      filtersExpanded={true}
      onFiltersExpandedChange={vi.fn()}
      queryLimit={queryLimit}
    />
  );
}

const limitSelect = () => screen.getByLabelText("Limit") as HTMLSelectElement;

describe("SearchBar Limit follows the last search", () => {
  it("shows the raised limit after Raise limit re-runs the search", () => {
    const { rerender } = render(bar());
    expect(limitSelect().value).toBe("10000");
    rerender(bar(100000));
    expect(limitSelect().value).toBe("100000");
  });

  it("offers a raised limit past the largest preset", () => {
    const { rerender } = render(bar());
    rerender(bar(1000000));
    expect(limitSelect().value).toBe("1000000");
    expect(Array.from(limitSelect().options).map((o) => o.value)).toContain("1000000");
  });
});
