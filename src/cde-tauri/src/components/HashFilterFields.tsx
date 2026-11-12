import { useId } from "react";
import { EVERYTHING_SYNTAX_HINT } from "../everythingSearch";
import { hashPreviewText } from "../hashJobs";
import type { HashPreview } from "../hooks/useHashPreview";

interface Props {
  filter: string;
  includeUniqueSizes: boolean;
  onFilterChange: (filter: string) => void;
  onIncludeUniqueSizesChange: (include: boolean) => void;
  /** The live answer for the current filter and flag, from {@link useHashPreview}. */
  preview: HashPreview;
  /** Focus the filter box when it mounts. */
  autoFocus?: boolean;
}

/** The filter box, the unique-size checkbox and the preview line of a hash run. Used by the Hash dialog. */
export function HashFilterFields({ filter, includeUniqueSizes, onFilterChange, onIncludeUniqueSizesChange, preview, autoFocus }: Props) {
  const id = useId();
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
        aria-invalid={preview.status === "error"}
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
      <div role="status" aria-live="polite" className="nc-label">
        {preview.status === "loading" && "Counting files…"}
        {preview.status === "ready" && hashPreviewText(preview.result)}
      </div>
      {preview.status === "error" && (
        <div className="nc-error" role="alert">
          {preview.message}
        </div>
      )}
    </>
  );
}
