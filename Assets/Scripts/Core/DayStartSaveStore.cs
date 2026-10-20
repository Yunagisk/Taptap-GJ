using System;
using System.IO;
using System.Text;
using UnityEngine;

// One persistent slot. Only the initial morning and subsequent dawns write it.
public sealed class DayStartSaveStore
{
    [Serializable] sealed class SaveData
    {
        public int version;
        public bool hasWildernessEvents;
        public GameState state;
    }

    public string Path { get; }
    public DayStartSaveStore(string path) { Path = path; }
    public static GameState Copy(GameState state)
    {
        var copy = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
        // Unity serializes a null inline class as an empty instance. Preserve the ungenerated level.
        if (state.map.wildernessEvents == null) copy.map.wildernessEvents = null;
        return copy;
    }

    public bool TrySave(GameState state, MapLayout layout, out string error)
    {
        string temporary = Path + ".tmp";
        try
        {
            Validate(state, layout);
            var data = new SaveData { version = 1, hasWildernessEvents = state.map.wildernessEvents != null, state = state };
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(data, true));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path)));
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            // A failed write must leave the previous morning intact.
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(Path)) File.Replace(temporary, Path, null);
                    else File.Move(temporary, Path);
                    break;
                }
                catch (IOException) when (attempt < 4)
                {
                    // Windows scanners may briefly hold a just-written slot without delete sharing.
                    System.Threading.Thread.Sleep(15 * (attempt + 1));
                }
            }
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = "清晨存档保存失败：" + exception.Message;
            return false;
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public bool TryLoad(MapLayout layout, out GameState state, out string error)
    {
        state = null;
        if (!File.Exists(Path)) { error = "暂无可用存档"; return false; }
        try
        {
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(Path, Encoding.UTF8));
            if (data == null || data.version != 1) throw new InvalidDataException("不支持的存档格式。");
            if (!data.hasWildernessEvents && data.state?.map != null) data.state.map.wildernessEvents = null;
            Validate(data.state, layout);
            EquipmentRules.EnsureInitialized(data.state);
            ShopRules.EnsureStock(data.state);
            state = data.state;
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = "无法读取清晨存档：" + exception.Message;
            return false;
        }
    }

    static void Validate(GameState state, MapLayout layout)
    {
        MapLayoutValidator.Validate(layout);
        if (state?.world == null || state.map == null || state.player == null || state.inventory == null
            || state.shop == null || state.story == null || state.monsterWeaknesses == null
            || state.world.time < 0 || state.world.time % WorldResourceRules.UnitsPerDay != 0
            || state.player.hp <= 0 || state.player.maxHp < state.player.hp
            || state.world.sin < 0 || state.world.sin > WorldResourceRules.MaxSin || state.world.sinRemnant < 0
            || state.inventory.items == null || state.inventory.weapons == null
            || state.inventory.equippedWeaponIds == null || state.inventory.seenWeaponIds == null
            || state.map.clearedNodes == null || state.map.visitedNodes == null || state.map.revealedNodes == null
            || state.map.spawnedBossNodes == null || state.map.defeatedBossNodes == null
            || state.map.templateId != layout.id || !layout.nodes.Exists(node => node.id == state.map.currentNodeId))
            throw new InvalidDataException("存档状态不完整，或不属于当前地图的清晨。");
        bool pending = !string.IsNullOrEmpty(state.map.pendingRequestId);
        if (pending != !string.IsNullOrEmpty(state.map.pendingNodeId)
            || pending && (state.map.pendingNodeId != state.map.currentNodeId
                || !layout.nodes.Exists(node => node.id == state.map.pendingNodeId)))
            throw new InvalidDataException("存档中的待处理节点无效。");
        var wilderness = state.map.wildernessEvents;
        if (wilderness != null && (wilderness.seed != state.map.randomSeed || wilderness.stage != state.world.stage
            || wilderness.nodes == null || wilderness.nodes.Count == 0 || wilderness.history == null
            || wilderness.repairedRoads == null))
            throw new InvalidDataException("存档中的野外事件不完整。");
    }
}
