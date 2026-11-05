import { useEffect, useState, type KeyboardEvent } from "react";
import { Button, Group, Modal } from "@mantine/core";
import type { CatalogInfo } from "../api/types";

/** The server refuses a longer description; the box stops the user typing past it. */
export const MAX_DESCRIPTION_LENGTH = 1000;

interface Props {
  /** The catalog being edited, or null when the dialog is closed. */
  catalog: CatalogInfo | null;
  /** Save the new text. Rejects with the reason when the server refuses; the dialog stays open and shows it. */
  onSave: (catalogId: number, description: string) => Promise<void>;
  onClose: () => void;
}

/** Edit a catalog's description. Ctrl+Enter saves. */
export function EditDescriptionDialog({ catalog, onSave, onClose }: Props) {
  const [text, setText] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const catalogId = catalog?.catalogId ?? null;
  useEffect(() => {
    setText(catalog?.description ?? "");
    setSaving(false);
    setError(null);
    // Reset only when a different catalog is opened, not when a background refresh replaces the object.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [catalogId]);

  async function save() {
    if (catalog === null || saving) return;
    setSaving(true);
    setError(null);
    try {
      await onSave(catalog.catalogId, text);
      onClose();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setSaving(false);
    }
  }

  function onKeyDown(e: KeyboardEvent) {
    if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) {
      e.preventDefault();
      void save();
    }
  }

  return (
    <Modal
      opened={catalog !== null}
      onClose={onClose}
      title="Edit description"
      centered
      size="lg"
      withCloseButton={!saving}
      closeOnClickOutside={!saving}
      closeOnEscape={!saving}
    >
      <div className="nc-form">
        <label className="nc-label" htmlFor="ed-text">
          {catalog?.rootPath}
        </label>
        <textarea
          id="ed-text"
          className="nc-input"
          rows={4}
          maxLength={MAX_DESCRIPTION_LENGTH}
          value={text}
          onChange={(e) => setText(e.target.value)}
          onKeyDown={onKeyDown}
          placeholder="Describe this catalog…"
          data-autofocus
        />
        {error && <div className="nc-error" role="alert">{error}</div>}
      </div>
      <Group justify="flex-end" mt="md">
        <Button variant="default" onClick={onClose} disabled={saving}>
          Cancel
        </Button>
        <Button onClick={() => void save()} loading={saving}>
          Save
        </Button>
      </Group>
    </Modal>
  );
}
