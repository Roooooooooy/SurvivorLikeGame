using UnityEngine;

// 给 HD-2D 精灵代理做逐实例的高度/阴影覆盖。
// 桥默认把精灵矩形底边贴到地面（effectLift=0.25），
// 有上下留白的角色表需要用负 lift 把“脚底线”压回地面。
public class Hd2dProxyLift : MonoBehaviour
{
    [Tooltip("世界单位；负数把精灵压向地面")]
    public float lift = 0f;
    public bool castShadow = false;
    public bool receiveShadows = false;
}
