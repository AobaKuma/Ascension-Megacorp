using System;
using System.Collections.Generic;

namespace USAC
{
    // 债务事件类型
    public enum DebtEventType
    {
        PrincipalChanged,      // 本金变更
        InterestAccrued,       // 利息累积
        PaymentMade,           // 还款完成
        PaymentFailed,         // 还款失败
        ContractSettled,       // 合同结清
        SystemLockChanged,     // 系统锁定状态变更
        CollectionStarted,     // 开始强制收缴
        CollectionCompleted    // 收缴完成
    }

    // 债务事件数据
    public class DebtEventArgs
    {
        public DebtEventType EventType { get; set; }
        public DebtContract Contract { get; set; }
        public float Amount { get; set; }
        public string Reason { get; set; }
        public object Data { get; set; }
    }

    // 债务事件总线
    public class DebtEventBus
    {
        private static DebtEventBus instance;
        public static DebtEventBus Instance => instance ??= new DebtEventBus();

        private readonly Dictionary<DebtEventType, List<Action<DebtEventArgs>>> subscribers = new();

        // 订阅事件
        public void Subscribe(DebtEventType eventType, Action<DebtEventArgs> handler)
        {
            if (!subscribers.ContainsKey(eventType))
                subscribers[eventType] = new List<Action<DebtEventArgs>>();

            subscribers[eventType].Add(handler);
        }

        // 取消订阅
        public void Unsubscribe(DebtEventType eventType, Action<DebtEventArgs> handler)
        {
            if (subscribers.ContainsKey(eventType))
                subscribers[eventType].Remove(handler);
        }

        // 发布事件
        public void Publish(DebtEventArgs eventArgs)
        {
            if (subscribers.ContainsKey(eventArgs.EventType))
            {
                foreach (var handler in subscribers[eventArgs.EventType])
                {
                    try
                    {
                        handler(eventArgs);
                    }
                    catch (Exception ex)
                    {
                        Verse.Log.Error($"[USAC] 债务事件处理异常: {ex}");
                    }
                }
            }
        }

        // 清空所有订阅
        public void Clear()
        {
            subscribers.Clear();
        }
    }
}
