using System;
using System.IO;
using System.Linq;
using System.Text;
using MG01.Demo.Domain;
using UnityEngine;

namespace MG01.Demo.Infrastructure
{
    /// <summary>独立JSONL审计输出。写入失败只停用日志，绝不改变会话或抛回战斗流程。</summary>
    public sealed class DemoBattleJournal
    {
        [Serializable] public sealed class ActorEntry { public string key; public int health, shield; }
        [Serializable] public sealed class ItemEntry { public string key, definition, container; public int column, row, slot, rotation; }
        [Serializable] public sealed class StateEntry
        {
            public string session, phase, target; public int round; public ActorEntry[] actors; public ItemEntry[] items;
            public static StateEntry From(BattleSnapshot snapshot) => snapshot == null ? null : new StateEntry {
                session = snapshot.SessionId.ToString(), phase = snapshot.Phase.ToString(), target = snapshot.TargetKey, round = snapshot.Round,
                actors = snapshot.Actors.Select(actor => new ActorEntry { key = actor.Key, health = actor.Health, shield = actor.Shield }).ToArray(),
                items = snapshot.Items.Select(item => new ItemEntry { key = item.Key, definition = item.DefinitionKey, container = item.Container.ToString(), column = item.Column, row = item.Row, slot = item.Slot, rotation = item.Rotation }).ToArray() };
        }
        [Serializable] public sealed class Entry
        {
            public int schema = 1; public string utc, version, kind, actor, target, item, presentation, detail;
            public long sequence, step; public int value, hitIndex; public StateEntry before, after;
        }
        private readonly Action<string> append;
        private readonly string version;
        private long sequence;
        public string Error { get; private set; }
        public string Path { get; }
        public DemoBattleJournal(string version, Action<string> append) { this.version = version; this.append = append; }
        public DemoBattleJournal(string version, string path) : this(version, line => {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)); File.AppendAllText(path, line + "\n", new UTF8Encoding(false)); }) { Path = path; }
        public void Record(string kind, BattleSnapshot before, BattleSnapshot after, ActionStep step = null, string detail = null)
        {
            if (Error != null) return;
            try
            {
                append(JsonUtility.ToJson(new Entry { utc = DateTime.UtcNow.ToString("O"), version = version, sequence = ++sequence,
                    kind = kind, before = StateEntry.From(before), after = StateEntry.From(after), step = step?.StepId ?? 0,
                    actor = step?.ActorKey, target = step?.TargetKey, item = step?.ItemKey, presentation = step?.PresentationKey,
                    value = step?.Value ?? 0, hitIndex = step?.HitIndex ?? 0, detail = detail }));
            }
            catch (Exception exception) { Error = exception.Message; }
        }
    }
}
