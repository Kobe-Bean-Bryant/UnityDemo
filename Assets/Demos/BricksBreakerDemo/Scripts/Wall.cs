using UnityEngine;

namespace BricksBreakerDemo
{
    public enum WallSide
    {
        Top,
        Bottom,
        Left,
        Right
    }

    /// <summary>
    /// 标识一面屏幕边界墙。挂在每面墙的 GameObject 上，
    /// 让球等物体在碰撞时能直接判断撞的是哪一边。
    /// </summary>
    public class Wall : MonoBehaviour
    {
        public WallSide Side;
    }
}
