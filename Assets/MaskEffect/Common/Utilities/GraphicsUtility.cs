using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class GraphicsUtility
{
    #region ----Constants----
    /// <summary>
    /// 我们认为在几何上应该被视为同一点的最大距离的平方
    /// </summary>
    public const float K_SamePointSquareDistance = 10e-16f;
    #endregion

    #region ----Texture----
    /// <summary>
    /// 全屏Blit, 需要着色器的vs是覆盖全屏的三角形的
    /// </summary>
    /// <param name="cmd"></param>
    /// <param name="source"></param>
    /// <param name="target"></param>
    /// <param name="pass"></param>
    /// <param name="material"></param>
    public static void CustumBlit(ref CommandBuffer cmd, RTHandle source, RTHandle target, int sourceId, int pass, Material material)
    {
        MaterialPropertyBlock _matPropertyBlock = new MaterialPropertyBlock();
        _matPropertyBlock.SetTexture(sourceId, source);
        //
        cmd.SetRenderTarget(target);
        cmd.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 3, 1, _matPropertyBlock);
    }
    #endregion

    #region ----Compute Buffer----
    public static void AllocateComputeBuffer(ref ComputeBuffer cb, int count, int stride, ComputeBufferType cbt = ComputeBufferType.Structured, ComputeBufferMode cbm = ComputeBufferMode.Immutable)
    {
        if (cb == null || cb.count != count || cb.stride != stride)
        {
            cb?.Release();
            cb = new ComputeBuffer(count, stride, cbt, cbm);
        }
    }
    #endregion

    #region ----Graphics Buffer----
    public static void AllocateGraphicsBuffer(ref GraphicsBuffer gb, int count, int stride, GraphicsBuffer.Target target = GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags flags = GraphicsBuffer.UsageFlags.None)
    {
        if (gb == null || gb.count != count || gb.stride != stride)
        {
            gb?.Release();
            gb = new GraphicsBuffer(target, flags, count, stride);
        }
    }
    #endregion

    #region ----Collision----
    //ref: https://www.realtimerendering.com/resources/GraphicsGems/gems/TransBox.c
    public static Bounds TransformAABB(Bounds old, Matrix4x4 o2wMatrix)
    {
        float a, b;
        //
        Vector3 oldMin = old.center - old.extents;
        Vector3 oldMax = old.center + old.extents;
        //
        Vector3 Omin = oldMin;
        Vector3 Omax = oldMax;
        Vector3 Nmin = new Vector3(o2wMatrix.GetRow(0).w, o2wMatrix.GetRow(1).w, o2wMatrix.GetRow(2).w);
        Vector3 Nmax = Nmin;
        //
        for (int i = 0; i < 3; i++)
        {
            Vector4 row = o2wMatrix.GetRow(i);
            for (int j = 0; j < 3; j++)
            {
                a = row[j] * Omin[j];
                b = row[j] * Omax[j];
                if (a < b)
                {
                    Nmin[i] += a;
                    Nmax[i] += b;
                }
                else
                {
                    Nmin[i] += b;
                    Nmax[i] += a;
                }
            }
        }
        return new Bounds(0.5f * (Nmin + Nmax), (Nmax - Nmin));
    }

    //ref: https://www.geometrictools.com/GTE/Mathematics/IntrOrientedBox3OrientedBox3.h
    //用mathematics包来写能方便一些
    public static bool OBBIntersectOBB(Bounds box0, Matrix4x4 o2wMat0, Bounds box1, Matrix4x4 o2wMat1)
    {
        const float k_intSec_Epsilon = 0.0001f;
        const float k_intSec_Cutoff = 1 - k_intSec_Epsilon;//(1 - k_intSec_Epsilon)
        //
        Vector3 C0 = o2wMat0.MultiplyPoint3x4(box0.center);
        Vector3 E0 = Vector3.Scale(o2wMat0.lossyScale, box0.extents); 
        Vector3 C1 = o2wMat1.MultiplyPoint3x4(box1.center);
        Vector3 E1 = Vector3.Scale(o2wMat1.lossyScale, box1.extents);
        //
        Vector3 D = C1 - C0;
        //
        Quaternion rot0 = o2wMat0.rotation;
        Quaternion rot1 = o2wMat1.rotation;
        //
        Vector3[] A0 = new Vector3[] { rot0 * Vector3.right, rot0 * Vector3.up, rot0 * Vector3.forward };
        Vector3[] A1 = new Vector3[] { rot1 * Vector3.right, rot1 * Vector3.up, rot1 * Vector3.forward };
        //
        bool existsParallelPair = false;
        //
        float[,] dot01 = new float[3, 3];
        float[,] absDot01 = new float[3, 3];
        Vector3 dotDA0 = Vector3.zero;
        //
        float r0, r1, r, r01;
        //
        for (int i = 0; i < 3; i++)
        {
            dot01[0, i] = Vector3.Dot(A0[0], A1[i]);
            absDot01[0, i] = Mathf.Abs(dot01[0, i]);
            if (absDot01[0, i] > k_intSec_Cutoff)
            {
                existsParallelPair = true;
            }
        }
        dotDA0[0] = Vector3.Dot(D, A0[0]);
        r = Mathf.Abs(dotDA0[0]);
        r1 = E1[0] * absDot01[0, 0] + E1[1] * absDot01[0, 1] + E1[2] * absDot01[0, 2];
        r01 = E0[0] + r1;
        if (r > r01)
        {
            return false;
        }
        //
        for (int i = 0; i < 3; i++)
        {
            dot01[1, i] = Vector3.Dot(A0[1], A1[i]);
            absDot01[1, i] = Mathf.Abs(dot01[1, i]);
            if (absDot01[1, i] > k_intSec_Cutoff)
            {
                existsParallelPair = true;
            }
        }
        dotDA0[1] = Vector3.Dot(D, A0[1]);
        r = Mathf.Abs(dotDA0[1]);
        r1 = E1[0] * absDot01[1, 0] + E1[1] * absDot01[1, 1] + E1[2] * absDot01[1, 2];
        r01 = E0[1] + r1;
        if (r > r01)
        {
            return false;
        }
        //
        for (int i = 0; i < 3; i++)
        {
            dot01[2, i] = Vector3.Dot(A0[2], A1[i]);
            absDot01[2, i] = Mathf.Abs(dot01[2, i]);
            if (absDot01[2, i] > k_intSec_Cutoff)
            {
                existsParallelPair = true;
            }
        }
        dotDA0[2] = Vector3.Dot(D, A0[2]);
        r = Mathf.Abs(dotDA0[2]);
        r1 = E1[0] * absDot01[2, 0] + E1[1] * absDot01[2, 1] + E1[2] * absDot01[2, 2];
        r01 = E0[2] + r1;
        if (r > r01)
        {
            return false;
        }
        //
        r = Mathf.Abs(Vector3.Dot(D, A1[0]));
        r0 = E0[0] * absDot01[0, 0] + E0[1] * absDot01[1, 0] + E0[2] * absDot01[2, 0];
        r01 = r0 + E1[0];
        if (r > r01)
        {
            return false;
        }
        //
        r = Mathf.Abs(Vector3.Dot(D, A1[1]));
        r0 = E0[0] * absDot01[0, 1] + E0[1] * absDot01[1, 1] + E0[2] * absDot01[2, 1];
        r01 = r0 + E1[1];
        if (r > r01)
        {
            return false;
        }
        //
        r = Mathf.Abs(Vector3.Dot(D, A1[2]));
        r0 = E0[0] * absDot01[0, 2] + E0[1] * absDot01[1, 2] + E0[2] * absDot01[2, 2];
        r01 = r0 + E1[2];
        if (r > r01)
        {
            return false;
        }
        //
        if (existsParallelPair)
        {
            return true;
        }
        //
        r = Mathf.Abs(dotDA0[2] * dot01[1, 0] - dotDA0[1] * dot01[2, 0]);
        r0 = E0[1] * absDot01[2, 0] + E0[2] * absDot01[1, 0];
        r1 = E1[1] * absDot01[0, 2] + E1[2] * absDot01[0, 1];
        r01 = r0 + r1;
        if (r > r01)
        {
            return false;
        }
        //
        r = Mathf.Abs(dotDA0[2] * dot01[1, 1] - dotDA0[1] * dot01[2, 1]);
        r0 = E0[1] * absDot01[2, 1] + E0[2] * absDot01[1, 1];
        r1 = E1[0] * absDot01[0, 2] + E1[2] * absDot01[0, 0];
        r01 = r0 + r1;
        if (r > r01)
        {
            return false;
        }
        //
        r = Mathf.Abs(dotDA0[2] * dot01[1, 2] - dotDA0[1] * dot01[2, 2]);
        r0 = E0[1] * absDot01[2, 2] + E0[2] * absDot01[1, 2];
        r1 = E1[0] * absDot01[0, 1] + E1[1] * absDot01[0, 0];
        r01 = r0 + r1;
        if (r > r01)
        {
            return false;
        }
        //
        r = Mathf.Abs(dotDA0[0] * dot01[2, 0] - dotDA0[2] * dot01[0, 0]);
        r0 = E0[0] * absDot01[2, 0] + E0[2] * absDot01[0, 0];
        r1 = E1[1] * absDot01[1, 2] + E1[2] * absDot01[1, 1];
        r01 = r0 + r1;
        if (r > r01)
        {
            return false;
        }
        //
        r = Mathf.Abs(dotDA0[0] * dot01[2, 1] - dotDA0[2] * dot01[0, 1]);
        r0 = E0[0] * absDot01[2, 1] + E0[2] * absDot01[0, 1];
        r1 = E1[0] * absDot01[1, 2] + E1[2] * absDot01[1, 0];
        r01 = r0 + r1;
        if (r > r01)
        {
            return false;
        }
        //
        r = Mathf.Abs(dotDA0[0] * dot01[2, 2] - dotDA0[2] * dot01[0, 2]);
        r0 = E0[0] * absDot01[2, 2] + E0[2] * absDot01[0, 2];
        r1 = E1[0] * absDot01[1, 1] + E1[1] * absDot01[1, 0];
        r01 = r0 + r1;
        if (r > r01)
        {
            return false;
        }
        //
        r = Mathf.Abs(dotDA0[1] * dot01[0, 0] - dotDA0[0] * dot01[1, 0]);
        r0 = E0[0] * absDot01[1, 0] + E0[1] * absDot01[0, 0];
        r1 = E1[1] * absDot01[2, 2] + E1[2] * absDot01[2, 1];
        r01 = r0 + r1;
        if (r > r01)
        {
            return false;
        }
        //
        r = Mathf.Abs(dotDA0[1] * dot01[0, 1] - dotDA0[0] * dot01[1, 1]);
        r0 = E0[0] * absDot01[1, 1] + E0[1] * absDot01[0, 1];
        r1 = E1[0] * absDot01[2, 2] + E1[2] * absDot01[2, 0];
        r01 = r0 + r1;
        if (r > r01)
        {
            return false;
        }
        //
        r = Mathf.Abs(dotDA0[1] * dot01[0, 2] - dotDA0[0] * dot01[1, 2]);
        r0 = E0[0] * absDot01[1, 2] + E0[1] * absDot01[0, 2];
        r1 = E1[0] * absDot01[2, 1] + E1[1] * absDot01[2, 0];
        r01 = r0 + r1;
        if (r > r01)
        {
            return false;
        }
        return true;
    }
    #endregion
}