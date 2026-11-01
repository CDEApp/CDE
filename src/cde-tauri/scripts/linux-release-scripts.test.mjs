// Tests for .github/scripts/stage-release-linux.sh and verify-release-versions-linux.sh, run against
// fake build output in a temp folder. They need bash, tar and sha256sum (the CI runners and Git Bash have them).
import { spawnSync } from "node:child_process";
import { chmodSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { afterEach, beforeEach, describe, expect, it } from "vitest";

const here = dirname(fileURLToPath(import.meta.url));
const scriptsDir = join(here, "..", "..", "..", ".github", "scripts");
const posix = (p) => p.replaceAll("\\", "/"); // bash and tar want forward slashes, also on Windows

const bash = (script, args, cwd) =>
  spawnSync("bash", [posix(join(scriptsDir, script)), ...args], { cwd, encoding: "utf8" });

let work;
beforeEach(() => {
  work = mkdtempSync(join(tmpdir(), "cde-linux-scripts-"));
});
afterEach(() => rmSync(work, { recursive: true, force: true }));

const put = (rel, content = "x", mode) => {
  const file = join(work, rel);
  mkdirSync(dirname(file), { recursive: true });
  writeFileSync(file, content);
  if (mode) chmodSync(file, mode);
  return file;
};

// What `./build.sh PublishRelease --self-contained true --runtime linux-x64` leaves in artifacts/.
const fakePublish = () => {
  put("artifacts/cde/cde", "ELF cde", 0o644);
  put("artifacts/cde/appsettings.json", "{}");
  put("artifacts/cde/cde.pdb", "symbols");
  put("artifacts/cdeApi/cdeApi", "ELF api", 0o644);
};

// What `pnpm tauri build --no-bundle` leaves in target/release.
const fakeTauri = () => {
  put("tauri/cde-tauri", "ELF tauri", 0o644);
  put("tauri/cdeApi", "ELF api", 0o644);
};

const stage = (...extra) =>
  bash("stage-release-linux.sh", [
    "--version", "2.1.0-beta.1",
    "--artifacts", posix(join(work, "artifacts")),
    "--tauri-release-dir", posix(join(work, "tauri")),
    "--out", posix(join(work, "out")),
    ...extra,
  ], work);

const listing = () => {
  const r = spawnSync("tar", ["tzf", "cde-2.1.0-beta.1-linux-x64.tar.gz"], { cwd: join(work, "out"), encoding: "utf8" });
  expect(r.status, r.stderr).toBe(0);
  return r.stdout.split("\n").map((l) => l.trim()).filter(Boolean).filter((l) => !l.endsWith("/")).sort();
};

describe("stage-release-linux.sh", () => {
  it("builds cde-<version>-linux-x64.tar.gz with cde/ and cde-tauri/ and no cdewin", () => {
    fakePublish();
    fakeTauri();

    const r = stage();

    expect(r.status, r.stderr + r.stdout).toBe(0);
    expect(listing()).toEqual(
      [
        "cde-2.1.0-beta.1-linux-x64/cde-tauri/cdeApi",
        "cde-2.1.0-beta.1-linux-x64/cde-tauri/cde-tauri",
        "cde-2.1.0-beta.1-linux-x64/cde/appsettings.json",
        "cde-2.1.0-beta.1-linux-x64/cde/cde",
      ].sort(),
    );
  });

  it("leaves debug symbols out", () => {
    fakePublish();
    fakeTauri();
    stage();
    expect(listing().some((l) => l.endsWith(".pdb"))).toBe(false);
  });

  it("writes a .sha256 in `sha256sum -c` format that matches the archive", () => {
    fakePublish();
    fakeTauri();
    stage();

    const sha = readFileSync(join(work, "out", "cde-2.1.0-beta.1-linux-x64.tar.gz.sha256"), "utf8");
    expect(sha).toMatch(/^[0-9a-f]{64} {2}cde-2\.1\.0-beta\.1-linux-x64\.tar\.gz\n$/);
    const check = spawnSync("sha256sum", ["-c", "cde-2.1.0-beta.1-linux-x64.tar.gz.sha256"], { cwd: join(work, "out"), encoding: "utf8" });
    expect(check.status, check.stdout + check.stderr).toBe(0);
  });

  // NTFS has no executable bit, so the mode can only be checked where the file system has one.
  it.skipIf(process.platform === "win32")("sets the executable bit on cde, cde-tauri and cdeApi", () => {
    fakePublish();
    fakeTauri();
    stage();

    const r = spawnSync("tar", ["tvzf", "cde-2.1.0-beta.1-linux-x64.tar.gz"], { cwd: join(work, "out"), encoding: "utf8" });
    const mode = (name) => r.stdout.split("\n").find((l) => l.endsWith(name)).split(/\s+/)[0];
    expect(mode("/cde/cde")).toMatch(/^-rwx/);
    expect(mode("/cde-tauri/cde-tauri")).toMatch(/^-rwx/);
    expect(mode("/cde-tauri/cdeApi")).toMatch(/^-rwx/);
    expect(mode("/cde/appsettings.json")).not.toMatch(/x/);
  });

  it("--no-tauri stages cde only, for the pull request smoke job", () => {
    fakePublish();

    const r = stage("--no-tauri");

    expect(r.status, r.stderr + r.stdout).toBe(0);
    expect(listing()).toEqual([
      "cde-2.1.0-beta.1-linux-x64/cde/appsettings.json",
      "cde-2.1.0-beta.1-linux-x64/cde/cde",
    ]);
  });

  it("fails and names the file when a build output is missing", () => {
    fakePublish(); // no tauri output

    const r = stage();

    expect(r.status).not.toBe(0);
    expect(r.stderr).toContain("Missing build output");
    expect(r.stderr).toContain("cde-tauri");
    expect(existsSync(join(work, "out", "cde-2.1.0-beta.1-linux-x64.tar.gz"))).toBe(false);
  });

  it("fails when cde itself is missing", () => {
    fakeTauri();
    const r = stage();
    expect(r.status).not.toBe(0);
    expect(r.stderr).toContain("Missing build output");
  });

  it.each(["v2.1.0", "2.1", "2.1.0-gamma.1", ""])("rejects the version '%s'", (version) => {
    fakePublish();
    fakeTauri();
    const r = bash("stage-release-linux.sh", [
      "--version", version,
      "--artifacts", posix(join(work, "artifacts")),
      "--tauri-release-dir", posix(join(work, "tauri")),
      "--out", posix(join(work, "out")),
    ], work);
    expect(r.status).not.toBe(0);
    expect(r.stderr).toMatch(/version/i);
  });

  it.each([["--version"], ["--artifacts"], ["--out"], ["--tauri-release-dir"]])(
    "says which option has no value when %s is last",
    (option) => {
      fakePublish();
      fakeTauri();
      const r = bash("stage-release-linux.sh", [option], work);
      expect(r.status).not.toBe(0);
      expect(r.stderr).toContain(option);
      expect(r.stderr).toMatch(/needs a value/);
    },
  );

  it("replaces an earlier archive for the same version", () => {
    fakePublish();
    fakeTauri();
    expect(stage().status).toBe(0);
    put("artifacts/cde/appsettings.json", '{"changed":true}');

    expect(stage().status).toBe(0);

    const r = spawnSync("tar", ["xzOf", "cde-2.1.0-beta.1-linux-x64.tar.gz", "cde-2.1.0-beta.1-linux-x64/cde/appsettings.json"], { cwd: join(work, "out"), encoding: "utf8" });
    expect(r.stdout).toContain("changed");
  });
});

// A staged folder with a fake cde that prints its version the way the real one does ("cde <version>+<commit>").
// Like the real cde, it reads appsettings.json from the current directory and exits without it, even for --version.
const stagedRoot = (cdeVersionLine) => {
  put("root/cde/appsettings.json", "{}");
  put(
    "root/cde/cde",
    `#!/usr/bin/env bash\n[ -f appsettings.json ] || { echo "Configuration file 'appsettings.json' not found" >&2; exit 1; }\necho "${cdeVersionLine}"\necho "Copyright"\n`,
    0o755,
  );
  put("root/cde-tauri/cde-tauri", "ELF", 0o755);
  put("root/cde-tauri/cdeApi", "ELF", 0o755);
  return posix(join(work, "root"));
};

const tauriFiles = (confVersion, cargoVersion) => {
  put("tauri-src/tauri.conf.json", JSON.stringify({ productName: "CDE", version: confVersion }, null, 2));
  put("tauri-src/Cargo.toml", `[package]\nname = "cde-tauri"\nversion = "${cargoVersion}"\n\n[dependencies]\nserde = { version = "1" }\n`);
  return posix(join(work, "tauri-src"));
};

const verify = (root, tauriSrc, version = "2.1.0-beta.1") =>
  bash("verify-release-versions-linux.sh", ["--version", version, "--root", root, "--tauri-src", tauriSrc], work);

describe("verify-release-versions-linux.sh", () => {
  it("passes when cde and the stamped Tauri files report the tag", () => {
    const r = verify(stagedRoot("cde 2.1.0-beta.1+abc1234"), tauriFiles("2.1.0-beta.1", "2.1.0-beta.1"));
    expect(r.status, r.stdout + r.stderr).toBe(0);
    expect(r.stdout).toContain("All checks report 2.1.0-beta.1");
  });

  it("accepts a tag build with no +commit suffix", () => {
    const r = verify(stagedRoot("cde 2.1.0-beta.1"), tauriFiles("2.1.0-beta.1", "2.1.0-beta.1"));
    expect(r.status, r.stdout + r.stderr).toBe(0);
  });

  it("runs cde from its own folder, where its appsettings.json is", () => {
    const r = verify(stagedRoot("cde 2.1.0-beta.1+abc"), tauriFiles("2.1.0-beta.1", "2.1.0-beta.1"));
    expect(r.status, r.stdout + r.stderr).toBe(0);
    expect(r.stdout).toContain("cde --version: 2.1.0-beta.1+abc");
  });

  it("fails when MinVer did not see the tag (a pre-release height version)", () => {
    const r = verify(stagedRoot("cde 2.0.1-alpha.0.354+abc1234"), tauriFiles("2.1.0-beta.1", "2.1.0-beta.1"));
    expect(r.status).not.toBe(0);
    expect(r.stdout).toContain("FAIL");
    expect(r.stdout).toContain("cde --version");
  });

  it("does not take a longer version for the tag", () => {
    const r = verify(stagedRoot("cde 2.1.0-beta.12+abc"), tauriFiles("2.1.0-beta.1", "2.1.0-beta.1"));
    expect(r.status).not.toBe(0);
  });

  it("fails when tauri.conf.json was not stamped", () => {
    const r = verify(stagedRoot("cde 2.1.0-beta.1+abc"), tauriFiles("0.1.0", "2.1.0-beta.1"));
    expect(r.status).not.toBe(0);
    expect(r.stdout).toContain("tauri.conf.json");
  });

  it("fails when Cargo.toml was not stamped, and ignores a dependency's version", () => {
    const r = verify(stagedRoot("cde 2.1.0-beta.1+abc"), tauriFiles("2.1.0-beta.1", "0.1.0"));
    expect(r.status).not.toBe(0);
    expect(r.stdout).toContain("Cargo.toml");
  });

  it.each([["--version"], ["--root"], ["--tauri-src"]])("says which option has no value when %s is last", (option) => {
    const r = bash("verify-release-versions-linux.sh", [option], work);
    expect(r.status).not.toBe(0);
    expect(r.stderr).toContain(option);
    expect(r.stderr).toMatch(/needs a value/);
  });

  it("fails when a staged file is missing", () => {
    const root = stagedRoot("cde 2.1.0-beta.1+abc");
    rmSync(join(work, "root", "cde-tauri", "cdeApi"));
    const r = verify(root, tauriFiles("2.1.0-beta.1", "2.1.0-beta.1"));
    expect(r.status).not.toBe(0);
    expect(r.stdout).toContain("file not found");
  });
});
