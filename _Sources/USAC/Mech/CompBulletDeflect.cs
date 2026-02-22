using Verse;

namespace USAC
{
    public class CompProperties_BulletDeflect : CompProperties
    {
        // 弹飞后剩余飞行距离
        public float deflectFlightDist = 5f;

        // 法线随机扰动幅度
        public float normalJitter = 0.3f;

        public CompProperties_BulletDeflect()
        {
            compClass = typeof(CompBulletDeflect);
        }
    }

    public class CompBulletDeflect : ThingComp
    {
        public CompProperties_BulletDeflect Props =>
            (CompProperties_BulletDeflect)props;
    }
}

