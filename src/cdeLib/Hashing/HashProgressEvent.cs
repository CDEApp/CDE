namespace cdeLib.Hashing;

public record HashProgressEvent(long FilesProcessed, long FilesToHash, string Phase);
