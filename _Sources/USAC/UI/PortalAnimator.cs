using UnityEngine;

namespace USAC.InternalUI
{
    // USAC 页面过渡动画主控制器
    public class PortalAnimator
    {
        #region 过渡类型
        public enum TransitionKind
        {
            None,
            CrossFade,
            SharedElement
        }
        #endregion

        #region 字段
        private float startTime = -999f;
        private float duration = 0.25f;
        private bool active = false;

        public TransitionKind Kind { get; private set; } = TransitionKind.None;
        public string FromUrl { get; private set; }
        public string ToUrl { get; private set; }
        public bool IsBack { get; private set; }

        // 附加卡片目标参数
        public Rect SharedElementStart { get; private set; }
        // 追踪实时坐标偏移
        public Rect SharedElementTarget { get; set; }
        #endregion

        #region 属性
        public bool IsPlaying => active && Progress < 1f;

        public float Progress
        {
            get
            {
                if (!active) return 1f;
                return Mathf.Clamp01((Time.unscaledTime - startTime) / duration);
            }
        }

        // 记录真实已过时间
        public float ElapsedTime => active ? Mathf.Max(0f, Time.unscaledTime - startTime) : 0f;

        // 总时长
        public float Duration => duration;

        // SmoothStep 曲线进度
        public float CurvedProgress
        {
            get
            {
                float t = Progress;
                return t * t * (3f - 2f * t);
            }
        }
        #endregion

        #region 启动接口
        // 启动普通页面渐变过渡
        public void StartCrossFade(string from, string to, bool back, float dur = 0.25f)
        {
            FromUrl = from;
            ToUrl = to;
            IsBack = back;
            Kind = TransitionKind.CrossFade;
            duration = dur;
            startTime = Time.unscaledTime;
            active = true;
        }

        // 启动卡片共享元素展开过渡
        public void StartSharedElement(string from, string to, Rect cardRect, float dur = 0.3f)
        {
            FromUrl = from;
            ToUrl = to;
            IsBack = false;
            Kind = TransitionKind.SharedElement;
            SharedElementStart = cardRect;
            duration = dur;
            startTime = Time.unscaledTime;
            active = true;
        }

        // 启动卡片收回过渡
        public void StartSharedElementBack(string from, string to, Rect cardRect, float dur = 0.3f)
        {
            FromUrl = from;
            ToUrl = to;
            IsBack = true;
            Kind = TransitionKind.SharedElement;
            SharedElementStart = cardRect;
            duration = dur;
            startTime = Time.unscaledTime;
            active = true;
        }

        // 结束过渡销毁耦合
        public void Complete()
        {
            active = false;
            Kind = TransitionKind.None;
            FromUrl = null;
            ToUrl = null;
        }
        #endregion

        #region 绘制辅助
        // 获取渐出页面透明
        public float GetFromAlpha() => 1f - CurvedProgress;

        // 获取渐入页面透明
        public float GetToAlpha() => CurvedProgress;

        // 计算变形插值范围
        public Rect LerpRect(Rect start, Rect end)
        {
            float t = CurvedProgress;
            return new Rect(
                Mathf.Lerp(start.x, end.x, t),
                Mathf.Lerp(start.y, end.y, t),
                Mathf.Lerp(start.width, end.width, t),
                Mathf.Lerp(start.height, end.height, t)
            );
        }

        // 计算返回反向插值
        public Rect LerpRectReverse(Rect start, Rect end)
        {
            float t = CurvedProgress;
            return new Rect(
                Mathf.Lerp(end.x, start.x, t),
                Mathf.Lerp(end.y, start.y, t),
                Mathf.Lerp(end.width, start.width, t),
                Mathf.Lerp(end.height, start.height, t)
            );
        }
        #endregion
    }
}
