using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshRenderer))]
public class MinuendRenderer : MonoBehaviour
{
    #region ----Renderer----
    public Renderer Renderer => m_renderer;
    private MeshRenderer m_renderer;
    private MeshFilter m_filter;
    public Material Material => m_material;
    private Material m_material;
    public Mesh Mesh => m_filter.sharedMesh;
    #endregion

    #region ----Unity----
    private void OnEnable()
    {
        if (m_renderer == null)
        {
            m_renderer = GetComponent<MeshRenderer>();
            m_material = Instantiate(m_renderer.sharedMaterial);
        }
        if (m_filter == null)
        {
            m_filter = GetComponent<MeshFilter>();
        }
        m_renderer.enabled = false;
        ComplementManager.Instance.Register(this);
    }

    private void OnDisable()
    {
        if (m_renderer != null)
        {
            m_renderer.enabled = true;
        }
        ComplementManager.Instance.Unregister(this);
    }

    private void OnDestroy()
    {
        if (m_material != null)
        {
            Destroy(m_material);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying && m_renderer != null)
        {
            Gizmos.color = Color.red;
            Bounds aabb = GraphicsUtility.TransformAABB(m_renderer.localBounds, transform.localToWorldMatrix);
            Gizmos.DrawWireCube(aabb.center, aabb.size);
        }
    }
#endif
    #endregion
}