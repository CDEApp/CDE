import { useEffect, useMemo, useRef, useState } from "react";
import { Button, Group, Modal } from "@mantine/core";
import type { CdeApiClient } from "../api/client";
import type { CatalogInfo, CreatePreflightResult } from "../api/types";
import { CATALOG_EXTENSION, deriveDialogState, normalizeFileName } from "../newCatalog";

export interface NewCatalogForm {
  rootPath: string;
  /** The final name, no extension. */
  fileName: string;
  description: string;
  followJunctions: boolean;
  /** Where the preflight said it would be written. */
  targetPath: string | null;
}

interface Props {
  /** The folder to catalog, or null when the dialog is closed. */
  rootPath: string | null;
  client: CdeApiClient | null;
  catalogs: readonly CatalogInfo[];
  onChangeRoot: () => void;
  onCreate: (form: NewCatalogForm) => void;
  /** Run the rescan of this loaded catalog instead (its file already exists under the chosen name). */
  onRescanExisting: (catalogId: number) => void;
  onClose: () => void;
}

const PREFLIGHT_DEBOUNCE_MS = 250;

/** Confirms a new catalog: root, file name (cde default pre-filled), description, junctions; shows what blocks it. */
export function NewCatalogDialog({ rootPath, client, catalogs, onChangeRoot, onCreate, onRescanExisting, onClose }: Props) {
  const [fileName, setFileName] = useState<string | null>(null); // null = the default name
  const [description, setDescription] = useState("");
  const [followJunctions, setFollowJunctions] = useState(false);
  const [preflight, setPreflight] = useState<CreatePreflightResult | null>(null);
  const [preflightError, setPreflightError] = useState<string | null>(null);
  const seq = useRef(0);

  // A different root starts a fresh form (its own default name); the description and junctions stay as typed.
  useEffect(() => {
    setFileName(null);
    setPreflight(null);
    setPreflightError(null);
  }, [rootPath]);

  // Ask the sidecar (debounced while typing) what the current root + name would do.
  const typedName = fileName === null ? null : normalizeFileName(fileName);
  useEffect(() => {
    if (rootPath === null || client === null) return;
    const mine = ++seq.current;
    const abort = new AbortController();
    const timer = window.setTimeout(
      () => {
        client
          .preflightCreate({ rootPath, fileName: typedName }, abort.signal)
          .then((r) => {
            if (mine !== seq.current) return;
            setPreflight(r);
            setPreflightError(null);
          })
          .catch((e) => {
            if (mine !== seq.current || (e as Error)?.name === "AbortError") return;
            setPreflightError(e instanceof Error ? e.message : String(e));
          });
      },
      typedName === null ? 0 : PREFLIGHT_DEBOUNCE_MS,
    );
    return () => {
      window.clearTimeout(timer);
      abort.abort();
    };
  }, [rootPath, typedName, client]);

  const state = useMemo(
    () => deriveDialogState({ rootPath: rootPath ?? "", fileName, preflight, preflightError }),
    [rootPath, fileName, preflight, preflightError],
  );

  const existingRoot =
    state.existingCatalogId !== null ? catalogs.find((c) => c.catalogId === state.existingCatalogId)?.rootPath : undefined;

  const create = () => {
    if (rootPath === null || !state.canCreate) return;
    onCreate({
      rootPath,
      fileName: state.effectiveName,
      description: description.trim(),
      followJunctions,
      targetPath: preflight?.targetPath ?? null,
    });
  };

  const shownName = fileName ?? preflight?.defaultFileName ?? "";

  return (
    <Modal opened={rootPath !== null} onClose={onClose} title="New catalog" centered size="lg">
      <div className="nc-form" onKeyDown={(e) => e.key === "Enter" && (e.target as HTMLElement).tagName === "INPUT" && create()}>
        <label className="nc-label">Root</label>
        <div className="nc-row">
          <input className="nc-input" value={rootPath ?? ""} readOnly title={rootPath ?? undefined} />
          <button type="button" onClick={onChangeRoot}>
            Change…
          </button>
        </div>
        {state.rootError && <div className="nc-error">{state.rootError}</div>}

        <label className="nc-label" htmlFor="nc-name">
          File name
        </label>
        <div className="nc-row">
          <input
            id="nc-name"
            className="nc-input"
            value={shownName}
            data-autofocus
            spellCheck={false}
            onChange={(e) => setFileName(e.target.value)}
          />
          <span className="nc-ext">{CATALOG_EXTENSION}</span>
        </div>
        {state.nameError && <div className="nc-error">{state.nameError}</div>}
        {state.existsError && (
          <div className="nc-error">
            <div>{state.existsError}</div>
            {state.canRescanExisting && state.existingCatalogId !== null && (
              <div className="nc-row nc-rescan">
                <span>
                  Rescan the loaded catalog{existingRoot ? <> of <b>{existingRoot}</b></> : null} instead?
                </span>
                <button type="button" onClick={() => onRescanExisting(state.existingCatalogId!)}>
                  Rescan existing
                </button>
              </div>
            )}
          </div>
        )}

        <label className="nc-label" htmlFor="nc-desc">
          Description
        </label>
        <input
          id="nc-desc"
          className="nc-input"
          value={description}
          placeholder="(optional)"
          onChange={(e) => setDescription(e.target.value)}
        />

        <label className="nc-check">
          <input type="checkbox" checked={followJunctions} onChange={(e) => setFollowJunctions(e.target.checked)} />
          Follow junctions
        </label>

        <label className="nc-label">Saved to</label>
        <input className="nc-input" value={preflight?.catalogDir ?? ""} readOnly title={preflight?.catalogDir ?? undefined} />
        {state.folderError && <div className="nc-error">{state.folderError}</div>}
        {state.busyError && <div className="nc-error">{state.busyError}</div>}
        {state.preflightError && <div className="nc-error">Could not check the settings: {state.preflightError}</div>}
        {state.sameRootWarning && <div className="nc-warning">{state.sameRootWarning}</div>}
      </div>

      <Group justify="flex-end" mt="md">
        <Button variant="default" onClick={onClose}>
          Cancel
        </Button>
        <Button onClick={create} disabled={!state.canCreate} title={state.checking ? "Checking…" : undefined}>
          Create
        </Button>
      </Group>
    </Modal>
  );
}
