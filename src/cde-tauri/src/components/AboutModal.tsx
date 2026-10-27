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

  useEffect(() => {
    if (!opened) return;
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

  // Never let the webview navigate to the URL: hand it to the system browser. Outside Tauri this fails, so
  // fall back to a new browser tab.
  const openRepo = (e: React.MouseEvent) => {
    e.preventDefault();
    open(REPO_URL).catch(() => window.open(REPO_URL, "_blank", "noopener,noreferrer"));
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
      <Group justify="flex-end" mt="md">
        <Button onClick={onClose} data-autofocus>
          Close
        </Button>
      </Group>
    </Modal>
  );
}
