//using System;
//using System.Collections;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//
//namespace cdeLib.Infrastructure
//{
//    public class ContextPartitioner : Partitioner<FlatDirEntryDTO>
//    {
        //set of dataitems.
//        protected FlatDirEntryDTO[] dataItems;
        //target sum of values per chunk.
//        protected int targetSum;
        //object used to create enumerators
//        private EnumerableSource enumSource;
        //first unchunked item
//        private long sharedStartIndex = 0;
        //lock object ot avoid index data recs.
//        private object lockObj = new object();
//
//        public ContextPartitioner(FlatDirEntryDTO[] data, int target)
//        {
//            this.dataItems = data;
//            targetSum = target;
//            enumSource = new EnumerableSource(this);
//        }
//
//        public override bool SupportsDynamicPartitions
//        {
//            get
//            {
                // dynamic partitions are required for
                // parallel foreach loops
//                return true;
//            }
//        }
//
//        public override IList<IEnumerator<FlatDirEntryDTO>> GetPartitions(int partitionCount)
//        {
//            IList<IEnumerator<FlatDirEntryDTO>> partitionsList = new List<IEnumerator<FlatDirEntryDTO>>();
            // get the IEnumerable that will generate dynamic partitions
//            IEnumerable<FlatDirEntryDTO> enumObj = GetDynamicPartitions();
            // create the required number of partitions
//            for (int i = 0; i < partitionCount; i++)
//            {
//                partitionsList.Add(enumObj.GetEnumerator());
//            }
            // return the result
//            return partitionsList;
//        }
//
//        private Tuple<long, long> getNextChunk()
//        {
            // create the result tuple
//            Tuple<long, long> result;
            // get an exclusive lock as we perform this operation
//            lock (lockObj)
//            {
                // check that there is still data available
//                if (sharedStartIndex < dataItems.Length)
//                {
//                    int sum = 0;
//                    long endIndex = sharedStartIndex;
//                    while (endIndex < dataItems.Length && sum < targetSum)
//                    {
//                        sum += dataItems[endIndex].WorkDuration;
//                        endIndex++;
//                    }
//                    result = new Tuple<long, long>(sharedStartIndex, endIndex);
//                    sharedStartIndex = endIndex;
//                }
//                else
//                {
                    // there is no data available
//                    result = new Tuple<long, long>(-1, -1);
//                }
//            }
            // end of locked region
            // return the result
//            return result;
//        }
//
//        public override IEnumerable<FlatDirEntryDTO> GetDynamicPartitions()
//        {
//            return enumSource;
//        }
//
//        #region Nested type: ChunkEnumerator
//
//        private class ChunkEnumerator
//        {
//            private ContextPartitioner parentPartitioner;
//
//            public ChunkEnumerator(ContextPartitioner parent)
//            {
//                parentPartitioner = parent;
//            }
//
//            public IEnumerator<FlatDirEntryDTO> GetEnumerator()
//            {
//                while (true)
//                {
                    // get the indices of the next chunk
//                    Tuple<long, long> chunkIndices = parentPartitioner.getNextChunk();
                    // check that we have data to deliver
//                    if (chunkIndices.Item1 == -1 && chunkIndices.Item2 == -1)
//                    {
                        // there is no more data
//                        break;
//                    }
//                    else
//                    {
                        // enter a loop to yield the data items
//                        for (long i = chunkIndices.Item1; i < chunkIndices.Item2; i++)
//                        {
//                            yield return parentPartitioner.dataItems[i];
//                        }
//                    }
//                }
//            }
//        }
//
//        #endregion
//
//        #region Nested type: EnumerableSource
//
//        protected class EnumerableSource : IEnumerable<FlatDirEntryDTO>
//        {
//            private ContextPartitioner parentPartitioner;
//
//            public EnumerableSource(ContextPartitioner parent)
//            {
//                parentPartitioner = parent;
//            }
//
//            #region IEnumerable<FlatDirEntryDTO> Members
//
//            IEnumerator IEnumerable.GetEnumerator()
//            {
//                return ((IEnumerable<FlatDirEntryDTO>) this).GetEnumerator();
//            }
//
//            IEnumerator<FlatDirEntryDTO> IEnumerable<FlatDirEntryDTO>.GetEnumerator()
//            {
//                return new ChunkEnumerator(parentPartitioner).GetEnumerator();
//            }
//
//            #endregion
//        }
//
//        #endregion
//    }
//}