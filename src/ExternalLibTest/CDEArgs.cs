using System;
using System.Collections.Generic;
using NDesk.Options;

namespace ExternalLibTest
{
    //public class CDEArgsException : ArgumentException { }

    public class CDEArgs
    {
        public enum Modes
        {
            None,
            Scan,
            Hash,
            Dupes,
            Find,
            Dump,
            Version,
            Help,
            LoadWait,
        };

        private readonly OptionSet _os;
        private List<string> _currentList;

        public CDEArgs()
        {
            _os = new OptionSet
            {
                //{
                //    "config=", "Load parameters from {File}(s)", o => {
                //        // import the config file.
                //        // # start of line is comment ignore.
                //        // new lines are considered same as white space for parameter identification.
                //        // extra white space is ignored at start or end of lines.
                //    }
                //},

                // Modes below here
                {
                    "scan=", "Mode: scans one or more {Path}(s) creating catalogs", o => {
                        Mode = Modes.Scan;
                        _currentList = _scanParameters;
                        _scanParameters.Add(o);
                    }
                },
                {
                    "find=", "Mode: find entries matching {String}(s)", o => {
                        Mode = Modes.Find;
                        _currentList = _findParameters;
                        _findParameters.Add(o);
                    }
                },
                {
                    "hash", "Mode: collect minimal set of hashes for dupes", o => {
                        Mode = Modes.Hash;
                    }
                },
                {
                    "dupes", "Mode: find duplicate files, requires hashes done", o => {
                        Mode = Modes.Dupes;
                    }
                },
                {
                    "dump", "Mode: output to console catalog entries", o => {
                        Mode = Modes.Dump;
                    }
                },
                {
                    "loadWait", "Mode: load catalogs and wait till enter pressed", o => {
                        Mode = Modes.LoadWait;
                    }
                },
                {
                    "h|help",  "Mode: show this message and exit", o => {
                        Mode = Modes.Help;
                    }
                },
                {
                    "v|version",  "Mode: show version", o => {
                        Mode = Modes.Version;
                    }
                },

                // Options below here
                {
                    "bp|basePath=", "Set one or more base {Path}(s)", o => {
                        if (!AllowStartPath.Contains(_mode))
                        {
                            throw new OptionException("The -basePath option is not supported in mode '-" + _mode.ToString().ToLower() + "'.", o);
                        }
                        _currentList = _basePaths;
                        _currentList.Add(o);
                    }
                },
                {
                    "grep", "Enable regular expressions for String find.", o => {
                        if (!AllowGrep.Contains(_mode))
                        {
                            throw new OptionException("The -grep option is not supported in mode '-" + _mode.ToString().ToLower() + "'.", o);
                        }
                        _grepEnabled = o != null;
                    }
                },
                {
                    "repl", "Enable repl prompting find.", o => {
                        if (!AllowRepl.Contains(_mode))
                        {
                            throw new OptionException("The -repl option is not supported in mode '-" + _mode.ToString().ToLower() + "'.", o);
                        }
                        _replEnabled = o != null;
                    }
                },
                {
                    "path", "Include paths when searching in find.", o => {
                        if (!AllowPath.Contains(_mode))
                        {
                            throw new OptionException("The -path option is not supported in mode '-" + _mode.ToString().ToLower() + "'.", o);
                        }
                        _pathEnabled = o != null;
                    }
                },
                {
                    "hashAll", "In hash mode hash all files", o => {
                        if (!AllowHashAll.Contains(_mode))
                        {
                            throw new OptionException("The -hashAll option is not supported in mode '-" + _mode.ToString().ToLower() + "'.", o);
                        }
                        _hashAllEnabled = o != null;
                    }
                },
                {
                    "e|exclude=", "regex {String}(s) to exclude from processing", o => {
                        if (!AllowExclude.Contains(_mode))
                        {
                            throw new OptionException("The -exclude option is not supported in mode '-" + _mode.ToString().ToLower() + "'.", o);
                        }
                        _currentList = _exclude;
                        _currentList.Add(o);
                    }
                },
                {
                    "i|include=", "regex {String}(s) to include for processing", o => {
                        if (!AllowInclude.Contains(_mode))
                        {
                            throw new OptionException("The -include option is not supported in mode '-" + _mode.ToString().ToLower() + "'.", o);
                        }
                        _currentList = _include;
                        _currentList.Add(o);
                    }
                },

                // to collection multi value parameter values.
                {   
                    "<>", "", o => {
                        if (_currentList != null)
                        {
                            _currentList.Add(o);
                        }
                        else
                        {
                            throw new OptionException("Error unmatched parameter: '" + o + "'", o);
                        }
                    }
                },
            };
        }

        public CDEArgs(IEnumerable<string> args) : this()
        {
            // exception in constructor bad
            try
            {
                // using "<>" in OptionSet, there for unmatched parameters never returned from Parse()
                _os.Parse(args);
            }
            catch (OptionException e)
            {
                _error = e.Message;
            }
        }

        private Modes _mode;
        public Modes Mode
        {
            get { return _mode; }
            private set
            {
                if (_mode != Modes.None)
                {
                    throw new OptionException("Only one Mode argument is allowed '-" + value.ToString().ToLower() + "'.", value.ToString());
                }
                _mode = value;
            }
        }

        private readonly string _error;
        public string Error { get { return _error; } }

        private readonly List<string> _scanParameters = new List<string>();
        public List<string> ScanParameters { get { return _scanParameters; } }

        private readonly List<string> _findParameters = new List<string>();
        public List<string> FindParameters { get { return _findParameters; } }

        private readonly string _find = string.Empty;
        public string Find { get { return _find; } }

        private readonly List<string> _basePaths = new List<string>();
        public List<string> BasePaths { get { return _basePaths; } }

        private bool _grepEnabled;
        public bool GrepEnabled { get { return _grepEnabled; } }

        private bool _replEnabled;
        public bool ReplEnabled { get { return _replEnabled; } }

        private bool _pathEnabled;
        public bool PathEnabled { get { return _pathEnabled; } }

        private bool _hashAllEnabled;
        public bool HashAllEnabled { get { return _hashAllEnabled; } }

        private readonly List<string> _exclude = new List<string>();
        public List<string> Exclude { get { return _exclude; } }

        private readonly List<string> _include = new List<string>();
        public List<string> Include { get { return _include; } }

        //private long _minSize;
        //public long MinSize { get { return _minSize; } }

        //private long _maxSize;
        //public long MaxSize { get { return _maxSize; } }

        //private DateTime _minDate;
        //public DateTime MinDate { get { return _minDate; } }

        //private DateTime _maxDate;
        //public DateTime MaxDate { get { return _maxDate; } }

        /// <summary>
        /// Minimum age of a file in hours to find it, or hash it, or ?
        /// </summary>
        //public int MinHourAge { get { return _minHourAge; } }
        //private int _minHourAge;


        /// <summary>
        /// Modes which allow startPath parameter
        /// </summary>
        private static readonly ICollection<Modes> AllowStartPath = new HashSet<Modes>
        {
            {Modes.Hash},
            {Modes.Dupes},
            {Modes.Find},
        };

        /// <summary>
        /// Modes which allow grep parameter
        /// </summary>
        private static readonly ICollection<Modes> AllowGrep = new HashSet<Modes>
        {
            {Modes.Find},
        };

        /// <summary>
        /// Modes which allow path parameter
        /// </summary>
        private static readonly ICollection<Modes> AllowPath = new HashSet<Modes>
        {
            {Modes.Find},
        };

        /// <summary>
        /// Modes which allow repl parameter
        /// </summary>
        private static readonly ICollection<Modes> AllowRepl = new HashSet<Modes>
        {
            {Modes.Find},
        };

        /// <summary>
        /// Modes which allow hashAll parameter
        /// </summary>
        private static readonly ICollection<Modes> AllowHashAll = new HashSet<Modes>
        {
            {Modes.Hash},
        };

        /// <summary>
        /// Modes which allow exclude parameter
        /// </summary>
        private static readonly ICollection<Modes> AllowExclude = new HashSet<Modes>
        {
            {Modes.Scan},
            {Modes.Hash},
            {Modes.Dupes},
            {Modes.Find},
        };

        /// <summary>
        /// Modes which allow include parameter
        /// </summary>
        private static readonly ICollection<Modes> AllowInclude = new HashSet<Modes>
        {
            {Modes.Scan},
            {Modes.Hash},
            {Modes.Dupes},
            {Modes.Find},
        };

        public void ShowHelpX()
        {
            Console.WriteLine("Usage: cde [OPTIONS]+");
            Console.WriteLine("");
            Console.WriteLine();
            Console.WriteLine("Options:");
            _os.WriteOptionDescriptions(Console.Out);
        }
    }

}