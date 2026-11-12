import { useId } from "react";
import { EVERYTHING_SYNTAX_HINT } from "../everythingSearch";
import { hashPreviewText } from "../hashJobs";
import type { HashPreview } from "../hooks/useHashPreview";

interface Props {
  filter: string;
  includeUniqueSizes: boolean;
  onFilterChange: (filter: string) => void;
  onIncludeUniqueSizesChange: (include: boolean) => void;
  /** Omitted when there is nothing to count yet (a catalog that is about to be created). */
  preview?: HashPreview;
  /** A refusal the caller got for this filter, shown like a preview error. */
  error?: string | null;
  autoFocus?: boolean;
}

export function HashFilterFields({ filter, includeUniqueSizes, onFilterChange, onIncludeUniqueSizesChange, preview, error, autoFocus }: Props) {
  const id = useId();
  const shownError = error ?? (preview?.status === "error" ? preview.message : null);
  return (
    <>
      <label className="nc-label" htmlFor={`${id}-filter`}>
        Filter (optional)
      </label>
      <input
        id={`${id}-filter`}
        className="nc-input"
        value={filter}
        spellCheck={false}
        placeholder="e.g. size:>2gb  or  ext:iso;mkv"
        title={EVERYTHING_SYNTAX_HINT}
        aria-describedby={`${id}-hint`}
        aria-invalid={shownError !== null}
        data-autofocus={autoFocus ? true : undefined}
        onChange={(e) => onFilterChange(e.target.value)}
      />
      <div id={`${id}-hint`} className="nc-label">
        Same syntax as the search bar. Only files that match are hashed.
      </div>
      <label className="nc-check">
        <input type="checkbox" checked={includeUniqueSizes} onChange={(e) => onIncludeUniqueSizesChange(e.target.checked)} />
        Also hash files with a unique size
      </label>
      {preview && (
        <div role="status" aria-live="polite" className="nc-label">
          {preview.status === "loading" && "Counting files…"}
          {preview.status === "ready" && hashPreviewText(preview.result)}
        </div>
      )}
      {shownError !== null && (
        <div className="nc-error" role="alert">
          {shownError}
        </div>
      )}
    </>
  );
}
