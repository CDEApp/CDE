using System;
using cdeLib.Entities;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace cdeLib.Infrastructure.Serialization;

/// <summary>
/// Custom MessagePack resolver that uses Hash16Formatter for Hash16 struct
/// and falls back to standard resolver for everything else.
/// </summary>
public sealed class CustomMessagePackResolver : IFormatterResolver
{
    // Singleton instance
    public static readonly CustomMessagePackResolver Instance = new();

    private CustomMessagePackResolver()
    {
    }

    public IMessagePackFormatter<T> GetFormatter<T>()
    {
        return FormatterCache<T>.Formatter;
    }

    private static class FormatterCache<T>
    {
        public static readonly IMessagePackFormatter<T> Formatter;

        static FormatterCache()
        {
            // Use custom formatter for Hash16
            if (typeof(T) == typeof(Hash16))
            {
                Formatter = (IMessagePackFormatter<T>)(object)new Hash16Formatter();
            }
            else
            {
                // Fall back to standard resolver for all other types
                Formatter = StandardResolverAllowPrivate.Instance.GetFormatter<T>();
            }
        }
    }
}

/// <summary>
/// Helper class to get MessagePackSerializerOptions with custom resolver.
/// </summary>
public static class MessagePackConfig
{
    private static readonly MessagePackSerializerOptions _options = MessagePackSerializerOptions.Standard
        .WithResolver(CompositeResolver.Create(
            CustomMessagePackResolver.Instance,
            StandardResolverAllowPrivate.Instance
        ));

    public static MessagePackSerializerOptions Options => _options;
}
