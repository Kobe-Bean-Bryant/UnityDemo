using UnityEngine;

namespace UnityDemo.Shared
{
// 将约束升级为 where T : Singleton<T> 会更严谨，方便内部转换和调用
    public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        private static T _instance;
        // 泛型机制使得这个变量无法在不同泛型之间共享
        private static readonly object _lock = new object();

        // 提供一个虚属性开关。默认设为 true (跨场景)
        // 子类可以通过 override 这个属性来将其变成单场景单例
        protected virtual bool IsPersistent => true;

        public static T Instance
        {
            get
            {
                // 绝对不能使用 is null！
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = FindFirstObjectByType<T>();

                            if (_instance == null)
                            {
                                GameObject singletonObject = new GameObject
                                {
                                    name = typeof(T).Name + " (Singleton)"
                                };

                                // 重点：AddComponent 会在底层立刻同步调用 Awake()
                                // 所以跨场景的 DontDestroyOnLoad 逻辑，我们统一挪到 Awake 里处理，
                                // 这里不再需要写 DontDestroyOnLoad
                                _instance = singletonObject.AddComponent<T>();
                            }
                        }
                    }
                }

                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;

                // 根据开关决定是否跨场景
                if (IsPersistent)
                {
                    // 防呆设计：DontDestroyOnLoad 只能对“根节点（Root）”物体生效。
                    // 如果场景里有人把这个单例拖成了某个物体的子节点，我们强行把它提出来。
                    if (transform.parent != null)
                    {
                        transform.SetParent(null);
                    }

                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (_instance != this)
            {
                Debug.LogWarning($"[Singleton] 检测到场景中存在多个 {typeof(T).Name}，已自动销毁多余的实例。");
                Destroy(gameObject);
            }
        }

        // 防空引用报错
        protected virtual void OnDestroy()
        {
            // 如果这是一个【单场景单例】，当场景被卸载时，这个 GameObject 会被 Unity 物理销毁。
            // 但 C# 里的 _instance 静态引用还会指向那块已经被销毁的内存。
            // 必须在这里手动清空引用，保证下次访问时能重新生成！
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
