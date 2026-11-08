// @vitest-environment jsdom
import { act, renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ShellError, type CdeApiClient } from "../api/client";
import { useShellActions } from "./useShellActions";

const { show } = vi.hoisted(() => ({ show: vi.fn() }));
vi.mock("@mantine/notifications", () => ({ notifications: { show } }));

const ref = { catalogId: 2, entryIndex: 7 };

function setup() {
  const client = {
    shellCommands: vi.fn(async () => [{ id: 0, label: "Open in VS Code" }]),
    shell: vi.fn(async (_req: unknown) => {}),
  };
  const hook = renderHook(() => useShellActions({ getClient: () => client as unknown as CdeApiClient }));
  return { ...hook, client };
}

beforeEach(() => show.mockClear());

describe("useShellActions", () => {
  it("has no custom commands until they are loaded", async () => {
    const { result } = setup();
    expect(result.current.commands).toEqual([]);
    await act(() => result.current.loadCommands());
    expect(result.current.commands).toEqual([{ id: 0, label: "Open in VS Code" }]);
  });

  it("runShell sends the entry as a ref string with the action and command id", async () => {
    const { result, client } = setup();
    await act(() => result.current.runShell(ref, "custom", 0));
    expect(client.shell).toHaveBeenCalledWith({ ref: "2-7", action: "custom", commandId: 0 });
  });

  it("exploreEntry runs explore", async () => {
    const { result, client } = setup();
    await act(async () => result.current.exploreEntry(ref));
    expect(client.shell).toHaveBeenCalledWith({ ref: "2-7", action: "explore", commandId: undefined });
  });

  it.each([501, 422])("a %i refusal is shown with its reason", async (status) => {
    const { result, client } = setup();
    client.shell.mockRejectedValueOnce(new ShellError(status, "not supported here"));
    await act(() => result.current.runShell(ref, "properties"));
    expect(show).toHaveBeenCalledWith(expect.objectContaining({ color: "red", message: "not supported here" }));
  });

  it("an entry not on this filesystem (400) and other errors stay silent", async () => {
    const { result, client } = setup();
    client.shell.mockRejectedValueOnce(new ShellError(400, "missing")).mockRejectedValueOnce(new Error("network"));
    await act(() => result.current.runShell(ref, "open"));
    await act(() => result.current.runShell(ref, "open"));
    expect(show).not.toHaveBeenCalled();
  });
});
