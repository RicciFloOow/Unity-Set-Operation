using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ComplementManager
{
    #region ----Singleton----
    private static ComplementManager instance;
    public static ComplementManager Instance => instance ??= new ComplementManager();

    private ComplementManager()
    {
        m_minuendRendererList = new List<MinuendRenderer>();
        m_subtrahendRendererList = new List<SubtrahendRenderer>();
        m_unusedMinuendRendererList = new List<MinuendRenderer>();
        m_usedMinuendRendererList = new List<MinuendRenderer>();
    }
    #endregion

    #region ----Renderers----
    public IReadOnlyList<MinuendRenderer> UnusedMinuendRendererList => m_unusedMinuendRendererList.AsReadOnly();
    public IReadOnlyList<MinuendRenderer> UsedMinuendRendererList => m_usedMinuendRendererList.AsReadOnly();
    public IReadOnlyList<SubtrahendRenderer> SubtrahendRendererList => m_subtrahendRendererList.AsReadOnly();

    private List<MinuendRenderer> m_minuendRendererList;
    private List<SubtrahendRenderer> m_subtrahendRendererList;
    private List<MinuendRenderer> m_unusedMinuendRendererList;
    private List<MinuendRenderer> m_usedMinuendRendererList;

    public void Register(MinuendRenderer renderer)
    {
        if (m_minuendRendererList.Contains(renderer))
        {
            return;
        }
        m_minuendRendererList.Add(renderer);
    }

    public void Unregister(MinuendRenderer renderer)
    {
        if (m_minuendRendererList.Contains(renderer))
        {
            m_minuendRendererList.Remove(renderer);
        }
    }

    public void Register(SubtrahendRenderer renderer)
    {
        if (m_subtrahendRendererList.Contains(renderer))
        {
            return;
        }
        m_subtrahendRendererList.Add(renderer);
    }

    public void Unregister(SubtrahendRenderer renderer)
    {
        if (m_subtrahendRendererList.Contains(renderer))
        {
            m_subtrahendRendererList.Remove(renderer);
        }
    }
    #endregion

    #region ----Culling----
    public void ResetMinuendRenderers()
    {
        m_unusedMinuendRendererList.Clear();
        m_usedMinuendRendererList.Clear();
        for (int i = 0; i < m_minuendRendererList.Count; i++)
        {
            m_unusedMinuendRendererList.Add(m_minuendRendererList[i]);
        }
    }

    //对SubtrahendRenderer渲染顺序做个简单的排序，先渲染远的再渲染近的
    public void OrderSubtrahendRenderers(Vector3 camPos, Vector3 camForward)
    {
        m_subtrahendRendererList.Sort(
            (x, y) => Vector3.Dot(y.transform.position - camPos, camForward).CompareTo(Vector3.Dot(x.transform.position - camPos, camForward))
            );
    }

    //对给定的SubtrahendRenderer，我们只绘制与之包围盒相交的MinuendRenderer
    //这里就只做最简单的CPU剔除，如果是cluster的，那么直接在GPU里剔除，然后用arg buffer配合DrawProceduralIndirect()绘制即可
    public IReadOnlyList<MinuendRenderer> GetIntersectedMinuendRenderers(SubtrahendRenderer sub)
    {
        List<MinuendRenderer> mrs = new List<MinuendRenderer>();
        //
        Bounds subBB = sub.Renderer.localBounds;
        Matrix4x4 subO2WMatrix = sub.transform.localToWorldMatrix;
        //
        for (int i = 0; i < m_minuendRendererList.Count; i++)
        {
            var mr = m_minuendRendererList[i];
            if (GraphicsUtility.OBBIntersectOBB(subBB, subO2WMatrix, mr.Renderer.localBounds, mr.transform.localToWorldMatrix))
            {
                mrs.Add(mr);
                if (m_unusedMinuendRendererList.Contains(mr))
                {
                    m_unusedMinuendRendererList.Remove(mr);
                }
                if (!m_usedMinuendRendererList.Contains(mr))
                {
                    m_usedMinuendRendererList.Add(mr);
                }
            }
        }
        //
        return mrs.AsReadOnly();
    }

    #endregion
}