using System;

namespace cdeLib.Infrastructure.Exceptions;

public class CatalogReadException : Exception
{
    public CatalogReadException()
    {
    }

    public CatalogReadException(string message) : base(message)
    {
    }

    public CatalogReadException(string message, Exception innerException) : base(message, innerException)
    {
    }
}