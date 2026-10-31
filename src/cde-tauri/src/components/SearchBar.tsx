import { useState } from "react";
import { SearchQuery } from "../api/types";
import { AGE_UNITS, notOlderThanCutoff, parseAge, parseSize, toLocalIsoString, type AgeUnit } from "../format";
import { PatternCombo } from "./PatternCombo";
import { EVERYTHING_SYNTAX_HINT } from "../everythingSearch";

interface Props {
  busy: boolean;
  onSearch: (query: SearchQuery) => void;
  onCancel: () => void;
  history: string[];
  defaults: Partial<SearchQuery>;
  /** Initial state of the ▼ Filters panel (persisted UI state). */
  filtersExpanded: boolean;
  onFiltersExpandedChange: (expanded: boolean) => void;
}

const LIMITS = [100, 1000, 10000, 100000];

/** Search box + advanced filters (size/date/hour ranges, not-older-than). */
export function SearchBar({
  busy, onSearch, onCancel, history, defaults, filtersExpanded, onFiltersExpandedChange,
}: Props) {
  const [pattern, setPattern] = useState("");
  const [regexMode, setRegexMode] = useState(defaults.regexMode ?? false);
  const [everythingMode, setEverythingMode] = useState(defaults.everythingMode ?? false);
  const [includePath, setIncludePath] = useState(defaults.includePath ?? false);
  const [includeFiles, setIncludeFiles] = useState(defaults.includeFiles ?? true);
  const [includeFolders, setIncludeFolders] = useState(defaults.includeFolders ?? true);
  const [limit, setLimit] = useState(defaults.limitResultCount ?? 10000);
  const [advanced, setAdvancedState] = useState(filtersExpanded);
  const setAdvanced = (expanded: boolean) => {
    setAdvancedState(expanded);
    onFiltersExpandedChange(expanded);
  };

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
  // "Not older than N <unit>": converted to an absolute modified-date cutoff at submit time.
  const [notOlderThanEnable, setNotOlderThanEnable] = useState(false);
  const [notOlderThanText, setNotOlderThanText] = useState("1");
  const [notOlderThanUnit, setNotOlderThanUnit] = useState<AgeUnit>("hours");

  const fromSizeInvalid = fromSizeEnable && fromSizeText.trim() !== "" && parseSize(fromSizeText) === null;
  const toSizeInvalid = toSizeEnable && toSizeText.trim() !== "" && parseSize(toSizeText) === null;
  const notOlderThanAmount = parseAge(notOlderThanText);
  const notOlderThanInvalid = notOlderThanEnable && notOlderThanAmount === null;

  function submit() {
    // Don't run a search with a filter the user asked for but we can't apply.
    if (notOlderThanInvalid) {
      setAdvanced(true);
      return;
    }
    onSearch({
      pattern,
      regexMode,
      everythingMode,
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
      notOlderThanEnable,
      notOlderThan:
        notOlderThanEnable && notOlderThanAmount !== null
          ? toLocalIsoString(notOlderThanCutoff(notOlderThanAmount, notOlderThanUnit))
          : undefined,
    });
  }

  return (
    <div className="searchbar">
      <div className="searchbar-row">
        <PatternCombo
          placeholder="Search pattern…"
          value={pattern}
          onChange={setPattern}
          onEnter={() => {
            if (!busy) submit();
          }}
          history={history}
        />
        {busy ? (
          <button className="cancel" onClick={onCancel}>
            Cancel Search
          </button>
        ) : (
          <button onClick={submit}>Search</button>
        )}
        <button className="link" onClick={() => setAdvanced(!advanced)}>
          {advanced ? "▲ Filters" : "▼ Filters"}
        </button>
      </div>

      <div className="searchbar-row toggles">
        <label title={everythingMode ? "Regex is off while Everything is on" : ""}>
          <input type="checkbox" checked={regexMode && !everythingMode} disabled={everythingMode} onChange={(e) => setRegexMode(e.target.checked)} /> Regex
        </label>
        <label title={EVERYTHING_SYNTAX_HINT}>
          <input type="checkbox" checked={everythingMode} onChange={(e) => setEverythingMode(e.target.checked)} /> Everything
        </label>
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
          <fieldset>
            <legend>Not older than</legend>
            <label>
              <input
                type="checkbox"
                checked={notOlderThanEnable}
                onChange={(e) => setNotOlderThanEnable(e.target.checked)}
              />{" "}
              Within last
            </label>
            <input
              type="number"
              className={`age${notOlderThanInvalid ? " invalid" : ""}`}
              min={0}
              step={1}
              title={notOlderThanInvalid ? "Enter a whole number of 0 or more" : ""}
              value={notOlderThanText}
              onChange={(e) => setNotOlderThanText(e.target.value)}
            />
            <select value={notOlderThanUnit} onChange={(e) => setNotOlderThanUnit(e.target.value as AgeUnit)}>
              {AGE_UNITS.map((u) => (
                <option key={u.value} value={u.value}>{u.label}</option>
              ))}
            </select>
          </fieldset>
        </div>
      )}
    </div>
  );
}
