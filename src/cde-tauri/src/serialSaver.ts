// Ordered, fire-and-forget saving: kept free of React so it unit-tests in plain Node.

export interface SerialSaver {
  /** Queue a write; it starts once every earlier write has finished. */
  save(text: string): void;
  /** Resolves when every write queued so far has finished (successfully or not). */
  idle(): Promise<void>;
}

/**
 * Serialise writes so an older body can never land after a newer one, and so a reader can wait
 * for pending writes (`idle`) before re-reading what was saved. `onResult` gets null on success
 * or the error message on failure; a failure doesn't stop later writes.
 */
export function createSerialSaver(
  put: (text: string) => Promise<void>,
  onResult: (error: string | null) => void,
): SerialSaver {
  let tail: Promise<void> = Promise.resolve();
  return {
    save(text) {
      tail = tail.then(() =>
        put(text).then(
          () => onResult(null),
          (e: unknown) => onResult(e instanceof Error ? e.message : String(e)),
        ),
      );
    },
    idle: () => tail,
  };
}
