using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MiniTrayMon
{
    /// <summary>
    /// Snapshot of system resource metrics, captured atomically each poll.
    /// </summary>
    public sealed class SystemStats
    {
        public float CpuPercent;          // 0..100
        public float RamPercent;           // 0..100
        public float RamUsedGB;            // GB used
        public float RamTotalGB;          // GB total physical
        public float RamFreeGB;            // GB available
        public float DiskPercent;          // 0..100 (active time approx)
        public float DiskReadKBps;        // KB/s
        public float DiskWriteKBps;       // KB/s
        public float DiskTotalMbps;       // total read+write in Mbps
        public float NetUpMbps;           // upload Mbps
        public float NetDownMbps;         // download Mbps
        public DateTime Timestamp;

        /// <summary>
        /// Overall combined "system load" 0..100. Weighted blend similar to
        /// what a user perceives as overall pressure. Used by "overall" mode.
        /// </summary>
        public float OverallPercent
        {
            get
            {
                // Weights: CPU 35%, RAM 35%, Disk 15%, Network 15%
                float netLoad = Math.Min(100, (NetDownMbps + NetUpMbps) * 2f); // ~50 Mbps = saturated
                float diskLoad = Math.Min(100, DiskPercent);
                float v = CpuPercent * 0.35f + RamPercent * 0.35f + diskLoad * 0.15f + netLoad * 0.15f;
                return Math.Max(0, Math.Min(100, v));
            }
        }
    }

    /// <summary>
    /// Collects performance counters and produces SystemStats snapshots.
    /// IDisposable; call Next() on each poll tick.
    /// </summary>
    public sealed class StatsCollector : IDisposable
    {
        private PerformanceCounter _cpu;
        private PerformanceCounter _diskRead;
        private PerformanceCounter _diskWrite;
        private PerformanceCounter _diskTime;
        private PerformanceCounter _netSent;
        private PerformanceCounter _netRecv;
        private ulong _ramTotalMB;
        private bool _disposed;

        [StructLayout(LayoutKind.Sequential)]
        private struct PERFORMANCE_INFORMATION
        {
            public uint cb;
            // SIZE_T fields are pointer-sized (8 bytes on x64) — must use IntPtr
            public IntPtr CommitTotal;
            public IntPtr CommitLimit;
            public IntPtr CommitPeak;
            public IntPtr PhysicalTotal;
            public IntPtr PhysicalAvailable;
            public IntPtr SystemCache;
            public IntPtr KernelTotal;
            public IntPtr KernelPaged;
            public IntPtr KernelNonpaged;
            public IntPtr PageSize;
            public uint HandleCount;
            public uint ProcessCount;
            public uint ThreadCount;
        }
        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetPerformanceInfo(out PERFORMANCE_INFORMATION pinfo, uint cb);

        public ulong RamTotalMB { get { return _ramTotalMB; } }

        public StatsCollector()
        {
            // CPU
            try
            {
                _cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                _cpu.NextValue(); // first sample is 0; discard
            }
            catch { _cpu = null; }

            // Disk
            try
            {
                _diskRead = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total", true);
                _diskWrite = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total", true);
                _diskRead.NextValue(); _diskWrite.NextValue();
            }
            catch { _diskRead = null; _diskWrite = null; }

            // Disk active time (%) — most accurate for "disk busy"
            try
            {
                _diskTime = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total", true);
                _diskTime.NextValue();
            }
            catch { _diskTime = null; }

            // Network
            try
            {
                string inst = GetFirstNetworkInstance();
                if (!string.IsNullOrEmpty(inst))
                {
                    _netSent = new PerformanceCounter("Network Interface", "Bytes Sent/sec", inst, true);
                    _netRecv = new PerformanceCounter("Network Interface", "Bytes Received/sec", inst, true);
                    _netSent.NextValue(); _netRecv.NextValue();
                }
            }
            catch { _netSent = null; _netRecv = null; }

            // RAM total
            PERFORMANCE_INFORMATION pi;
            if (GetPerformanceInfo(out pi, (uint)Marshal.SizeOf(typeof(PERFORMANCE_INFORMATION))))
            {
                ulong pageSize = (ulong)pi.PageSize.ToInt64();
                _ramTotalMB = (ulong)pi.PhysicalTotal.ToInt64() * pageSize / (1024UL * 1024UL);
            }
        }

        private static string GetFirstNetworkInstance()
        {
            try
            {
                var cat = new PerformanceCounterCategory("Network Interface");
                foreach (var inst in cat.GetInstanceNames())
                    if (!inst.Contains("loopback") && !inst.Contains("isatap") &&
                        !inst.Contains("tunnel") && !inst.Contains("teredo"))
                        return inst;
            }
            catch { }
            return null;
        }

        public SystemStats Next()
        {
            var s = new SystemStats { Timestamp = DateTime.Now };

            if (_cpu != null)
            {
                try { s.CpuPercent = Clamp(_cpu.NextValue()); }
                catch { s.CpuPercent = 0; }
            }

            // RAM via GetPerformanceInfo (accurate physical memory)
            PERFORMANCE_INFORMATION pi;
            if (GetPerformanceInfo(out pi, (uint)Marshal.SizeOf(typeof(PERFORMANCE_INFORMATION))))
            {
                // IntPtr fields → cast to ulong for math (pointer-sized on x64)
                ulong pageSize = (ulong)pi.PageSize.ToInt64();
                ulong totalMB = (ulong)pi.PhysicalTotal.ToInt64() * pageSize / (1024UL * 1024UL);
                ulong availMB = (ulong)pi.PhysicalAvailable.ToInt64() * pageSize / (1024UL * 1024UL);
                ulong usedMB = totalMB - availMB;
                _ramTotalMB = totalMB;
                s.RamTotalGB = totalMB / 1024f;
                s.RamUsedGB = usedMB / 1024f;
                s.RamFreeGB = availMB / 1024f;
                s.RamPercent = totalMB > 0 ? Clamp((float)usedMB / totalMB * 100f) : 0;
            }

            // Disk
            if (_diskRead != null && _diskWrite != null)
            {
                try
                {
                    s.DiskReadKBps = _diskRead.NextValue() / 1024f;
                    s.DiskWriteKBps = _diskWrite.NextValue() / 1024f;
                    s.DiskTotalMbps = (s.DiskReadKBps + s.DiskWriteKBps) * 8f / 1024f;
                }
                catch { }
            }
            if (_diskTime != null)
            {
                try { s.DiskPercent = Clamp(_diskTime.NextValue()); }
                catch { }
            }
            // Fallback: approximate disk % from throughput if % Disk Time counter missing
            if (s.DiskPercent == 0 && (s.DiskReadKBps + s.DiskWriteKBps) > 0)
                s.DiskPercent = Math.Min(100, (s.DiskReadKBps + s.DiskWriteKBps) / 10f);

            // Network
            if (_netSent != null && _netRecv != null)
            {
                try
                {
                    float up = _netSent.NextValue();
                    float dn = _netRecv.NextValue();
                    s.NetUpMbps = up * 8f / 1024f / 1024f;
                    s.NetDownMbps = dn * 8f / 1024f / 1024f;
                }
                catch { }
            }

            return s;
        }

        private static float Clamp(float v) { return v < 0 ? 0 : (v > 100 ? 100 : v); }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_cpu != null) _cpu.Dispose();
            if (_diskRead != null) _diskRead.Dispose();
            if (_diskWrite != null) _diskWrite.Dispose();
            if (_diskTime != null) _diskTime.Dispose();
            if (_netSent != null) _netSent.Dispose();
            if (_netRecv != null) _netRecv.Dispose();
        }
    }
}
