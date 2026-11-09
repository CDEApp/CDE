// @vitest-environment jsdom
import { act, renderHook } from "@testing-library/react";
import { beforeAll, describe, expect, it, vi } from "vitest";
import type { CdeApiClient } from "../api/client";
import { useColourRules } from "./useColourRules";

beforeAll(() => vi.stubGlobal("CSS", { supports: () => true }));

function setup(fileText = "\\.sys$ red\n[folder] blue") {
  const client = {
    getColourRules: vi.fn(async () => fileText),
    putColourRules: vi.fn(async (_t: string) => {}),
    getColourRuleDefaults: vi.fn(async () => "defaults"),
  };
  const hook = renderHook(() => useColourRules({ getClient: () => client as unknown as CdeApiClient }));
  return { ...hook, client };
}

describe("useColourRules", () => {
  it("starts empty with no styles", () => {
    const { result } = setup();
    expect(result.current.text).toBe("");
    expect(result.current.resolveStyle("a.sys", false)).toBeUndefined();
  });

  it("load reads the rules file, and the resolvers follow the text", async () => {
    const { result } = setup();
    await act(() => result.current.load());
    expect(result.current.text).toBe("\\.sys$ red\n[folder] blue");
    expect(result.current.resolveStyle("a.sys", false)).toEqual({ color: "red" });
    expect(result.current.resolveStyle("dir", true)).toEqual({ color: "blue" });
  });

  it("the tree resolver skips the [folder] rule", async () => {
    const { result } = setup();
    await act(() => result.current.load());
    expect(result.current.resolveTreeStyle("dir", true)).toBeUndefined();
    expect(result.current.resolveTreeStyle("a.sys", true)).toEqual({ color: "red" });
  });

  it("an edit restyles at once, before any save", () => {
    const { result } = setup();
    act(() => result.current.setText("^x green"));
    expect(result.current.resolveStyle("xy", false)).toEqual({ color: "green" });
  });

  it("load waits for a pending save, so it never reads text older than the save", async () => {
    const { result, client } = setup();
    let finishPut!: () => void;
    client.putColourRules.mockImplementationOnce(() => new Promise<void>((resolve) => (finishPut = resolve)));
    act(() => result.current.save("^x green"));
    let loading!: Promise<void>;
    act(() => {
      loading = result.current.load();
    });
    await act(async () => {
      await Promise.resolve();
      expect(client.getColourRules).not.toHaveBeenCalled();
      finishPut();
      await loading;
    });
    expect(client.getColourRules).toHaveBeenCalledOnce();
  });

  it("a failed save is reported until a later save succeeds", async () => {
    const { result, client } = setup();
    client.putColourRules.mockRejectedValueOnce(new Error("disk full"));
    await act(async () => {
      result.current.save("a red");
      await result.current.load();
    });
    expect(result.current.saveError).toBe("disk full");
    await act(async () => {
      result.current.save("a blue");
      await result.current.load();
    });
    expect(result.current.saveError).toBeNull();
  });

  it("a failed load keeps the current text", async () => {
    const { result, client } = setup();
    act(() => result.current.setText("^x green"));
    client.getColourRules.mockRejectedValueOnce(new Error("gone"));
    vi.spyOn(console, "error").mockImplementationOnce(() => {});
    await act(() => result.current.load());
    expect(result.current.text).toBe("^x green");
  });

  it("getDefaults asks the sidecar for the default rules", async () => {
    const { result } = setup();
    expect(await result.current.getDefaults()).toBe("defaults");
  });
});
