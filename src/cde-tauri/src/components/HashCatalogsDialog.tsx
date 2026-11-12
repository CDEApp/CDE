import { useMemo, useState } from "react";
import { Button, Group, Modal } from "@mantine/core";
import type { CdeApiClient } from "../api/client";
import type { CatalogInfo, HashOptions } from "../api/types";
import { COMPARED_ACROSS_CATALOGS, hashOptions } from "../hashJobs";
import { useHashPreview } from "../hooks/useHashPreview";
import { hashDisabledReason, type JobsModel } from "../jobsModel";
import { HashFilterFields } from "./HashFilterFields";

export interface HashRequest {
  catalogIds: number[];
  hash: HashOptions | null;
}

interface Props {
  initialCatalogIds: readonly number[] | null;
  catalogs: readonly CatalogInfo[];
  jobs: JobsModel;
  client: CdeApiClient | null;
  onStart: (req: HashRequest) => void;
  onClose: () => void;
}

export function HashCatalogsDialog({ initialCatalogIds, catalogs, jobs, client, onStart, onClose }: Props) {
  return (
    <Modal opened={initialCatalogIds !== null} onClose={onClose} title="Hash files" centered size="lg">
      {initialCatalogIds !== null && (
        <HashCatalogsForm
          initialCatalogIds={initialCatalogIds}
          catalogs={catalogs}
          jobs={jobs}
          client={client}
          onStart={onStart}
          onClose={onClose}
        />
      )}
    </Modal>
  );
}

type FormProps = Omit<Props, "initialCatalogIds"> & { initialCatalogIds: readonly number[] };

function HashCatalogsForm({ initialCatalogIds, catalogs, jobs, client, onStart, onClose }: FormProps) {
  const [selected, setSelected] = useState<ReadonlySet<number>>(() => new Set(initialCatalogIds));
  const [filter, setFilter] = useState("");
  const [includeUniqueSizes, setIncludeUniqueSizes] = useState(false);

  const usable = useMemo(
    () => new Set(catalogs.filter((c) => hashDisabledReason(jobs, c) === null).map((c) => c.catalogId)),
    [catalogs, jobs],
  );
  const catalogIds = useMemo(() => [...selected].filter((id) => usable.has(id)).sort((a, b) => a - b), [selected, usable]);
  const hash = hashOptions(filter, includeUniqueSizes);
  const preview = useHashPreview(client, catalogIds.length === 0 ? null : { catalogIds, hash });
  const canStart = catalogIds.length > 0 && preview.status === "ready";

  const toggle = (id: number, on: boolean) =>
    setSelected((prev) => {
      const next = new Set(prev);
      if (on) next.add(id);
      else next.delete(id);
      return next;
    });

  return (
    <div className="nc-form">
      <div className="nc-label">{COMPARED_ACROSS_CATALOGS}</div>
      <ul className="hash-catalogs" aria-label="Catalogs to hash">
        {catalogs.map((c) => {
          const reason = hashDisabledReason(jobs, c);
          return (
            <li key={c.catalogId}>
              <label className="nc-check" title={reason ?? c.rootPath}>
                <input
                  type="checkbox"
                  checked={selected.has(c.catalogId) && reason === null}
                  disabled={reason !== null}
                  onChange={(e) => toggle(c.catalogId, e.target.checked)}
                />
                {c.rootPath}
              </label>
              {reason && <span className="nc-label"> ({reason})</span>}
            </li>
          );
        })}
      </ul>
      {catalogIds.length === 0 && <div className="nc-label">Pick at least one catalog.</div>}
      <HashFilterFields
        filter={filter}
        includeUniqueSizes={includeUniqueSizes}
        onFilterChange={setFilter}
        onIncludeUniqueSizesChange={setIncludeUniqueSizes}
        preview={preview}
        autoFocus
      />
      <Group justify="flex-end" mt="md">
        <Button variant="default" onClick={onClose}>
          Cancel
        </Button>
        <Button onClick={() => canStart && onStart({ catalogIds, hash })} disabled={!canStart}>
          Start
        </Button>
      </Group>
    </div>
  );
}
