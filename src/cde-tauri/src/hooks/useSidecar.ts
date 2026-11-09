import { useCallback, useEffect, useRef, useState } from "react";
import { getCurrentWindow } from "@tauri-apps/api/window";
import { CdeApiClient } from "../api/client";
import { startSidecar, type Sidecar } from "../sidecar";
import { UiStateManager, type UiState } from "../state/uiState";

export interface UseSidecarOptions {
  /** Runs once the client and UI state are ready, before `ready` turns true; a throw fails the start. */
  boot: (ui: UiState) => Promise<void>;
}

/**
 * The cdeApi sidecar and what hangs off it: spawn it, build the client and the UI-state manager, run
 * `boot`, then report ready. On window close, flush UI state and kill the sidecar before the window goes.
 */
export function useSidecar({ boot }: UseSidecarOptions) {
  const clientRef = useRef<CdeApiClient | null>(null);
  const uiRef = useRef<UiStateManager | null>(null);
  const sidecarRef = useRef<Sidecar | null>(null);
  const [status, setStatus] = useState("Starting…");
  const [ready, setReady] = useState(false);
  // The boot step closes over the caller's hooks; the start effect runs once, so it reads the latest one.
  const bootRef = useRef(boot);
  bootRef.current = boot;

  useEffect(() => {
    let disposed = false;
    void (async () => {
      try {
        const sidecar = await startSidecar();
        if (disposed) {
          await sidecar.kill();
          return;
        }
        sidecarRef.current = sidecar;
        const client = new CdeApiClient(sidecar.connection.baseUrl, sidecar.connection.token);
        clientRef.current = client;
        uiRef.current = new UiStateManager(client);
        await bootRef.current(await uiRef.current.load());
        setReady(true);
        setStatus("");
      } catch (e) {
        console.error("bootstrap failed", e);
        const msg = e instanceof Error ? e.message : typeof e === "string" ? e : JSON.stringify(e);
        setStatus(`Failed to start: ${msg}`);
      }
    })();
    return () => {
      disposed = true;
    };
  }, []);

  useEffect(() => {
    const win = getCurrentWindow();
    const unlistenPromise = win.onCloseRequested(async (event) => {
      event.preventDefault();
      // Best-effort cleanup; neither step may block the actual close.
      try {
        await uiRef.current?.flush();
      } catch {
        /* ignore */
      }
      try {
        await sidecarRef.current?.kill();
      } catch {
        /* ignore */
      }
      await win.destroy();
    });
    return () => {
      void unlistenPromise.then((f) => f());
    };
  }, []);

  // Stable accessors, so the callbacks of hooks built on them keep a stable identity.
  const getClient = useCallback(() => {
    const client = clientRef.current;
    if (!client) throw new Error("The catalog service is not ready yet.");
    return client;
  }, []);
  const getUi = useCallback(() => uiRef.current, []);

  return { getClient, getUi, ready, status, setStatus };
}
