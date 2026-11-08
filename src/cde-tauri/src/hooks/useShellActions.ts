import { useCallback, useState } from "react";
import { notifications } from "@mantine/notifications";
import { ShellError, type CdeApiClient } from "../api/client";
import { refToString, type EntryRef, type ShellAction } from "../api/types";
import type { ShellCommand } from "../menus";

export interface UseShellActionsOptions {
  getClient: () => CdeApiClient;
}

/** The configured custom commands, and running open / explore / properties / a custom command on an entry. */
export function useShellActions({ getClient }: UseShellActionsOptions) {
  const [commands, setCommands] = useState<ShellCommand[]>([]);

  const loadCommands = useCallback(async () => setCommands(await getClient().shellCommands()), [getClient]);

  const runShell = useCallback(
    async (ref: EntryRef, action: ShellAction, commandId?: number) => {
      try {
        await getClient().shell({ ref: refToString(ref), action, commandId });
      } catch (e) {
        // 501/422 carry the reason the OS action did not happen; anything else mirrors cdeWin, where
        // shell actions are silent no-ops when the entry isn't on this filesystem.
        if (e instanceof ShellError && e.status !== 400) {
          notifications.show({ color: "red", title: "Could not run the action", message: e.message });
        }
      }
    },
    [getClient],
  );

  const exploreEntry = useCallback((ref: EntryRef) => void runShell(ref, "explore"), [runShell]);

  return { commands, loadCommands, runShell, exploreEntry };
}
