import { useState } from "react";
import { SearchQuery } from "../api/types";

interface Props {
  busy: boolean;
  onSearch: (query: SearchQuery) => void;
  onCancel: () => void;
  history: string[];
  defaults: Partial<SearchQuery>;
}

const LIMITS = [100, 1000, 10000, 100000];

/** Search box + advanced filters (size/date/hour ranges, not-older-than). */
export function SearchBar({ busy, onSearch, onCancel, history, defaults }: Props) {
  const [pattern, setPattern] = useState("");
  const [regexMode, setRegexMode] = useState(defaults.regexMode ?? false);
  const [includePath, setIncludePath] = useState(defaults.includePath ?? false);
  const [includeFiles, setIncludeFiles] = useState(defaults.includeFiles ?? true);
  const [includeFolders, setIncludeFolders] = useState(defaults.includeFolders ?? true);
  const [limit, setLimit] = useState(defaults.limitResultCount ?? 10000);
  const [advanced, setAdvanced] = useState(false);

  // Advanced filters
  const [fromSizeEnable, setFromSizeEnable] = useState(false);
  const [fromSize, setFromSize] = useState(0);
  const [toSizeEnable, setToSizeEnable] = useState(false);
  const [toSize, setToSize] = useState(0);
  const [fromDateEnable, setFromDateEnable] = useState(false);
  const [fromDate, setFromDate] = useState("");
  const [toDateEnable, setToDateEnable] = useState(false);
  const [toDate, setToDate] = useState("");

  function submit() {
    onSearch({
      pattern,
      regexMode,
      includePath,
      includeFiles,
      includeFolders,
      limitResultCount: limit,
      fromSizeEnable,
      fromSize,
      toSizeEnable,
      toSize,
      fromDateEnable,
      fromDate: fromDate ? new Date(fromDate).toISOString() : undefined,
      toDateEnable,
      toDate: toDate ? new Date(toDate).toISOString() : undefined,
    });
  }

  return (
    <div className="searchbar">
      <div className="searchbar-row">
        <input
          className="pattern"
          list="search-history"
          placeholder="Search pattern…"
          value={pattern}
          onChange={(e) => setPattern(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter" && !busy) submit();
          }}
        />
        <datalist id="search-history">
          {history.map((h) => (
            <option key={h} value={h} />
          ))}
        </datalist>
        {busy ? (
          <button className="cancel" onClick={onCancel}>
            Cancel Search
          </button>
        ) : (
          <button onClick={submit}>Search</button>
        )}
        <button className="link" onClick={() => setAdvanced((a) => !a)}>
          {advanced ? "▲ Filters" : "▼ Filters"}
        </button>
      </div>

      <div className="searchbar-row toggles">
        <label><input type="checkbox" checked={regexMode} onChange={(e) => setRegexMode(e.target.checked)} /> Regex</label>
        <label><input type="checkbox" checked={includePath} onChange={(e) => setIncludePath(e.target.checked)} /> Include path</label>
        <label><input type="checkbox" checked={includeFiles} onChange={(e) => setIncludeFiles(e.target.checked)} /> Files</label>
        <label><input type="checkbox" checked={includeFolders} onChange={(e) => setIncludeFolders(e.target.checked)} /> Folders</label>
        <label>
          Limit
          <select value={limit} onChange={(e) => setLimit(Number(e.target.value))}>
            {LIMITS.map((l) => (
              <option key={l} value={l}>{l.toLocaleString()}</option>
            ))}
          </select>
        </label>
      </div>

      {advanced && (
        <div className="searchbar-advanced">
          <fieldset>
            <legend>Size (bytes)</legend>
            <label><input type="checkbox" checked={fromSizeEnable} onChange={(e) => setFromSizeEnable(e.target.checked)} /> From</label>
            <input type="number" value={fromSize} onChange={(e) => setFromSize(Number(e.target.value))} />
            <label><input type="checkbox" checked={toSizeEnable} onChange={(e) => setToSizeEnable(e.target.checked)} /> To</label>
            <input type="number" value={toSize} onChange={(e) => setToSize(Number(e.target.value))} />
          </fieldset>
          <fieldset>
            <legend>Date modified</legend>
            <label><input type="checkbox" checked={fromDateEnable} onChange={(e) => setFromDateEnable(e.target.checked)} /> From</label>
            <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
            <label><input type="checkbox" checked={toDateEnable} onChange={(e) => setToDateEnable(e.target.checked)} /> To</label>
            <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
          </fieldset>
        </div>
      )}
    </div>
  );
}
