using System;
using UnityEngine;
using UnityEngine.UI;
using MG01.Demo.Views;
using static LT;

namespace MG01.Demo
{
    public static class DemoModule
    {
        private static DemoCoordinator active;
        public static DemoCoordinator Active => active;
        public static void Register()
        { LayoutManager.registeLayout<DemoStagePage>("UIStage", LAYOUT_LIFE_CYCLE.PART_USE, page => { }); }
        public static void Enter()
        {
            if (active != null) return;
            var host = new GameObject("DemoCoordinator"); active = host.AddComponent<DemoCoordinator>();
            active.Begin(() => { active = null; SHOW<UIMain>(); });
        }
    }
}
