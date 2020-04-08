using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Util
{
    // TODO think about capturing start and end time.
	public class TimeIt
	{
		public struct LabelElapsed
		{
			public string Label;
			public float ElapsedMsec;
		}

		private string _runningLabel;
		private readonly Stopwatch _watch;
        private readonly List<LabelElapsed> _elapsedList;

        /// <summary>
        /// Start(label) and Stop() to collect list of elapsed times for things.
        /// </summary>
		public TimeIt()
		{
			_runningLabel = String.Empty;
			_watch = new Stopwatch();
			_elapsedList = new List<LabelElapsed>(20);
		}

		public void Start(string label)
		{
			if (string.IsNullOrEmpty(_runningLabel))
			{
				_runningLabel = label;
				_watch.Reset();
				_watch.Start();
				return;
			}
			throw new Exception("TimeIt in started state all ready.");
		}

		public LabelElapsed Stop()
		{
			if (!string.IsNullOrEmpty(_runningLabel))
			{
				_watch.Stop();
				var nameElapsed = new LabelElapsed
				{
					Label = _runningLabel,
					ElapsedMsec = _watch.ElapsedMilliseconds
				};
				_runningLabel = String.Empty;
				_elapsedList.Add(nameElapsed);
				return nameElapsed;
			}
			throw new Exception("TimeIt not in started state.");
		}

		public IEnumerable<LabelElapsed> ElapsedList => _elapsedList;

        public float TotalMsec
	    {
            get { return _elapsedList.Sum(x => x.ElapsedMsec); }
	    }

	    public new string ToString()
		{
			var str = _elapsedList
				.Aggregate(string.Empty,
					(current, labelElapsed) =>
					    $"{current},\"{labelElapsed.Label}\",{labelElapsed.ElapsedMsec}");
			return str.Substring(1) + ",Total," + TotalMsec;
		}
	}
}
