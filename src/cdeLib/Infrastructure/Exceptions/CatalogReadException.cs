using System;
using System.Runtime.Serialization;

namespace cdeLib.Infrastructure.Exceptions;

public class CatalogReadException : Exception
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

    public CatalogReadException(string message, Exception innerException) : base(message, innerException)
    {
    }
}