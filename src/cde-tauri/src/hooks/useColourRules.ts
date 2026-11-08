import { useCallback, useMemo, useState } from "react";
import type { CdeApiClient } from "../api/client";
import { createRuleResolver, parseColourRules } from "../colourRules";
import { createSerialSaver } from "../serialSaver";

export interface UseColourRulesOptions {
  getClient: () => CdeApiClient;
}

/** The colour-rules text (file-backed through the sidecar), its ordered saves, and the name resolvers built from it. */
export function useColourRules({ getClient }: UseColourRulesOptions) {
  const [text, setText] = useState("");
  // Saves live here, not in ColoursView, so a save flushed as the tab closes still runs in order
  // and its error is still shown when the tab reopens.
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saver] = useState(() => createSerialSaver((t) => getClient().putColourRules(t), setSaveError));

  // Parsed + compiled once per change; the resolvers (with their per-name caches) are rebuilt with the text.
  const rules = useMemo(() => parseColourRules(text).rules, [text]);
  const resolveStyle = useMemo(() => createRuleResolver(rules), [rules]);
  const resolveTreeStyle = useMemo(() => createRuleResolver(rules, { skipFolder: true }), [rules]);

  // Re-read the rules file (picks up hand edits). Wait for pending saves first, or a save flushed as the
  // Customize tab last closed could land after this read. A failure keeps the current text.
  const load = useCallback(async () => {
    try {
      await saver.idle();
      setText(await getClient().getColourRules());
    } catch (e) {
      console.error("colour rules failed to load", e);
    }
  }, [saver, getClient]);

  const save = useCallback((t: string) => saver.save(t), [saver]);
  const getDefaults = useCallback(() => getClient().getColourRuleDefaults(), [getClient]);

  return { text, setText, saveError, save, load, getDefaults, resolveStyle, resolveTreeStyle };
}
