using System.Globalization;

namespace cdeLib
{
	public interface IConfigCdeLib
	{
		CompareInfo MyCompareInfo { get; }
		CompareOptions MyCompareOptions { get; }
		int CompareWithInfo(string s1, string s2);
	}
}