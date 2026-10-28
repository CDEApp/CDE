import { useEffect, useMemo, useRef, useState } from "react";
import { Button, Group, Modal, Progress, ScrollArea, Stack, Text } from "@mantine/core";
import { MigrateFailure, type CdeApiClient } from "../api/client";
import type { LegacyCatalog, MigrateProgress } from "../api/types";
import { mb } from "../format";
import { fileName, legacyFoundText, migratingText, migrationReport, summarizeLegacy } from "../legacyMigration";

interface Props {
  opened: boolean;
  legacy: readonly LegacyCatalog[];
  /** The catalog folder the legacy catalogs were found in. */
  folder: string;
  /** Why a migration can't start right now (another catalog job is running), or null. */
  blockedReason: string | null;
  client: CdeApiClient | null;
  /** Closed without migrating: remember the choice for this folder. */
  onNotNow: () => void;
  /** A migration ran (done, failed or cancelled) and its report was closed: reload to pick up the new catalogs. */
  onFinished: () => void;
}

type Stage = "confirm" | "migrating" | "report";

interface Report {
  headline: string;
  lines: string[];
}

function CatalogLines({ items }: { items: readonly LegacyCatalog[] }) {
  return (
    <ScrollArea.Autosize mah={180} type="auto">
      {items.map((l) => (
        <Text key={l.path} size="sm" title={l.path}>
          {fileName(l.path)} ({mb(l.sizeBytes)})
        </Text>
      ))}
    </ScrollArea.Autosize>
  );
}

/**
 * Offers to convert the legacy .cde catalogs the sidecar listed (and did not load) to the .cdex format, shows the
 * progress, then the report. It can't be closed while a migration runs: Cancel stops it after the catalog in
 * progress, and the catalogs already converted stay converted.
 */
export function MigrateLegacyDialog({ opened, legacy, folder, blockedReason, client, onNotNow, onFinished }: Props) {
  const [stage, setStage] = useState<Stage>("confirm");
  const [progress, setProgress] = useState<MigrateProgress | null>(null);
  const [report, setReport] = useState<Report | null>(null);
  const [failure, setFailure] = useState<string | null>(null);
  const [cancelled, setCancelled] = useState(false);
  const abortRef = useRef<AbortController | null>(null);
  const summary = useMemo(() => summarizeLegacy(legacy), [legacy]);

  // Each opening starts at the question.
  useEffect(() => {
    if (!opened) return;
    setStage("confirm");
    setProgress(null);
    setReport(null);
    setFailure(null);
    setCancelled(false);
  }, [opened]);

  const migrate = async () => {
    if (!client) return;
    const abort = new AbortController();
    abortRef.current = abort;
    setStage("migrating");
    try {
      const result = await client.migrateLegacy({ onProgress: setProgress, signal: abort.signal });
      if (result) setReport(migrationReport(result));
      else setCancelled(true);
    } catch (e) {
      setFailure(e instanceof MigrateFailure ? e.error.message : e instanceof Error ? e.message : String(e));
    } finally {
      abortRef.current = null;
    }
    setStage("report");
  };

  const migrating = stage === "migrating";
  const close = stage === "report" ? onFinished : onNotNow;

  return (
    <Modal
      opened={opened}
      onClose={close}
      title="Legacy catalogs"
      centered
      size="lg"
      withCloseButton={!migrating}
      closeOnClickOutside={!migrating}
      closeOnEscape={!migrating}
    >
      {stage === "confirm" && summary.writable.length === 0 && (
        <Stack gap="sm">
          <Text>{legacyFoundText(summary, folder)}</Text>
          <Text>
            <b>{folder}</b> is read-only, so the catalogs can't be converted here. Copy them somewhere writable, or
            run <code>cde migrate</code> there.
          </Text>
          <Group justify="flex-end">
            <Button onClick={onNotNow} data-autofocus>
              Close
            </Button>
          </Group>
        </Stack>
      )}

      {stage === "confirm" && summary.writable.length > 0 && (
        <Stack gap="sm">
          <Text>{legacyFoundText(summary, folder)}</Text>
          <Text size="sm">
            Legacy catalogs load slowly and use a lot of memory, so they are not loaded. Convert them to the faster
            format? Each original is kept, renamed to <code>.cde.migrated</code>.
          </Text>
          <CatalogLines items={summary.writable} />
          {summary.readOnly.length > 0 && (
            <>
              <Text size="sm">
                {summary.readOnly.length === 1 ? "This one is" : `These ${summary.readOnly.length} are`} in a
                read-only folder and can't be converted there:
              </Text>
              <CatalogLines items={summary.readOnly} />
            </>
          )}
          {blockedReason && (
            <Text size="sm" c="red">
              {blockedReason}
            </Text>
          )}
          <Group justify="flex-end">
            <Button variant="default" onClick={onNotNow}>
              Not now
            </Button>
            <Button onClick={() => void migrate()} disabled={blockedReason !== null || !client} data-autofocus>
              Migrate
            </Button>
          </Group>
        </Stack>
      )}

      {migrating && (
        <Stack gap="sm">
          <Text>{progress ? migratingText(progress) : "Starting…"}</Text>
          <Progress value={progress ? (100 * (progress.current - 1)) / progress.total : 0} />
          <Group justify="flex-end">
            <Button
              variant="default"
              onClick={() => abortRef.current?.abort()}
              title="Stop after discarding the catalog in progress; the ones already converted stay converted"
            >
              Cancel
            </Button>
          </Group>
        </Stack>
      )}

      {stage === "report" && (
        <Stack gap="sm">
          {report && <Text>{report.headline}</Text>}
          {report && report.lines.length > 0 && (
            <ScrollArea.Autosize mah={220} type="auto">
              {report.lines.map((line) => (
                <Text key={line} size="sm">
                  {line}
                </Text>
              ))}
            </ScrollArea.Autosize>
          )}
          {cancelled && <Text>Migration cancelled. Catalogs converted before the cancel stay converted.</Text>}
          {failure && <Text c="red">Migration failed: {failure}</Text>}
          <Group justify="flex-end">
            <Button onClick={onFinished} data-autofocus>
              Close
            </Button>
          </Group>
        </Stack>
      )}
    </Modal>
  );
}
