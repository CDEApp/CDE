import type { JobDto } from "../api/types";
import { isActive, progressNote, startedLabel, statusLabel } from "../jobsModel";

interface Props {
  sortedJobs: readonly JobDto[];
  onCancel: (id: number) => void;
  onDismiss: (id: number) => void;
  onClearFinished: () => void;
}

export function JobsView({ sortedJobs: jobs, onCancel, onDismiss, onClearFinished }: Props) {
  const anyFinished = jobs.some((j) => !isActive(j.state));
  return (
    <div className="jobs-view">
      <div className="cataloglist-toolbar">
        <button disabled={!anyFinished} onClick={onClearFinished}>
          Clear finished
        </button>
      </div>
      {jobs.length === 0 ? (
        <div className="cataloglist-empty">No jobs. Rescans, new catalogs and hash runs show up here.</div>
      ) : (
        <table className="jobs-table">
          <thead>
            <tr>
              <th>Task</th>
              <th>Started</th>
              <th>Progress</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {jobs.map((j) => (
              <tr key={j.id} className={`job-${j.state}`}>
                <td title={j.title}>{j.title}</td>
                <td>{startedLabel(j)}</td>
                <td title={progressNote(j)}>{progressNote(j)}</td>
                <td>{statusLabel(j.state)}</td>
                <td>
                  {isActive(j.state) ? (
                    <button onClick={() => onCancel(j.id)} title="Cancel this job; nothing is changed or left behind">
                      Cancel
                    </button>
                  ) : (
                    <button onClick={() => onDismiss(j.id)} title="Remove this job from the list">
                      Dismiss
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
