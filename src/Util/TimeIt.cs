using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Util
{
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
			throw new Exception("TimeIt in started state allready.");
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
				TotalMsec += _watch.ElapsedMilliseconds;
				_runningLabel = String.Empty;
				_elapsedList.Add(nameElapsed);
				return nameElapsed;
			}
			throw new Exception("TimeIt not in started state.");
		}

		public IEnumerable<LabelElapsed> ElapsedList { get { return _elapsedList; } }
		public float TotalMsec { get; private set; }

		public new string ToString()
		{
			var str = _elapsedList
				.Aggregate(string.Empty,
					(current, labelElapsed) =>
						String.Format("{0},\"{1}\",{2}", current, labelElapsed.Label, labelElapsed.ElapsedMsec));
			return str.Substring(1) + ",Total," + TotalMsec;
		}
	}

}
