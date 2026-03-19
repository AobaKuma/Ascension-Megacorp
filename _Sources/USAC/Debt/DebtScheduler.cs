using System;
using System.Collections.Generic;
using Verse;

namespace USAC
{
    // 债务合同调度器
    public class DebtScheduler : IExposable
    {
        // 按到期时间排序的合同队列
        private List<ScheduledEvent> scheduledEvents = new List<ScheduledEvent>();

        private class ScheduledEvent : IExposable
        {
            public int triggerTick;
            public string contractId;
            public Action callback;

            public void ExposeData()
            {
                Scribe_Values.Look(ref triggerTick, "triggerTick");
                Scribe_Values.Look(ref contractId, "contractId");
            }
        }

        // 注册合同到期事件
        public void ScheduleContractCycle(DebtContract contract, Action callback)
        {
            if (contract.NextCycleTick <= 0) return;

            // 移除旧的调度
            UnscheduleContract(contract.ContractId);

            // 添加新调度
            var evt = new ScheduledEvent
            {
                triggerTick = contract.NextCycleTick,
                contractId = contract.ContractId,
                callback = callback
            };

            // 插入排序保持队列有序
            int insertIndex = scheduledEvents.Count;
            for (int i = 0; i < scheduledEvents.Count; i++)
            {
                if (scheduledEvents[i].triggerTick > evt.triggerTick)
                {
                    insertIndex = i;
                    break;
                }
            }
            scheduledEvents.Insert(insertIndex, evt);
        }

        // 取消合同调度
        public void UnscheduleContract(string contractId)
        {
            for (int i = scheduledEvents.Count - 1; i >= 0; i--)
            {
                if (scheduledEvents[i].contractId == contractId)
                {
                    scheduledEvents.RemoveAt(i);
                }
            }
        }

        // 检查并触发到期事件
        public void CheckAndTrigger(int currentTick)
        {
            // 从队列头部开始检查
            while (scheduledEvents.Count > 0)
            {
                var evt = scheduledEvents[0];
                
                // 队列有序所以第一个未到期则后续都未到期
                if (evt.triggerTick > currentTick)
                    break;

                // 移除并触发
                scheduledEvents.RemoveAt(0);
                evt.callback?.Invoke();
            }
        }

        // 重建回调引用
        public void RebuildCallbacks(GameComponent_USACDebt debtComp)
        {
            for (int i = 0; i < scheduledEvents.Count; i++)
            {
                var evt = scheduledEvents[i];
                var contract = debtComp.ActiveContracts.Find(c => c.ContractId == evt.contractId);
                if (contract != null)
                {
                    evt.callback = () => debtComp.ProcessContractCycle(contract);
                }
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref scheduledEvents, "scheduledEvents", LookMode.Deep);
            if (scheduledEvents == null)
                scheduledEvents = new List<ScheduledEvent>();
        }
    }
}
