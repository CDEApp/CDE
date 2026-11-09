// @vitest-environment jsdom
import { act, renderHook } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { CatalogsResponse } from "../api/types";
import type { UiState, UiStateManager } from "../state/uiState";
import { useLegacyMigration } from "./useLegacyMigration";

function fakeUi(initial: UiState = {}) {
  let state: UiState = { ...initial };
  return {
    get: () => state,
    update: (patch: Partial<UiState>) => {
      state = { ...state, ...patch };
    },
  } as unknown as UiStateManager;
}

const load = (catalogDir: string, legacyCount: number) =>
  ({
    catalogs: [],
    catalogDir,
    legacyCatalogs: Array.from({ length: legacyCount }, (_, i) => ({ path: `${catalogDir}/old${i}.cde` })),
  }) as unknown as CatalogsResponse;

function setup(ui = fakeUi()) {
  return { ...renderHook(() => useLegacyMigration({ getUi: () => ui })), ui };
}

describe("useLegacyMigration", () => {
  it("opens after a load that found legacy catalogs, and not after one that found none", () => {
    const { result } = setup();
    act(() => result.current.promptFor(load("/cat", 0)));
    expect(result.current.open).toBe(false);
    act(() => result.current.promptFor(load("/cat", 2)));
    expect(result.current.open).toBe(true);
  });

  it("Not now closes it and stops it opening by itself for that folder only", () => {
    const { result, ui } = setup();
    act(() => result.current.promptFor(load("/cat", 1)));
    act(() => result.current.dismiss("/cat"));
    expect(result.current.open).toBe(false);
    expect(ui.get().legacyMigrateDismissed).toEqual(["/cat"]);

    act(() => result.current.promptFor(load("/cat", 1)));
    expect(result.current.open).toBe(false);
    act(() => result.current.promptFor(load("/other", 1)));
    expect(result.current.open).toBe(true);
  });

  it("show opens it on request, even for a dismissed folder", () => {
    const { result } = setup(fakeUi({ legacyMigrateDismissed: ["/cat"] }));
    act(() => result.current.show());
    expect(result.current.open).toBe(true);
  });

  it("dismiss with no folder only closes it", () => {
    const { result, ui } = setup();
    act(() => result.current.show());
    act(() => result.current.dismiss(null));
    expect(result.current.open).toBe(false);
    expect(ui.get().legacyMigrateDismissed).toBeUndefined();
  });
});
