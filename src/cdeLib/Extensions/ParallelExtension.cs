using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace cdeLib;

public static class ParallelExtension
{
    public static void ForEachInApproximateOrder<TSource>(this ParallelQuery<TSource> source, ParallelOptions options, Action<TSource, ParallelLoopState> action)
    {
        source = Partitioner.Create(source)
            .AsParallel()
            .AsOrdered();
        Parallel.ForEach(source, options, action);
    }
}