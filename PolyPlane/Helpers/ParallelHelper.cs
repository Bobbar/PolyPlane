namespace PolyPlane.Helpers
{
    public static class ParallelHelpers
    {
        public static void ForEachParallel<T>(this List<T> list, Action<T> action)
        {
            ParallelForSlim(list.Count, (start, end) =>
            {
                for (int i = start; i < end; i++)
                {
                    action(list[i]);
                }
            });
        }

        public static void ParallelForSlim(int count, Action<int, int> body)
        {
            int pLen, pRem, pCount;
            Partition(count, World.MUTLI_THREAD_COUNT, out pLen, out pRem, out pCount);

            Parallel.For(0, pCount, (p) =>
            {
                int offset = p * pLen;
                int len = offset + pLen;

                if (p == pCount - 1)
                    len += pRem;

                body(offset, len);
            });
        }


        /// <summary>
        /// Computes parameters for partitioning the specified length into the specified number of parts.
        /// </summary>
        /// <param name="length">Total number of items to be partitioned.</param>
        /// <param name="parts">Number of partitions to compute.</param>
        /// <param name="partLen">Computed length of each part.</param>
        /// <param name="modulo">Computed modulo or remainder to be added to the last partitions length.</param>
        /// <param name="count">Computed number of partitions. If parts is greater than length, this will be 1.</param>
        public static void Partition(int length, int parts, out int partLen, out int modulo, out int count)
        {
            int outpLen, outMod;

            outpLen = length / parts;
            outMod = length % parts;

            if (parts >= length || outpLen <= 1)
            {
                partLen = length;
                modulo = 0;
                count = 1;
            }
            else
            {
                partLen = outpLen;
                modulo = outMod;
                count = parts;
            }
        }
    }
}
