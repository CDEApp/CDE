using System.Runtime.Serialization;

namespace cdeDataStructure3.Exceptions
{
    public class CatalogReadException : System.Exception
    {
        public CatalogReadException()
        {
        }

        protected CatalogReadException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public CatalogReadException(string message) : base(message)
        {
        }

        public CatalogReadException(string message, System.Exception innerException) : base(message, innerException)
        {
        }
    }
}