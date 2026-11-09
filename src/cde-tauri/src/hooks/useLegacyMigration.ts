import { useCallback, useState } from "react";
import type { CatalogsResponse } from "../api/types";
import { dismissFolder, shouldPromptLegacy } from "../legacyMigration";
import type { UiStateManager } from "../state/uiState";

export interface UseLegacyMigrationOptions {
  getUi: () => UiStateManager | null;
}

/** Whether the migrate dialog for a folder's legacy .cde catalogs is open (the sidecar does not load them). */
export function useLegacyMigration({ getUi }: UseLegacyMigrationOptions) {
  const [open, setOpen] = useState(false);

  // After a load: open the dialog when the folder has legacy catalogs, unless "Not now" was chosen for it.
  const promptFor = useCallback(
    (cat: CatalogsResponse) => {
      if (shouldPromptLegacy(cat.legacyCatalogs ?? [], cat.catalogDir, getUi()?.get().legacyMigrateDismissed)) {
        setOpen(true);
      }
    },
    [getUi],
  );

  // Remember the folder so the dialog no longer opens by itself there; the status-bar notice stays.
  const dismiss = useCallback(
    (folder: string | null | undefined) => {
      const ui = getUi();
      if (folder && ui) ui.update({ legacyMigrateDismissed: dismissFolder(ui.get().legacyMigrateDismissed, folder) });
      setOpen(false);
    },
    [getUi],
  );

  const show = useCallback(() => setOpen(true), []);

  return { open, promptFor, dismiss, show };
}
