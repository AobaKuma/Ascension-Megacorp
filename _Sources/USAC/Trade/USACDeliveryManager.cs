using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using RimWorld;
using Verse;

namespace USAC
{
    // USAC交付管理器
    // 修复要点
    // 1 待交付物采用深保存 避免读档后Thing变为null导致流程卡死
    // 2 全流程空引用防御 任何失效条目自动剔除
    // 3 异常兜底 出现意外时改用普通空投 保证玩家不丢货且系统可继续使用
    public class USACDeliveryManager : GameComponent
    {
        #region 字段
        private static USACDeliveryManager instance;

        private List<PendingDelivery> pendingDeliveries = new();
        private int currentDeliveryIndex = 0;
        private bool needsReselect = false;
        private bool needsStartPlacement = false;

        // 记录当前已下发的指示器 防止重复选择造成的抖动与死循环
        private Designator activeDesignator;
        private Thing activeThing;

        // 单帧内重入保护
        private bool processing;
        #endregion

        #region 属性
        public static USACDeliveryManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Current.Game?.GetComponent<USACDeliveryManager>();
                }
                return instance;
            }
        }

        // 只统计有效条目 空引用条目不视为待交付
        public bool HasPendingDeliveries => pendingDeliveries != null && pendingDeliveries.Any(IsUsable);

        public List<PendingDelivery> PendingDeliveries
        {
            get
            {
                pendingDeliveries ??= new List<PendingDelivery>();
                return pendingDeliveries;
            }
        }
        #endregion

        #region 生命周期
        public USACDeliveryManager(Game game)
        {
            instance = this;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref pendingDeliveries, "pendingDeliveries", LookMode.Deep);
            Scribe_Values.Look(ref currentDeliveryIndex, "currentDeliveryIndex", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pendingDeliveries ??= new List<PendingDelivery>();

                // 清理读档过程中失效的条目
                // 此阶段地图与物件尚未完全就绪 只剔除记录 不做销毁与地图回退
                int removed = PruneInvalidDeliveries(postLoad: true);
                if (removed > 0)
                {
                    Log.Warning($"[USAC] 读档时移除了 {removed} 条失效的待交付记录 交付流程已重置");
                    NotifyDeliveriesLost(removed);
                }

                // 存在残留交付则恢复放置流程 避免货物永久卡在列表里
                if (pendingDeliveries.Count > 0)
                {
                    needsStartPlacement = true;
                }
                else
                {
                    currentDeliveryIndex = 0;
                }
            }
        }

        public override void GameComponentUpdate()
        {
            if (!needsStartPlacement && !needsReselect)
            {
                return;
            }

            bool doStart = needsStartPlacement;
            bool doReselect = needsReselect;

            // 先复位标志 保证异常时不会每帧重复抛错
            needsStartPlacement = false;
            needsReselect = false;

            try
            {
                if (doStart)
                {
                    StartPlacementProcess();
                }
                else if (doReselect)
                {
                    ProcessNextDelivery();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[USAC] 交付放置流程异常 已转为普通空投兜底\n{ex}");
                AbortAllDeliveries();
            }
        }
        #endregion

        #region 公共方法
        public void AddDelivery(Thing thing, Map map)
        {
            if (thing == null || thing.Destroyed || map == null)
            {
                Log.Warning("[USAC] 忽略无效的交付请求 thing或map为空");
                return;
            }

            PendingDeliveries.Add(new PendingDelivery
            {
                thing = thing,
                map = map,
                confirmed = false,
                targetPos = IntVec3.Invalid,
                targetRot = thing.Rotation
            });
        }

        public void StartPlacementProcess()
        {
            PruneInvalidDeliveries();

            if (!HasPendingDeliveries)
            {
                // 没有有效条目直接收尾 防止残留指示器
                ClearActiveDesignator();
                currentDeliveryIndex = 0;
                return;
            }

            currentDeliveryIndex = 0;

            // 暂停游戏
            if (Find.TickManager != null)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            }

            // 开始第一个建筑的放置
            ProcessNextDelivery();
        }

        public void RequestReselect() => needsReselect = true;

        public void RequestStartPlacement() => needsStartPlacement = true;

        public void ConfirmPlacement(Thing thing, IntVec3 pos, Rot4 rot)
        {
            if (thing == null)
            {
                // 指示器持有的物件已失效 直接跳过当前条目继续流程
                Log.Warning("[USAC] 确认放置时物件为空 跳过该条目");
                PruneInvalidDeliveries();
                RequestReselect();
                return;
            }

            var delivery = PendingDeliveries.FirstOrDefault(d => d != null && d.thing == thing);
            if (delivery == null)
            {
                Log.Warning($"[USAC] 未找到与 {thing.LabelCap} 匹配的待交付记录 跳过");
                RequestReselect();
                return;
            }

            delivery.confirmed = true;
            delivery.targetPos = pos;
            delivery.targetRot = rot;

            // 处理下一个
            ProcessNextDelivery();
        }

        // 放弃当前排队并以普通空投方式发货 用于异常兜底与调试
        public void AbortAllDeliveries()
        {
            var snapshot = PendingDeliveries.ToList();
            pendingDeliveries.Clear();
            currentDeliveryIndex = 0;
            ClearActiveDesignator();

            foreach (var delivery in snapshot)
            {
                if (!IsUsable(delivery))
                {
                    DisposeOrphan(delivery?.thing);
                    continue;
                }

                if (!TryFallbackDropPod(delivery))
                {
                    DisposeOrphan(delivery.thing);
                }
            }
        }
        #endregion

        #region 私有方法
        private void ProcessNextDelivery()
        {
            if (processing)
            {
                return;
            }

            processing = true;
            try
            {
                PruneInvalidDeliveries();

                if (pendingDeliveries.Count == 0)
                {
                    ClearActiveDesignator();
                    currentDeliveryIndex = 0;
                    return;
                }

                if (currentDeliveryIndex < 0)
                {
                    currentDeliveryIndex = 0;
                }

                // 查找下一个未确认的交付
                while (currentDeliveryIndex < pendingDeliveries.Count)
                {
                    var delivery = pendingDeliveries[currentDeliveryIndex];

                    // 双保险 循环内再次校验 防止遍历途中条目失效
                    if (!IsUsable(delivery))
                    {
                        pendingDeliveries.RemoveAt(currentDeliveryIndex);
                        continue;
                    }

                    if (!delivery.confirmed)
                    {
                        SelectDesignatorFor(delivery);
                        return;
                    }

                    currentDeliveryIndex++;
                }

                // 放置完成 清除指示器
                ClearActiveDesignator();
                ExecuteAllDeliveries();
            }
            finally
            {
                processing = false;
            }
        }

        private void SelectDesignatorFor(PendingDelivery delivery)
        {
            // 已经在为同一物件放置则不重复下发 避免选择与反选互相触发的死循环
            if (activeThing == delivery.thing
                && activeDesignator != null
                && Find.DesignatorManager?.SelectedDesignator == activeDesignator)
            {
                return;
            }

            // 目标地图与当前地图不一致时先切图 否则放置校验会一直失败
            Map targetMap = ResolveMap(delivery);
            if (targetMap != null && Find.CurrentMap != targetMap)
            {
                Current.Game.CurrentMap = targetMap;
            }

            var designator = new Designator_PlaceUSACDelivery(delivery.thing);
            activeDesignator = designator;
            activeThing = delivery.thing;

            Find.DesignatorManager.Select(designator);

            Messages.Message(
                "USAC.Trade.SelectPlacement".Translate(delivery.thing.LabelCap),
                MessageTypeDefOf.NeutralEvent);
        }

        private void ClearActiveDesignator()
        {
            activeDesignator = null;
            activeThing = null;
            Find.DesignatorManager?.Deselect();
        }

        private void ExecuteAllDeliveries()
        {
            var toExecute = PendingDeliveries
                .Where(d => IsUsable(d) && d.confirmed)
                .ToList();

            // 未确认的残留条目不应留在列表里 统一兜底处理
            var leftovers = PendingDeliveries
                .Where(d => !toExecute.Contains(d))
                .ToList();

            pendingDeliveries.Clear();
            currentDeliveryIndex = 0;

            int dispatched = 0;
            foreach (var delivery in toExecute)
            {
                try
                {
                    if (DispatchOne(delivery))
                    {
                        dispatched++;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[USAC] 交付 {delivery.thing?.def?.defName ?? "null"} 时出错 改用普通空投\n{ex}");
                    if (!TryFallbackDropPod(delivery))
                    {
                        DisposeOrphan(delivery.thing);
                    }
                }
            }

            foreach (var delivery in leftovers)
            {
                if (IsUsable(delivery))
                {
                    if (!TryFallbackDropPod(delivery))
                    {
                        DisposeOrphan(delivery.thing);
                    }
                }
                else
                {
                    DisposeOrphan(delivery?.thing);
                }
            }

            if (dispatched > 0)
            {
                Messages.Message(
                    "USAC.Trade.DeliveriesDispatched".Translate(),
                    MessageTypeDefOf.PositiveEvent);
            }
        }

        private bool DispatchOne(PendingDelivery delivery)
        {
            Thing thing = delivery.thing;
            Map map = ResolveMap(delivery);
            if (map == null)
            {
                DisposeOrphan(thing);
                return false;
            }

            // 解除任何残留持有关系 否则生成天降物时会报错
            DetachFromHolder(thing);

            // 设置建筑物阵营
            if (thing is Building building && building.def.CanHaveFaction && building.Faction != Faction.OfPlayer)
            {
                building.SetFaction(Faction.OfPlayer);
            }

            if (thing.def.rotatable && delivery.targetRot.IsValid)
            {
                thing.Rotation = delivery.targetRot;
            }

            IntVec3 pos = delivery.targetPos;
            if (!pos.IsValid || !pos.InBounds(map))
            {
                pos = DropCellFinder.TradeDropSpot(map);
            }

            // 生成运输夹
            SkyfallerMaker.SpawnSkyfaller(
                USAC_DefOf.USAC_TransportIncoming,
                thing,
                pos,
                map);

            return true;
        }

        // 剔除所有不可用条目 并同步修正当前索引
        // postLoad为true时处于读档收尾阶段 只做记录级清理
        private int PruneInvalidDeliveries(bool postLoad = false)
        {
            pendingDeliveries ??= new List<PendingDelivery>();

            int removed = 0;
            for (int i = pendingDeliveries.Count - 1; i >= 0; i--)
            {
                var delivery = pendingDeliveries[i];

                if (IsUsable(delivery))
                {
                    // 地图引用失效时尝试回退到当前地图
                    if (postLoad || ResolveMap(delivery) != null)
                    {
                        continue;
                    }
                }

                // 物件仍然存活但已无法交付 直接销毁 防止内存中悬挂
                if (!postLoad && delivery != null && delivery.thing != null && !delivery.thing.Destroyed)
                {
                    DisposeOrphan(delivery.thing);
                }

                pendingDeliveries.RemoveAt(i);
                removed++;

                if (currentDeliveryIndex > i)
                {
                    currentDeliveryIndex--;
                }

                if (delivery != null && delivery.thing != null && delivery.thing == activeThing)
                {
                    activeDesignator = null;
                    activeThing = null;
                }
            }

            if (currentDeliveryIndex > pendingDeliveries.Count)
            {
                currentDeliveryIndex = pendingDeliveries.Count;
            }
            if (currentDeliveryIndex < 0)
            {
                currentDeliveryIndex = 0;
            }

            return removed;
        }

        private static bool IsUsable(PendingDelivery delivery)
        {
            return delivery != null
                && delivery.thing != null
                && delivery.thing.def != null
                && !delivery.thing.Destroyed;
        }

        // 解析可用地图 失效则回退并写回条目
        private static Map ResolveMap(PendingDelivery delivery)
        {
            if (delivery == null)
            {
                return null;
            }

            if (delivery.map != null && Find.Maps != null && Find.Maps.Contains(delivery.map))
            {
                return delivery.map;
            }

            Map fallback = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            delivery.map = fallback;
            return fallback;
        }

        private static void DetachFromHolder(Thing thing)
        {
            if (thing == null)
            {
                return;
            }

            if (thing.Spawned)
            {
                thing.DeSpawn(DestroyMode.Vanish);
            }

            if (thing.holdingOwner != null)
            {
                thing.holdingOwner.Remove(thing);
            }
        }

        private static bool TryFallbackDropPod(PendingDelivery delivery)
        {
            if (!IsUsable(delivery))
            {
                return false;
            }

            Map map = ResolveMap(delivery);
            if (map == null)
            {
                return false;
            }

            try
            {
                DetachFromHolder(delivery.thing);
                TradeUtility.SpawnDropPod(DropCellFinder.TradeDropSpot(map), map, delivery.thing);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[USAC] 兜底空投失败\n{ex}");
                return false;
            }
        }

        private static void DisposeOrphan(Thing thing)
        {
            if (thing == null || thing.Destroyed)
            {
                return;
            }

            try
            {
                DetachFromHolder(thing);
                thing.Destroy(DestroyMode.Vanish);
            }
            catch (Exception ex)
            {
                Log.Warning($"[USAC] 清理失效交付物时出错\n{ex}");
            }
        }

        private static void NotifyDeliveriesLost(int count)
        {
            if (count <= 0)
            {
                return;
            }

            try
            {
                // 未配置语言键时使用内置文案 避免再抛异常
                string text = $"USAC 有 {count} 件待放置的货物在读档时丢失 相关记录已清除";
                if ("USAC.Trade.DeliveriesLost".CanTranslate())
                {
                    text = "USAC.Trade.DeliveriesLost".Translate(count).Resolve();
                }

                Messages.Message(text, MessageTypeDefOf.NegativeEvent, false);
            }
            catch
            {
                // 读档阶段消息系统未就绪时忽略
            }
        }
        #endregion

        #region 内部类
        public class PendingDelivery : IExposable
        {
            public Thing thing;
            public Map map;
            public bool confirmed;
            public IntVec3 targetPos = IntVec3.Invalid;
            public Rot4 targetRot = Rot4.North;

            public void ExposeData()
            {
                // 关键修复
                // 待交付物既未生成也不属于任何ThingOwner 使用引用保存时无人深保存该对象
                // 读档时交叉引用无法解析 thing必然为null 进而使整个交付流程卡死
                // 这里改为深保存 由本组件真正持有该物件
                if (Scribe.mode == LoadSaveMode.LoadingVars && IsLegacyReferenceNode())
                {
                    // 旧存档为引用格式 无法还原 静默丢弃 由PostLoadInit统一清理
                    thing = null;
                }
                else
                {
                    Scribe_Deep.Look(ref thing, "thing");
                }

                Scribe_References.Look(ref map, "map");
                Scribe_Values.Look(ref confirmed, "confirmed", false);
                Scribe_Values.Look(ref targetPos, "targetPos", IntVec3.Invalid);
                Scribe_Values.Look(ref targetRot, "targetRot", Rot4.North);
            }

            // 判断当前节点是否为旧版引用式保存 避免深读取时刷出无意义的报错
            private static bool IsLegacyReferenceNode()
            {
                XmlNode parent = Scribe.loader?.curXmlParent;
                XmlNode node = parent?["thing"];
                if (node == null)
                {
                    return false;
                }

                if (node.Attributes?["Class"] != null || node.Attributes?["IsNull"] != null)
                {
                    return false;
                }

                // 引用节点只有一个纯文本子节点 内容形如 Thing_XXX123
                return node.ChildNodes.Count == 1 && node.FirstChild is XmlText;
            }
        }
        #endregion
    }
}
