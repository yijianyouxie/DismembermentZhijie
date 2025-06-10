using UnityEngine;
using System.Collections;
using System.Collections.Generic;
//using Games.TLBB.Log;

public class RagdollEnabler : MonoBehaviour
{
    // 注掉了这些代码，以后不再使用jointDisabler脚本了，只要RigidBody被置为Kinematic，Joint就不会再消耗性能了
    // Prefab初始的Ragdoll配置中RigidBody使用isKinematic，并关闭了所有碰撞体，只在ActiveRagdoll时激活

    //private List<CharacterJointDisabler> _jointDisablers;
    //private Rigidbody[] _rigidBodys;

    List<Rigidbody> _lstRigidBodys;
    List<Collider> _lstCollider = null;
    Animation _animation;
    private void Awake()
    {
        _lstRigidBodys = null;
        ListPool.Instance.Get(out _lstRigidBodys);
        if (_lstRigidBodys == null) return;
        ListPool.Instance.Get(out _lstCollider);
        if (_lstCollider == null) return;


        GetComponentsInChildren(_lstRigidBodys);
        GetComponentsInChildren(_lstCollider);

        _animation = GetComponent<Animation>();


        DeactiveRagdoll();

    }

    public void ActivateRagdoll(Vector3 force)
    {
        //LogSystem.Warn("*************ActivateRagdoll()");

        if (_lstRigidBodys == null) return;
        if (_lstRigidBodys.Count == 0) return ;
        if (_lstCollider == null) return;
        if (_lstCollider.Count == 0) return;

        if (_animation == null)
        {
            //LogSystem.Error("***********************************ActivateRagdoll Animation == null");
            Debug.LogError("***********************************ActivateRagdoll Animation == null");
        }
        _animation.Stop();
        _animation.enabled = false;

        Rigidbody rb;
        for (int i = 0; i < _lstRigidBodys.Count; ++i)
        {
            rb = _lstRigidBodys[i];
            rb.detectCollisions = true;
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        Collider col;
        for (int i = 0; i < _lstCollider.Count; ++i)
        {
            col = _lstCollider[i];
            if ((object)col != null) col.enabled = true;
        }

        //加力
        Rigidbody root = _lstRigidBodys[0];
        root.AddForce(force);
        root.maxAngularVelocity = 90.0f;
        //rb.AddTorque(actor.transform.up * torque);
    }

    public void DeactiveRagdoll()
    {
        //LogSystem.Warn("*************DeactiveRagdoll()" + transform.parent.name);

        if (_lstRigidBodys == null) return;
        if (_lstCollider == null) return;

        Rigidbody rb;
        for (int i = 0; i < _lstRigidBodys.Count; ++i)
        {
            rb = _lstRigidBodys[i];
            rb.detectCollisions = false;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Collider col;
        for (int i = 0; i < _lstCollider.Count; ++i)
        {
            if (_lstCollider[i] != null)
                _lstCollider[i].enabled = false;
        }

        if (_animation != null)
        {
            _animation.enabled = true;
        }
    }

    public void OnDestroy()
    {
        ListPool.Instance.Remove(_lstRigidBodys);
        ListPool.Instance.Remove(_lstCollider);
    }

}