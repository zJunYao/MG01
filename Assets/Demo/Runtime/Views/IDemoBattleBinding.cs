using System.Collections.Generic;
using MG01.Demo.Configuration;
using MG01.Demo.Domain;
using UnityEngine;

namespace MG01.Demo.Views
{
    /// <summary>表现只读快照并发送命令，不引用HotFix程序集；资源所有权由协调器持有。</summary>
    public interface IDemoBattleBinding
    {
        DemoConfigCatalog Catalog { get; }
        BattleSnapshot Snapshot { get; }
        IReadOnlyList<EnemyIntent> Intents { get; }
        Sprite Sprite(string key);
        bool Move(string key, ItemContainer container, int column, int row, int slot, out string reason);
        bool Rotate(string key, out string reason);
        bool SelectTarget(string key, out string reason);
        bool CycleTarget(out string reason);
        bool Confirm(bool dragging, out string reason);
        void Restart();
        void Exit();
    }
}
