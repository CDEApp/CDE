namespace cdeWin;

public class ComboBoxItem<TValue>
{
    public string Text { get; private set; }
    public TValue Value { get; private set; }

    public ComboBoxItem(string text, TValue value)
    {
        Text = text;
        Value = value;
    }

    public override string ToString()
    {
        return Text;
    }
}