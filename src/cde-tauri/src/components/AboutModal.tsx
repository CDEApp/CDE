import { useEffect, useState } from "react";
import { Anchor, Button, Group, Modal, Text } from "@mantine/core";
import { getName, getVersion } from "@tauri-apps/api/app";
import { open } from "@tauri-apps/plugin-shell";
import { releaseDateText, REPO_URL } from "../buildInfo";

interface Props {
  opened: boolean;
  onClose: () => void;
}

interface AppInfo {
  name: string;
  version: string;
}

// Plain-browser fallbacks (`pnpm dev` outside Tauri): no app API, so show the product name and "unknown".
const FALLBACK: AppInfo = { name: "CDE", version: "unknown" };

/** The About dialog: program name, version (from the Tauri app), release date (build-time) and the repo link. */
export function AboutModal({ opened, onClose }: Props) {
  const [info, setInfo] = useState<AppInfo>(FALLBACK);
  const [linkError, setLinkError] = useState<string | null>(null);

  useEffect(() => {
    if (!opened) return;
    setLinkError(null);
    let cancelled = false;
    Promise.all([getName(), getVersion()])
      .then(([name, version]) => {
        if (!cancelled) setInfo({ name, version });
      })
      .catch(() => {
        if (!cancelled) setInfo(FALLBACK);
      });
    return () => {
      cancelled = true;
    };
  }, [opened]);

  // Never let the webview navigate to the URL: hand it to the system browser. Only outside Tauri (plain
  // browser dev) fall back to a new tab; inside Tauri a failure is a real problem, so surface it.
  const openRepo = (e: React.MouseEvent) => {
    e.preventDefault();
    open(REPO_URL).catch((err) => {
      if (!("__TAURI_INTERNALS__" in window)) {
        window.open(REPO_URL, "_blank", "noopener,noreferrer");
        return;
      }
      console.error("Failed to open the repository link", err);
      setLinkError(`Could not open the link: ${err instanceof Error ? err.message : String(err)}`);
    });
  };

  return (
    <Modal opened={opened} onClose={onClose} title={`About ${info.name}`} centered>
      <Text fw={700} size="lg">
        {info.name}
      </Text>
      <Text mt="xs">Version: {info.version}</Text>
      <Text>Release date: {releaseDateText(__BUILD_DATE__, import.meta.env.DEV)}</Text>
      <Text mt="xs">
        Repository:{" "}
        <Anchor href={REPO_URL} onClick={openRepo}>
          {REPO_URL}
        </Anchor>
      </Text>
      {linkError && (
        <Text c="red" size="sm" mt="xs" role="alert">
          {linkError}
        </Text>
      )}
      <Group justify="flex-end" mt="md">
        <Button onClick={onClose} data-autofocus>
          Close
        </Button>
      </Group>
    </Modal>
  );
}
