import { useState } from "react";
import { SearchQuery } from "../api/types";
import { parseSize } from "../format";

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

  // Advanced filters — sizes are entered as human-readable text ("25 KB", "2.5 MB", "4GB"; a bare
  // number is bytes) and parsed to bytes on submit.
  const [fromSizeEnable, setFromSizeEnable] = useState(false);
  const [fromSizeText, setFromSizeText] = useState("");
  const [toSizeEnable, setToSizeEnable] = useState(false);
  const [toSizeText, setToSizeText] = useState("");
  const [fromDateEnable, setFromDateEnable] = useState(false);
  const [fromDate, setFromDate] = useState("");
  const [toDateEnable, setToDateEnable] = useState(false);
  const [toDate, setToDate] = useState("");

  const fromSizeInvalid = fromSizeEnable && fromSizeText.trim() !== "" && parseSize(fromSizeText) === null;
  const toSizeInvalid = toSizeEnable && toSizeText.trim() !== "" && parseSize(toSizeText) === null;

  function submit() {
    onSearch({
      pattern,
      regexMode,
      includePath,
      includeFiles,
      includeFolders,
      limitResultCount: limit,
      fromSizeEnable,
      fromSize: parseSize(fromSizeText) ?? 0,
      toSizeEnable,
      toSize: parseSize(toSizeText) ?? 0,
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
            <legend>Size (e.g. 25 KB, 2.5 MB, 4GB; bare number = bytes)</legend>
            <label><input type="checkbox" checked={fromSizeEnable} onChange={(e) => setFromSizeEnable(e.target.checked)} /> From</label>
            <input
              type="text"
              placeholder="e.g. 2.5 MB"
              className={fromSizeInvalid ? "invalid" : ""}
              title={fromSizeInvalid ? "Could not parse size" : ""}
              value={fromSizeText}
              onChange={(e) => setFromSizeText(e.target.value)}
            />
            <label><input type="checkbox" checked={toSizeEnable} onChange={(e) => setToSizeEnable(e.target.checked)} /> To</label>
            <input
              type="text"
              placeholder="e.g. 4 GB"
              className={toSizeInvalid ? "invalid" : ""}
              title={toSizeInvalid ? "Could not parse size" : ""}
              value={toSizeText}
              onChange={(e) => setToSizeText(e.target.value)}
            />
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
