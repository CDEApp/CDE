using MediatR;

namespace cdeLib.Cache
{
    public class CreateCacheCommand : IRequest
    {
        public CreateCacheCommand(string path)
        {
            Path = path;
        }

        public string Path { get; }
    }
}