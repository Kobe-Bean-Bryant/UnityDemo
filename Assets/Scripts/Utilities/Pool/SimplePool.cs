using System.Collections.Generic;
using UnityEngine;

namespace UnityDemo.Shared
{
    public static class SimplePool
    {
        // 通过将此值设置为等于或大于您预期大多数对象池大小的数值，
        // 可以避免 Stack 内部数组的重新调整大小。
        // 注意，您也可以使用 Preload() 来设置对象池的初始大小——
        // 如果只有某些对象池会特别大（例如子弹），这会很有用。
        private const int DefaultPoolSize = 3;

        // 我们所有的对象池。
        // 使用预制体名称作为键，确保相同类型的预制体共享同一个对象池。
        private static Dictionary<string, Pool> _pools;

        // 所有对象池的根父对象，用于保持 Hierarchy 整洁。
        private static GameObject _poolRoot;

        /// <summary>
        /// 如果您想在场景开始时预加载一些对象的副本，可以使用此方法。
        /// 除非您需要从零个实例快速增加到 10 个以上，否则实际上并不需要这样做。
        /// 从技术上讲可以进一步优化，但在实践中 Spawn/Despawn 序列会非常快，
        /// 这样避免了代码重复。
        /// </summary>
        public static void Preload(GameObject prefab, int qty = 1)
        {
            Init(prefab, qty);

            // 创建数组来获取我们即将预生成的对象。
            GameObject[] obs = new GameObject[qty];
            for (int i = 0; i < qty; i++)
            {
                obs[i] = Spawn(prefab, Vector3.zero, Quaternion.identity);
            }

            // 现在将它们全部放回对象池。
            for (int i = 0; i < qty; i++)
            {
                Despawn(obs[i]);
            }
        }

        /// <summary>
        /// 生成指定预制体的副本（如果需要则实例化一个）。
        /// 注意：请记住 Awake() 或 Start() 只会在第一次生成时运行，
        /// 成员变量不会被重置。OnEnable 会在生成后运行——但请注意，
        /// 切换 IsActive 也会调用该函数。
        /// </summary>
        public static GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            Init(prefab);

            return _pools[prefab.name].Spawn(pos, rot);
        }

        /// <summary>
        /// 将指定的游戏对象放回其对象池中。
        /// </summary>
        public static void Despawn(GameObject obj)
        {
            PoolMember pm = obj.GetComponent<PoolMember>();
            if (pm == null)
            {
                Object.Destroy(obj);
            }
            else
            {
                pm.pool.Despawn(obj);
            }
        }

        /// <summary>
        /// 初始化我们的字典。
        /// </summary>
        private static void Init(GameObject prefab = null, int qty = DefaultPoolSize)
        {
            if (_pools == null)
            {
                _pools = new Dictionary<string, Pool>();
            }

            // 确保根对象存在
            if (_poolRoot == null)
            {
                _poolRoot = new GameObject("SimplePool");
            }

            if (prefab != null && !_pools.ContainsKey(prefab.name))
            {
                _pools[prefab.name] = new Pool(prefab, qty);
            }
        }

        /// <summary>
        /// Pool 类代表特定预制体的对象池。
        /// </summary>
        private class Pool
        {
            // 我们会在我们实例化的任何对象的名称后面附加一个 ID。
            // 这纯粹是为了美观。
            private int _nextId = 1;

            // 包含非活动对象的结构。
            // 为什么使用 Stack 而不是 List？因为我们永远不需要从数组的开头或中间取出对象。
            // 我们总是只获取最后一个对象，这样就消除了打乱内存中对象的需要。
            private readonly Stack<GameObject> _inactive;

            // 我们正在池化的预制体。
            private readonly GameObject _prefab;

            // 此对象池的父对象，用于组织 Hierarchy。
            private readonly Transform _poolTransform;

            // 构造函数。
            public Pool(GameObject prefab, int initialQty)
            {
                this._prefab = prefab;

                // 创建此对象池的父对象
                _poolTransform = CreatePoolParent(prefab.name);

                // 如果 Stack 在内部使用链表，那么整个 initialQty 参数就是一个安慰剂，
                // 我们可以将其删除以获得更简洁的代码。
                _inactive = new Stack<GameObject>(initialQty);
            }

            // 创建对象池的父对象
            private static Transform CreatePoolParent(string poolName)
            {
                // 创建此池的父对象
                GameObject poolParent = new GameObject(poolName + "_Pool");
                poolParent.transform.SetParent(_poolRoot.transform);
                return poolParent.transform;
            }

            // 从对象池中生成一个对象。
            public GameObject Spawn(Vector3 pos, Quaternion rot)
            {
                while (true)
                {
                    GameObject obj;
                    if (_inactive.Count == 0)
                    {
                        // 我们的池中没有对象，所以我们实例化一个全新的对象。
                        obj = (GameObject)Object.Instantiate(_prefab, pos, rot);
                        obj.name = _prefab.name + " (" + (_nextId++) + ")";

                        // 添加 PoolMember 组件，以便我们知道它属于哪个池。
                        obj.AddComponent<PoolMember>().pool = this;

                        // 设置父对象为此池的父对象
                        obj.transform.SetParent(_poolTransform);
                    }
                    else
                    {
                        // 获取非活动数组中的最后一个对象。
                        obj = _inactive.Pop();

                        if (obj == null)
                        {
                            // 我们期望找到的非活动对象不再存在。
                            // 最可能的原因是：
                            //   - 有人对我们的对象调用了 Destroy()
                            //   - 场景切换（这将销毁我们所有的对象）。
                            //     注意：如果真的不想这样，可以使用 DontDestroyOnLoad 来防止。
                            // 不用担心——我们只需尝试序列中的下一个对象。
                            continue;
                        }
                    }

                    obj.transform.position = pos;
                    obj.transform.rotation = rot;
                    obj.SetActive(true);
                    return obj;
                }
            }

            // 将对象返回到非活动池中。
            public void Despawn(GameObject obj)
            {
                obj.SetActive(false);

                // 由于 Stack 没有 Capacity 成员，我们无法控制它在必须扩展内部数组时的增长因子。
                // 另一方面，它可能在内部使用链表。但是，为什么它允许我们在构造函数中指定大小呢？
                // Stack 很奇怪。
                _inactive.Push(obj);
            }
        }

        /// <summary>
        /// 添加到新实例化的对象上，以便我们在回收时可以链接回正确的池。
        /// </summary>
        private class PoolMember : MonoBehaviour
        {
            public Pool pool;
        }
    }
}
