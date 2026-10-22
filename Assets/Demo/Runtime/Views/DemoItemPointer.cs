using UnityEngine;
using UnityEngine.EventSystems;

namespace MG01.Demo.Views
{
    /// <summary>UGUI原生事件入口；禁止EventSystem自带的屏幕像素拖动阈值覆盖参考坐标阈值。</summary>
    public sealed class DemoItemPointer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IInitializePotentialDragHandler
    {
        public DemoBattleView Owner { get; set; }
        public string ItemKey { get; set; }
        public void OnInitializePotentialDrag(PointerEventData data) { data.useDragThreshold = false; }
        public void OnPointerDown(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) Owner?.PointerDown(this, data); }
        public void OnDrag(PointerEventData data) { Owner?.PointerMove(this, data); }
        public void OnPointerUp(PointerEventData data) { Owner?.PointerUp(this, data); }
    }
}
