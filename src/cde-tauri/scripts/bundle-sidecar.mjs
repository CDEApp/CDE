// Publishes cdeApi as a self-contained single-file binary and stages it under
// src-tauri/binaries/cdeApi-<target-triple>(.exe) where Tauri's externalBin sidecar expects it.
import { execSync } from "node:child_process";
import { copyFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, ".."); // src/cde-tauri
const repo = join(root, "..", ".."); // repo root
const apiProj = join(repo, "src", "cdeApi", "cdeApi.csproj");

const triple = execSync("rustc -vV")
  .toString()
  .split("\n")
  .find((l) => l.startsWith("host:"))
  .split(":")[1]
  .trim();
const rid = tripleToRid(triple);
const ext = triple.includes("windows") ? ".exe" : "";

console.log(`Publishing cdeApi for ${rid} …`);
execSync(
  `dotnet publish "${apiProj}" -c Release -r ${rid} --self-contained ` +
    `-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`,
  { stdio: "inherit" },
);

const pubExe = join(repo, "src", "cdeApi", "bin", "Release", "net10.0", rid, "publish", `cdeApi${ext}`);
const binDir = join(root, "src-tauri", "binaries");
mkdirSync(binDir, { recursive: true });
const dest = join(binDir, `cdeApi-${triple}${ext}`);
copyFileSync(pubExe, dest);
console.log(`Sidecar staged: ${dest}`);

function tripleToRid(t) {
  if (t.includes("windows")) return "win-x64";
  if (t.includes("apple")) return t.includes("aarch64") ? "osx-arm64" : "osx-x64";
  return "linux-x64";
}
