using System;
using System.Collections.Generic;
using System.Linq;
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
            // every Option except "<>" needs to set _currentlist to its own list, or null.
            // to improve handling of non matching parameters.
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

                // Modes below here in this section
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
                        _currentList = null;
                    }
                },
                {
                    "dupes", "Mode: find duplicate files, depends on hashes", o => {
                        Mode = Modes.Dupes;
                        _currentList = null;
                    }
                },
                {
                    "dump", "Mode: output to console catalog entries", o => {
                        Mode = Modes.Dump;
                        _currentList = null;
                    }
                },
                {
                    "loadWait", "Mode: load catalogs and wait till enter pressed", o => {
                        Mode = Modes.LoadWait;
                        _currentList = null;
                    }
                },
                {
                    "h|help",  "Mode: show this message and exit", o => {
                        Mode = Modes.Help;
                        _currentList = null;
                    }
                },
                {
                    "v|version",  "Mode: show version", o => {
                        Mode = Modes.Version;
                        _currentList = null;
                    }
                },

                // Options below here in this section
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
                        _currentList = null;
                    }
                },
                {
                    "repl", "Enable prompting for more find searches.", o => {
                        if (!AllowRepl.Contains(_mode))
                        {
                            throw new OptionException("The -repl option is not supported in mode '-" + _mode.ToString().ToLower() + "'.", o);
                        }
                        _replEnabled = o != null;
                        _currentList = null;
                    }
                },
                {
                    "path", "Include paths when searching in find.", o => {
                        if (!AllowPath.Contains(_mode))
                        {
                            throw new OptionException("The -path option is not supported in mode '-" + _mode.ToString().ToLower() + "'.", o);
                        }
                        _pathEnabled = o != null;
                        _currentList = null;
                    }
                },
                {
                    "hashAll", "In hash mode hash all files", o => {
                        if (!AllowHashAll.Contains(_mode))
                        {
                            throw new OptionException("The -hashAll option is not supported in mode '-" + _mode.ToString().ToLower() + "'.", o);
                        }
                        _hashAllEnabled = o != null;
                        _currentList = null;
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
                {
                    "minSize=", "Minimum file size to include in processing", o => {
                        if (!AllowMinSize.Contains(_mode))
                        {
                            throw new OptionException("The -minSize option is not supported in mode '-" + _mode.ToString().ToLower() + "'.", o);
                        }
                        _minSize = SizeOption(o);
                        _currentList = null;
                    }
                },
                {
                    "maxSize=", "Maximum file size to include in processing", o => {
                        if (!AllowMaxSize.Contains(_mode))
                        {
                            throw new OptionException("The -maxSize option is not supported in mode '-" + _mode.ToString().ToLower() + "'.", o);
                        }
                        _maxSize = SizeOption(o);
                        _currentList = null;
                    }
                },
                {   // unsure if leaving this here for releases
                    "alternate", "an alternate data model (testing)", o => {
                        if (!AllowAlternate.Contains(_mode))
                        {
                            throw new OptionException("The -alt option is not supported in mode '-" + _mode.ToString().ToLower() + "'.", o);
                        }
                        _alternate = o != null;
                        _currentList = null;
                    }
                },
                // to collection multi value parameter values.
                {   
                    "<>", "", o => {
                        if (_currentList == null)
                        {
                            throw new OptionException("Error unmatched parameter: '" + o + "'", o);
                        }
                        _currentList.Add(o);
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

        public long SizeOption(string o)
        {
            var suffix = new List<string> {"KB", "MB", "GB"};
            var foundIndex = suffix.FindIndex(c => o.EndsWith(c));
            var multiplier = 1L;
            if (foundIndex >= 0)
            {
                o = o.Substring(0, o.Length - 2);
                multiplier = (long)Math.Pow(1000, (foundIndex + 1));
            }
            return multiplier * long.Parse(o);
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

        private bool _alternate;
        public bool Alternate { get { return _alternate; } }

        private long _minSize;
        public long MinSize { get { return _minSize; } }

        private long _maxSize;
        public long MaxSize { get { return _maxSize; } }

        //private DateTime _minDate;
        //public DateTime MinDate { get { return _minDate; } }

        //private DateTime _maxDate;
        //public DateTime MaxDate { get { return _maxDate; } }

        //private DateTime _minTime;
        //public DateTime MinTime { get { return _minTime; } }

        //private DateTime _maxTime;
        //public DateTime MaxTime { get { return _maxTime; } }

        /// <summary>
        /// Minimum age of a file in hours to find it, or hash it, or ?
        /// </summary>
        //public int MinHours { get { return _minHours; } }
        //private int _minHours;


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
        private static readonly ICollection<Modes> AllowPath = AllowGrep;

        /// <summary>
        /// Modes which allow repl parameter
        /// </summary>
        private static readonly ICollection<Modes> AllowRepl = AllowGrep;

        /// <summary>
        /// Modes which allow hashAll parameter
        /// </summary>
        private static readonly ICollection<Modes> AllowHashAll = new HashSet<Modes>
        {
            {Modes.Hash},
        };

        private static readonly ICollection<Modes> ScanHashDupesFind = new HashSet<Modes>
        {
            {Modes.Scan},
            {Modes.Hash},
            {Modes.Dupes},
            {Modes.Find},
        };

        /// <summary>
        /// Modes which allow exclude parameter
        /// </summary>
        private static readonly ICollection<Modes> AllowExclude = ScanHashDupesFind;

        /// <summary>
        /// Modes which allow include parameter
        /// </summary>
        private static readonly ICollection<Modes> AllowInclude = ScanHashDupesFind;

        /// <summary>
        /// Modes which allow alt parameter
        /// </summary>
        private static readonly ICollection<Modes> AllowAlternate = ScanHashDupesFind;
        /// <summary>
        /// Modes which allow minSize parameter
        /// </summary>
        private static readonly ICollection<Modes> AllowMinSize = ScanHashDupesFind;

        /// <summary>
        /// Modes which allow maxSize parameter
        /// </summary>
        private static readonly ICollection<Modes> AllowMaxSize = ScanHashDupesFind;

        /// <summary>
        /// Modes which allow minDate parameter
        /// </summary>
        private static readonly ICollection<Modes> AllowMinDate = ScanHashDupesFind;

        /// <summary>
        /// Modes which allow maxDate parameter
        /// </summary>
        private static readonly ICollection<Modes> AllowMaxDate = ScanHashDupesFind;

        /// <summary>
        /// Modes which allow minTime parameter
        /// </summary>
        private static readonly ICollection<Modes> AllowMinTime = ScanHashDupesFind;

        /// <summary>
        /// Modes which allow maxTime parameter
        /// </summary>
        private static readonly ICollection<Modes> AllowMaxTime = ScanHashDupesFind;

        /// <summary>
        /// Modes which allow minHours parameter
        /// </summary>
        private static readonly ICollection<Modes> AllowMinHours = ScanHashDupesFind;


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