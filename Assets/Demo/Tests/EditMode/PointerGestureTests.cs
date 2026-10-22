using MG01.Demo.Application;
using NUnit.Framework;

namespace MG01.Demo.Tests
{
    public sealed class PointerGestureTests
    {
        [Test] public void 同道具窗口内双击才触发旋转()
        {
            var gesture = new PointerGesture(.25, 10);
            gesture.Down("staff", -1, 0, 0); Assert.That(gesture.Up(-1, 1, out _), Is.False);
            gesture.Down("staff", -1, 0, 0); Assert.That(gesture.Up(-1, 1.25, out _), Is.True);
            gesture.Down("hat", -1, 0, 0); Assert.That(gesture.Up(-1, 1.4, out _), Is.False);
            gesture.Down("hat", -1, 0, 0); Assert.That(gesture.Up(-1, 1.7, out _), Is.False);
        }
        [Test] public void 阈值按参考坐标且拖动不能成为双击()
        {
            var gesture = new PointerGesture(.25, 10); gesture.Down("staff", -1, 0, 0);
            Assert.That(gesture.Move(-1, 9, 0), Is.False); Assert.That(gesture.Move(-1, 6, 8), Is.True);
            Assert.That(gesture.Up(-1, 1, out var dragged), Is.False); Assert.That(dragged, Is.True);
            gesture.Down("staff", -1, 0, 0); Assert.That(gesture.Up(-1, 1.1, out _), Is.False);
        }
        [Test] public void 第二指针及取消不会残留双击或拖动()
        {
            var gesture = new PointerGesture(.25, 10); gesture.Down("staff", 1, 0, 0);
            Assert.That(gesture.Down("hat", 2, 0, 0), Is.False); Assert.That(gesture.Move(2, 100, 0), Is.False);
            gesture.Up(2, 1, out _); Assert.That(gesture.Active, Is.True);
            gesture.Cancel(); Assert.That(gesture.Active, Is.False); Assert.That(gesture.Dragging, Is.False);
            gesture.Down("staff", 1, 0, 0); Assert.That(gesture.Up(1, 1.1, out _), Is.False);
        }
    }
}
