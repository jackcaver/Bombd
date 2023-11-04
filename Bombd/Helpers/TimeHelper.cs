using System.Diagnostics;

namespace Bombd.Helpers;

public static class TimeHelper
{
    private static readonly Stopwatch _stopwatch;
    private static readonly int _localTimeStart;

    static TimeHelper()
    {
        _stopwatch = new Stopwatch();
        _localTimeStart = (int)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond);
        _stopwatch.Start();
    }

    public static int LocalTime => (int)_stopwatch.ElapsedMilliseconds + _localTimeStart;
}