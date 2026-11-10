import { useEffect, useState } from "react";

/** The current time, refreshed every `intervalMs`, so derived ages and staleness stay current in an open window. */
export function useNow(intervalMs: number): Date {
  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), intervalMs);
    return () => clearInterval(id);
  }, [intervalMs]);
  return now;
}
