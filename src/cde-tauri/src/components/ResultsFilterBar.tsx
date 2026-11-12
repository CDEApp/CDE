import type { ResultsFilterApi } from "../hooks/useResultsFilter";
import { countText, type FilterField, type FilterMode } from "../resultsFilter";

interface Props {
  filter: ResultsFilterApi;
  busy: boolean;
  canSearchAgain: boolean;
  onSearchAgain: () => void;
}

const SEARCH_AGAIN_TITLE =
  "Runs a new search for the last query and this filter together. The query is rewritten in Everything syntax so it can hold both.";

export function ResultsFilterBar({ filter, busy, canSearchAgain, onSearchAgain }: Props) {
  const { text, mode, field, error } = filter;
  const searchDisabled = busy || !canSearchAgain || !filter.searchable;
  return (
    <div className="results-filter">
      <input
        type="text"
        className={error ? "invalid" : ""}
        aria-label="Filter loaded results"
        placeholder="Filter loaded results…"
        value={text}
        onChange={(e) => filter.setText(e.target.value)}
      />
      <select aria-label="Filter field" value={field} onChange={(e) => filter.setField(e.target.value as FilterField)}>
        <option value="name">Name</option>
        <option value="path">Full path</option>
      </select>
      <label>
        <input
          type="checkbox"
          checked={mode === "regex"}
          onChange={(e) => filter.setMode((e.target.checked ? "regex" : "substring") as FilterMode)}
        />{" "}
        Regex
      </label>
      {error ? (
        <span className="filter-error" role="alert">
          {error}
        </span>
      ) : (
        text !== "" && (
          <span
            className="filter-count"
            title="Counts only the rows already loaded. The search limit may have left matches out; Search again with this filter looks for them."
          >
            {countText(filter.shown.length, filter.total)}
          </span>
        )
      )}
      <button disabled={searchDisabled} title={SEARCH_AGAIN_TITLE} onClick={onSearchAgain}>
        Search again with this filter
      </button>
    </div>
  );
}
