using UnityEngine;

// 池化标记：记录对象来自哪个 prefab，SmallObjectPool 靠它把对象归还到正确的池。
// 由池在首次生成时自动挂载，业务代码不需要手动添加。
[DisallowMultipleComponent]
public class PooledObject : MonoBehaviour
{
    // 源 prefab：Unity 对象判空语义在场景重载后依然成立
    public GameObject SourcePrefab { get; private set; }

    // 是否已在空闲池中，防止同一帧重复 Release
    internal bool InPool { get; set; }

    public void Bind(GameObject prefab) => SourcePrefab = prefab;
}
