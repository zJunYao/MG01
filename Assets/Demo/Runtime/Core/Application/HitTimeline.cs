using System;

namespace MG01.Demo.Application
{
    /// <summary>单一步骤的跨帧时间门禁；命中与完成分离，低帧率越过两点时先命中。</summary>
    public sealed class HitTimeline
    {
        private readonly double hitTime, endTime;
        private bool hit, complete;
        public HitTimeline(double duration, double normalizedHit, double normalizedEnd)
        {
            if (double.IsNaN(duration) || double.IsInfinity(duration) || double.IsNaN(normalizedHit) || double.IsInfinity(normalizedHit) ||
                double.IsNaN(normalizedEnd) || double.IsInfinity(normalizedEnd) || duration <= 0 || normalizedHit < 0 || normalizedEnd < normalizedHit || normalizedEnd <= 0) throw new ArgumentOutOfRangeException();
            hitTime = duration * normalizedHit; endTime = duration * normalizedEnd;
        }
        public bool TakeHit(double elapsed) { if (hit || elapsed < hitTime) return false; hit = true; return true; }
        public bool TakeComplete(double elapsed) { if (!hit || complete || elapsed < endTime) return false; complete = true; return true; }
    }
}
