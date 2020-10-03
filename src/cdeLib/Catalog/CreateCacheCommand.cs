using MediatR;

namespace cdeLib.Catalog
{
    public class CreateCacheCommand : IRequest
    {
        public CreateCacheCommand(string path)
        {
            Path = path;
        }

        public string Path { get; }
        public string Description { get; set; }
    }
}