using System;
using MG01.Demo.Application;
using NUnit.Framework;

namespace MG01.Demo.Tests
{
    public sealed class HitTimelineTests
    {
        [Test] public void 跨帧越过命中只提交一次()
        {
            var timeline = new HitTimeline(2, .5, 1);
            Assert.That(timeline.TakeHit(.9), Is.False);
            Assert.That(timeline.TakeHit(1.4), Is.True);
            Assert.That(timeline.TakeHit(4), Is.False);
            Assert.That(timeline.TakeComplete(1.9), Is.False);
            Assert.That(timeline.TakeComplete(4), Is.True);
            Assert.That(timeline.TakeComplete(5), Is.False);
        }
        [Test] public void 完成不能替代命中且大步长先命中再完成()
        {
            var timeline = new HitTimeline(1, .4, .8);
            Assert.That(timeline.TakeComplete(5), Is.False);
            Assert.That(timeline.TakeHit(5), Is.True);
            Assert.That(timeline.TakeComplete(5), Is.True);
        }
        [Test] public void 零时点蓄力允许无伤害命中步骤()
        {
            var timeline = new HitTimeline(1, 0, 1);
            Assert.That(timeline.TakeHit(0), Is.True);
            Assert.That(timeline.TakeHit(.1), Is.False);
            Assert.That(timeline.TakeComplete(1), Is.True);
        }
        [TestCase(0, .5, 1)] [TestCase(1, -.1, 1)] [TestCase(1, .9, .5)]
        public void 非法时序拒绝启动(double duration, double hit, double end)
        { Assert.Throws<ArgumentOutOfRangeException>(() => new HitTimeline(duration, hit, end)); }
    }
}
