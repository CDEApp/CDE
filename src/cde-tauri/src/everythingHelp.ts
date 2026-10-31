/** One line of the Search Syntax help: what to type and what it does. */
export interface HelpRow {
  syntax: string;
  meaning: string;
}

export interface HelpSection {
  title: string;
  rows: HelpRow[];
}

/** The text of Help > Search Syntax: the Everything-style search operators, modifiers and filters. */
export const EVERYTHING_HELP: HelpSection[] = [
  {
    title: "Operators",
    rows: [
      { syntax: "a b", meaning: "space is AND: the name has both a and b" },
      { syntax: "a | b", meaning: "OR: the name has a or b (a space binds tighter than |)" },
      { syntax: "!a", meaning: "NOT: the name does not have a" },
      { syntax: "<a | b> c", meaning: "group with < >: the name has a or b, and also c" },
      { syntax: "\"a b\"", meaning: "phrase: the exact text a b, and operators inside the quotes are plain text" },
    ],
  },
  {
    title: "Modifiers",
    rows: [
      { syntax: "path:term", meaning: "match the full path, not just the name. Directly in front of one word, phrase or group, with no space" },
      { syntax: "no-path:term", meaning: "match the name only" },
      { syntax: "path:<a | b>", meaning: "a modifier can go in front of a group; the innermost modifier wins" },
      { syntax: "!path:term", meaning: "put ! in front of the modifier to negate it" },
    ],
  },
  {
    title: "Filters",
    rows: [
      { syntax: "file:", meaning: "files only" },
      { syntax: "folder:", meaning: "folders only" },
      { syntax: "!file:", meaning: "NOT a file, so folders" },
      { syntax: "<file: a> | <folder: b>", meaning: "files with a, or folders with b" },
    ],
  },
  {
    title: "Examples",
    rows: [
      { syntax: "do win", meaning: "matches Windows (both do and win are in the name)" },
      { syntax: "folder:music", meaning: "folders whose name has music" },
      { syntax: "file: .mp3", meaning: "files whose name has .mp3" },
      { syntax: "path:system32 hosts", meaning: "hosts in a path that has system32" },
      { syntax: "txt | log !tmp", meaning: "names with txt, or with log but not tmp" },
    ],
  },
];

/** Closing notes for the Search Syntax help. */
export const EVERYTHING_HELP_NOT_SUPPORTED: string[] = [
  "Terms are case-insensitive substrings of the name.",
  "Include path and the Files/Folders choice are ignored in this mode: use path:, file: and folder:.",
  "Not supported yet: wildcards, ext: and other functions, the \: alias and the global ?path: form.",
];
