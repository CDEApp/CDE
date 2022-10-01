using System.Windows.Forms;

namespace cdeWin;

public static class SplitContainerExtensions
{
    public static float GetSplitterRatio(this SplitContainer splitter)
    {
        return (float)splitter.SplitterDistance / splitter.GetSplitterSize();
    }

    public static void SetSplitterRatio(this SplitContainer splitter, float splitterRatio)
    {
        var currentSize = splitter.GetSplitterSize();
        var newDistance = (int)(currentSize * splitterRatio);
        if (newDistance >= splitter.Panel1MinSize   // Set splitter if Valid splitter distance
            && newDistance <= (currentSize - splitter.Panel2MinSize))
        {
            splitter.SplitterDistance = newDistance;
        }
    }

    public static int GetSplitterSize(this SplitContainer splitter)
    {
        return splitter.Orientation == Orientation.Vertical
            ? splitter.Width
            : splitter.Height;
    }
}