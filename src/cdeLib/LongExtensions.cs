using System;

namespace cdeLib
{
    public static class LongExtensions
    {
        /// <summary>
        /// Format a long as a string of GB/MB/KB
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string FormatAsBytes(this long bytes)
        {
            const int scale = 1024;
            var orders = new[] { "GB", "MB", "KB", "Bytes" };
            var max = (long)Math.Pow(scale, orders.Length - 1);

            foreach (var order in orders)
            {
                if (bytes > max)
                    return $"{decimal.Divide(bytes, max):##.##} {order}";

                max /= scale;
            }
            return "0 Bytes";
        }
    }
}