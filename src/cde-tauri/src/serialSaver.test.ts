import { describe, expect, it } from "vitest";
import { createSerialSaver } from "./serialSaver";

/** A put() whose calls resolve only when the test says so, recording the order writes land in. */
function controllablePut() {
  const landed: string[] = [];
  const waiting: { text: string; resolve: () => void; reject: (e: Error) => void }[] = [];
  const put = (text: string) =>
    new Promise<void>((resolve, reject) => {
      waiting.push({
        text,
        resolve: () => {
          landed.push(text);
          resolve();
        },
        reject,
      });
    });
  return { put, landed, waiting };
}

const tick = () => new Promise((r) => setTimeout(r, 0));

describe("createSerialSaver", () => {
  it("starts the next write only after the previous one finishes, so writes land in order", async () => {
    const { put, landed, waiting } = controllablePut();
    const saver = createSerialSaver(put, () => {});

    saver.save("one");
    saver.save("two");
    await tick();
    expect(waiting.map((w) => w.text)).toEqual(["one"]);

    waiting[0].resolve();
    await tick();
    expect(waiting.map((w) => w.text)).toEqual(["one", "two"]);
    waiting[1].resolve();
    await saver.idle();

    expect(landed).toEqual(["one", "two"]);
  });

  it("idle() waits for every queued write", async () => {
    const { put, waiting } = controllablePut();
    const saver = createSerialSaver(put, () => {});
    saver.save("one");

    let idle = false;
    void saver.idle().then(() => (idle = true));
    await tick();
    expect(idle).toBe(false);

    waiting[0].resolve();
    await tick();
    expect(idle).toBe(true);
  });

  it("idle() resolves straight away with nothing queued", async () => {
    const saver = createSerialSaver(() => Promise.resolve(), () => {});
    await expect(saver.idle()).resolves.toBeUndefined();
  });

  it("reports each write's outcome, and a failure doesn't block later writes", async () => {
    const { put, landed, waiting } = controllablePut();
    const outcomes: (string | null)[] = [];
    const saver = createSerialSaver(put, (error) => outcomes.push(error));

    saver.save("bad");
    saver.save("good");
    await tick();
    waiting[0].reject(new Error("disk full"));
    await tick();
    waiting[1].resolve();
    await saver.idle();

    expect(outcomes).toEqual(["disk full", null]);
    expect(landed).toEqual(["good"]);
  });
});
