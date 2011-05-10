using System.Diagnostics;

namespace cdeLib.Infrastructure
{
    public interface IApplicationDiagnostics
    {
        long GetMemoryAllocated();
    }

    public class ApplicationDiagnostics : IApplicationDiagnostics
    {
        private Process _process;
        public ApplicationDiagnostics()
        {
        }

        public long GetMemoryAllocated()
        {
            _process = Process.GetCurrentProcess();
            return _process.PrivateMemorySize64;
        }
    }
}