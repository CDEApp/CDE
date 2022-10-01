namespace cdeLib.Infrastructure.Hashing;

public class HashResponse
{
    public byte[] Hash { get; set; }
    public bool IsPartialHash { get; set; }
    public long BytesHashed { get; set; }
}