import { describe, expect, it } from "vitest";
import type { SearchResultRow } from "./api/types";
import { rowsToClipboardText } from "./copyText";

const r = (name: string, parentPath: string) => ({ name, parentPath, fullPath: `${parentPath}${name}` }) as SearchResultRow;
const rows = [r("b.txt", "C:\\x\\"), r("a.txt", "C:\\y\\")];

describe("rowsToClipboardText", () => {
  it("joins full paths one per line in the order given", () => {
    expect(rowsToClipboardText(rows, "path")).toBe("C:\\x\\b.txt\nC:\\y\\a.txt");
  });

  it("joins names", () => {
    expect(rowsToClipboardText(rows, "name")).toBe("b.txt\na.txt");
  });

  it("joins parent folders, repeating a shared one", () => {
    expect(rowsToClipboardText([...rows, r("c.txt", "C:\\x\\")], "parent")).toBe("C:\\x\\\nC:\\y\\\nC:\\x\\");
  });

  it("is empty for no rows", () => {
    expect(rowsToClipboardText([], "path")).toBe("");
  });
});
