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
  const stderr: string[] = [];
  command.stderr.on("data", (line: string) => {
    if (line.trim()) {
      stderr.push(line.trim());
      console.debug("[cdeApi]", line);
    }
  });
  command.on("error", (err) => console.error("[cdeApi] command error", err));
  command.on("close", (data) =>
    console.warn("[cdeApi] exited", JSON.stringify(data)),
  );

  const tail = () => (stderr.length ? ` | sidecar stderr: ${stderr.slice(-6).join(" / ")}` : "");

  let child: Child | null = null;
  const timeout = setTimeout(
    () => rejectConn(new Error(`sidecar handshake timed out${tail()}`)),
    15000,
  );

  try {
    child = await command.spawn();
    const connection = await connPromise;
    clearTimeout(timeout);
    const handle = child;
    return { connection, kill: async () => { await handle?.kill(); } };
  } catch (e) {
    clearTimeout(timeout);
    await child?.kill();
    const detail = e instanceof Error ? e.message : typeof e === "string" ? e : JSON.stringify(e);
    throw new Error(`sidecar spawn failed: ${detail}${tail()}`);
  }
}
