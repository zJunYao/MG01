using System;

namespace MG01.Demo.Application
{
    /// <summary>参考分辨率坐标下的单指针手势，不依赖Unity事件或屏幕缩放。</summary>
    public sealed class PointerGesture
    {
        private readonly double doubleClickSeconds;
        private readonly double thresholdSquared;
        private string previousKey;
        private double previousTime = double.NegativeInfinity;
        private double startX, startY;
        public string Key { get; private set; }
        public int PointerId { get; private set; }
        public bool Dragging { get; private set; }
        public bool Active => Key != null;
        public PointerGesture(double doubleClickSeconds, double dragThreshold)
        {
            if (doubleClickSeconds <= 0 || dragThreshold <= 0) throw new ArgumentOutOfRangeException();
            this.doubleClickSeconds = doubleClickSeconds; thresholdSquared = dragThreshold * dragThreshold;
        }
        public bool Down(string key, int pointerId, double x, double y)
        {
            if (Active) return false;
            Key = key; PointerId = pointerId; startX = x; startY = y; Dragging = false; return true;
        }
        public bool Move(int pointerId, double x, double y)
        {
            if (!Active || PointerId != pointerId) return false;
            if ((x - startX) * (x - startX) + (y - startY) * (y - startY) >= thresholdSquared)
            { Dragging = true; previousKey = null; }
            return Dragging;
        }
        public bool Up(int pointerId, double time, out bool dragged)
        {
            dragged = false;
            if (!Active || PointerId != pointerId) return false;
            dragged = Dragging;
            bool twice = !dragged && previousKey == Key && time >= previousTime && time - previousTime <= doubleClickSeconds;
            previousKey = dragged || twice ? null : Key; previousTime = time;
            Key = null; Dragging = false; return twice;
        }
        public void Cancel() { Key = null; previousKey = null; previousTime = double.NegativeInfinity; Dragging = false; }
    }
}
