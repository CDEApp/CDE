import { useCallback, useState } from "react";
import { open } from "@tauri-apps/plugin-dialog";
import type { UiStateManager } from "../state/uiState";

export interface UseNewCatalogOptions {
  getUi: () => UiStateManager | null;
}

/** The root picked for a new catalog: set while the New Catalog dialog is open, null otherwise. */
export function useNewCatalog({ getUi }: UseNewCatalogOptions) {
  const [root, setRoot] = useState<string | null>(null);

  // Start (File menu / Catalogs tab) and "Change…" in the dialog both (re)pick the root, with the native
  // folder picker starting at the last root used for a new catalog. Cancelling keeps the current root.
  const pick = useCallback(async () => {
    const last = getUi()?.get().lastNewCatalogRoot;
    const picked = await open({
      directory: true,
      multiple: false,
      title: "Select the folder to catalog",
      defaultPath: typeof last === "string" && last ? last : undefined,
    });
    if (typeof picked !== "string") return;
    getUi()?.update({ lastNewCatalogRoot: picked });
    setRoot(picked);
  }, [getUi]);

  const close = useCallback(() => setRoot(null), []);

  return { root, pick, close };
}
