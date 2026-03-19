using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace USAC
{
    // USAC交付管理器
    public class USACDeliveryManager : GameComponent
    {
        #region 字段
        private static USACDeliveryManager instance;
        private List<PendingDelivery> pendingDeliveries = new();
        private int currentDeliveryIndex = 0;
        #endregion

        #region 属性
        public static USACDeliveryManager Instance => instance;
        public bool HasPendingDeliveries => pendingDeliveries.Any();
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
            }
        }
        #endregion

        #region 公共方法
        public void AddDelivery(Thing thing, Map map)
        {
            if (thing == null || map == null)
                return;
            
            pendingDeliveries.Add(new PendingDelivery
            {
                thing = thing,
                map = map,
                confirmed = false
            });
        }

        public void StartPlacementProcess()
        {
            if (!HasPendingDeliveries)
                return;
            
            currentDeliveryIndex = 0;
            
            // 暂停游戏
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            
            // 开始第一个建筑的放置
            ProcessNextDelivery();
        }

        public void ConfirmPlacement(Thing thing, IntVec3 pos, Rot4 rot)
        {
            var delivery = pendingDeliveries.FirstOrDefault(d => d.thing == thing);
            if (delivery != null)
            {
                delivery.confirmed = true;
                delivery.targetPos = pos;
                delivery.targetRot = rot;
                
                // 处理下一个
                ProcessNextDelivery();
            }
        }
        #endregion

        #region 私有方法
        private void ProcessNextDelivery()
        {
            // 查找下一个未确认的交付
            while (currentDeliveryIndex < pendingDeliveries.Count)
            {
                var delivery = pendingDeliveries[currentDeliveryIndex];
                
                if (!delivery.confirmed)
                {
                    // 启动放置模式
                    var designator = new Designator_PlaceUSACDelivery(delivery.thing);
                    Find.DesignatorManager.Select(designator);
                    
                    Messages.Message(
                        "USAC.Trade.SelectPlacement".Translate(delivery.thing.LabelCap),
                        MessageTypeDefOf.NeutralEvent);
                    
                    return;
                }
                
                currentDeliveryIndex++;
            }
            
            // 所有建筑都已放置完成
            ExecuteAllDeliveries();
        }

        private void ExecuteAllDeliveries()
        {
            foreach (var delivery in pendingDeliveries.Where(d => d.confirmed))
            {
                // 设置建筑物朝向
                if (delivery.thing is Building building)
                {
                    building.SetFaction(Faction.OfPlayer);
                }
                
                // 生成运输夹
                SkyfallerMaker.SpawnSkyfaller(
                    USAC_DefOf.USAC_TransportIncoming,
                    delivery.thing,
                    delivery.targetPos,
                    delivery.map);
            }
            
            // 清空列表
            pendingDeliveries.Clear();
            currentDeliveryIndex = 0;
            
            Messages.Message(
                "USAC.Trade.DeliveriesDispatched".Translate(),
                MessageTypeDefOf.PositiveEvent);
        }
        #endregion

        #region 内部类
        private class PendingDelivery : IExposable
        {
            public Thing thing;
            public Map map;
            public bool confirmed;
            public IntVec3 targetPos;
            public Rot4 targetRot;

            public void ExposeData()
            {
                Scribe_References.Look(ref thing, "thing");
                Scribe_References.Look(ref map, "map");
                Scribe_Values.Look(ref confirmed, "confirmed", false);
                Scribe_Values.Look(ref targetPos, "targetPos");
                Scribe_Values.Look(ref targetRot, "targetRot");
            }
        }
        #endregion
    }
}
