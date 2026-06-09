import { Command, Child } from "@tauri-apps/plugin-shell";

export interface Connection {
  baseUrl: string;
  token: string;
}

export interface Sidecar {
  connection: Connection;
  kill: () => Promise<void>;
}

/**
 * Spawn the bundled cdeApi sidecar, read its stdout handshake ({ url, token }), and return the
 * connection plus a kill handle. The sidecar binds to 127.0.0.1 on an ephemeral port; all of its
 * logging goes to stderr, so stdout carries only the single handshake JSON line.
 */
export async function startSidecar(): Promise<Sidecar> {
  const command = Command.sidecar("binaries/cdeApi");

  let resolveConn!: (c: Connection) => void;
  let rejectConn!: (e: Error) => void;
  const connPromise = new Promise<Connection>((res, rej) => {
    resolveConn = res;
    rejectConn = rej;
  });

  command.stdout.on("data", (line: string) => {
    const trimmed = line.trim();
    if (!trimmed) return;
    try {
      const obj = JSON.parse(trimmed) as Partial<Connection> & { url?: string };
      if (obj.url && obj.token) resolveConn({ baseUrl: obj.url, token: obj.token });
    } catch {
      /* not the handshake line — ignore */
    }
  });
  command.stderr.on("data", (line: string) => {
    if (line.trim()) console.debug("[cdeApi]", line);
  });

  let child: Child | null = null;
  const timeout = setTimeout(() => rejectConn(new Error("sidecar handshake timed out")), 15000);

  try {
    child = await command.spawn();
    const connection = await connPromise;
    clearTimeout(timeout);
    const handle = child;
    return { connection, kill: async () => { await handle?.kill(); } };
  } catch (e) {
    clearTimeout(timeout);
    await child?.kill();
    throw e;
  }
}
