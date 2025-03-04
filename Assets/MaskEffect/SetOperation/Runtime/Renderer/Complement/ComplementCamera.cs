//https://github.com/hecomi/UnityScreenSpaceBoolean
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public class ComplementCamera : MonoBehaviour
{
    #region ----Camera----
    private Camera m_renderCam;

    private void SetupRenderCam()
    {
        if (m_renderCam == null)
        {
            m_renderCam = GetComponent<Camera>();
            m_renderCam.allowHDR = false;
            m_renderCam.allowMSAA = false;
        }
    }
    #endregion

    #region ----Shader Helper----
    private static readonly int k_shaderProperty_Cmd_ZTest = Shader.PropertyToID("_ZTest");
    private static readonly int k_shaderProperty_Cmd_ZWrite = Shader.PropertyToID("_ZWrite");
    private static readonly int k_shaderProperty_Cmd_CullMode = Shader.PropertyToID("_CullMode");

    private static readonly int k_shaderProperty_Tex_CameraDepthInnerGRT = Shader.PropertyToID("_CameraDepthInnerGRT");
    private static readonly int k_shaderProperty_Tex_CameraDepthOuterGRT = Shader.PropertyToID("_CameraDepthOuterGRT");
    private static readonly int k_shaderProperty_Tex_CameraDepthInnerLRT = Shader.PropertyToID("_CameraDepthInnerLRT");
    private static readonly int k_shaderProperty_Tex_CameraDepthOuterLRT = Shader.PropertyToID("_CameraDepthOuterLRT");

    private static readonly int k_shaderProperty_Tex_CameraMaskRT = Shader.PropertyToID("_CameraMaskRT");
    private static readonly int k_shaderProperty_Tex_CameraDepthRT = Shader.PropertyToID("_CameraDepthRT");
    private static readonly int k_shaderProperty_Tex_CameraDepthSubInnerRT = Shader.PropertyToID("_CameraDepthSubInnerRT");
    #endregion

    #region ----RT Handle----
    private RTHandle m_camColor_Handle;
    private RTHandle m_camDepth_Handle;
    private RTHandle m_camDepthLowPingpong0_Handle;
    private RTHandle m_camDepthLowPingpong1_Handle;
    private RTHandle m_camMask_Handle;
    private RTHandle m_camDepthInnerG_Handle;
    private RTHandle m_camDepthOuterG_Handle;
    private RTHandle m_camDepthInnerL_Handle;
    private RTHandle m_camDepthOuterL_Handle;
    private RTHandle m_camDepthSubInner_Handle;

    private int m_camLowPingpong;

    private void SetupRTHandles()
    {
        Vector2Int screenSize = new Vector2Int(m_renderCam.pixelWidth, m_renderCam.pixelHeight);
        m_camColor_Handle = new RTHandle(screenSize.x, screenSize.y, 0, GraphicsFormat.R8G8B8A8_UNorm);
        m_camDepth_Handle = new RTHandle(screenSize.x, screenSize.y, GraphicsFormat.None, GraphicsFormat.D32_SFloat);
        m_camDepthLowPingpong0_Handle = new RTHandle(screenSize.x, screenSize.y, GraphicsFormat.None, GraphicsFormat.D16_UNorm);
        m_camDepthLowPingpong1_Handle = new RTHandle(screenSize.x, screenSize.y, GraphicsFormat.None, GraphicsFormat.D16_UNorm); 
        m_camMask_Handle = new RTHandle(screenSize.x, screenSize.y, 0, GraphicsFormat.R16_UNorm);//之后可以清空，然后作为绘制时material id在color buffer中的RT
        //别看要4张GraphicsFormat.D16_UNorm的纹理，显存占用和一张同尺寸的GraphicsFormat.D32_SFloat_S8_UInt是一样的(有24bit没用的用于对齐)
        m_camDepthInnerG_Handle = new RTHandle(screenSize.x, screenSize.y, GraphicsFormat.None, GraphicsFormat.D16_UNorm);
        m_camDepthOuterG_Handle = new RTHandle(screenSize.x, screenSize.y, GraphicsFormat.None, GraphicsFormat.D16_UNorm);
        m_camDepthInnerL_Handle = new RTHandle(screenSize.x, screenSize.y, GraphicsFormat.None, GraphicsFormat.D16_UNorm);
        m_camDepthOuterL_Handle = new RTHandle(screenSize.x, screenSize.y, GraphicsFormat.None, GraphicsFormat.D16_UNorm);
        //
        m_camDepthSubInner_Handle = new RTHandle(screenSize.x, screenSize.y, GraphicsFormat.None, GraphicsFormat.D16_UNorm);
    }

    private void ReleaseRTHandles()
    {
        m_camColor_Handle?.Release();
        m_camDepth_Handle?.Release();
        m_camDepthLowPingpong0_Handle?.Release();
        m_camDepthLowPingpong1_Handle?.Release();
        m_camMask_Handle?.Release();
        m_camDepthInnerG_Handle?.Release();
        m_camDepthOuterG_Handle?.Release();
        m_camDepthInnerL_Handle?.Release();
        m_camDepthOuterL_Handle?.Release();
        m_camDepthSubInner_Handle?.Release();
    }
    #endregion

    #region ----Material----
    private Material m_minuendDepthMat;
    private Material m_subtrahendDepthMat;
    private Material m_complementDepthMat;

    private void SetupMaterials()
    {
        m_minuendDepthMat = new Material(Shader.Find("MaskEffect/Complement/MinuendDepth"));
        m_subtrahendDepthMat = new Material(Shader.Find("MaskEffect/Complement/SubtrahendDepth"));
        m_complementDepthMat = new Material(Shader.Find("MaskEffect/Complement/ComplementDepth"));
    }

    private void ReleaseMaterials()
    {
        if (m_minuendDepthMat != null)
        {
            Destroy(m_minuendDepthMat);
        }
        if (m_subtrahendDepthMat != null)
        {
            Destroy(m_subtrahendDepthMat);
        }
        if (m_complementDepthMat != null)
        {
            Destroy(m_complementDepthMat);
        }
    }
    #endregion

    #region ----Complement Pass----
    private const CameraEvent K_ComplementPassCameraEvent = CameraEvent.BeforeForwardOpaque;
    private CommandBuffer m_complementPassCmdBuffer;

    private void ReleaseComplementPass()
    {
        if (m_renderCam != null && m_complementPassCmdBuffer != null)
        {
            m_renderCam.RemoveCommandBuffer(K_ComplementPassCameraEvent, m_complementPassCmdBuffer);
            m_complementPassCmdBuffer.Release();
            m_complementPassCmdBuffer = null;
        }
    }

    private void DrawRenderersDepth(ref CommandBuffer cmd, SubtrahendRenderer subRenderer, IReadOnlyList<MinuendRenderer> mList, RTHandle sourceDepth, RTHandle destDepth)
    {
        //TODO:基于包围盒检测并分批绘制受影响的被减集
        //
        //绘制被减集的内表面(GEqual的)
        cmd.SetRenderTarget(m_camMask_Handle, m_camDepthInnerG_Handle);//注意，这里应该用BuiltinRenderTextureType.None作为color buffer，我这里这么做只是为了得到"正确"的VP矩阵而利用了一下"特性"(偷懒罢了)
        cmd.ClearRenderTarget(true, true, Color.clear, 0);//TODO:深度默认值区分API
        for (int i = 0; i < mList.Count; i++)
        {
            var m = mList[i];
            cmd.DrawMesh(m.Mesh, m.transform.localToWorldMatrix, m_minuendDepthMat, 0, 0);
        }
        //绘制被减集的内表面(LEqual的)
        cmd.SetRenderTarget(m_camMask_Handle, m_camDepthInnerL_Handle);
        cmd.ClearRenderTarget(true, true, Color.clear);
        for (int i = 0; i < mList.Count; i++)
        {
            var m = mList[i];
            cmd.DrawMesh(m.Mesh, m.transform.localToWorldMatrix, m_minuendDepthMat, 0, 1);
        }
        //绘制被减集的外表面(GEqual的)
        cmd.SetRenderTarget(m_camMask_Handle, m_camDepthOuterG_Handle);
        cmd.ClearRenderTarget(true, true, Color.clear, 0);
        for (int i = 0; i < mList.Count; i++)
        {
            var m = mList[i];
            cmd.DrawMesh(m.Mesh, m.transform.localToWorldMatrix, m_minuendDepthMat, 0, 2);
        }
        //绘制被减集的外表面(LEqual的)
        cmd.SetRenderTarget(m_camMask_Handle, m_camDepthOuterL_Handle);
        cmd.ClearRenderTarget(true, true, Color.clear);
        for (int i = 0; i < mList.Count; i++)
        {
            var m = mList[i];
            cmd.DrawMesh(m.Mesh, m.transform.localToWorldMatrix, m_minuendDepthMat, 0, 3);
        }
        //利用被减集的内外表面深度图来绘制减集的内表面深度图
        cmd.SetRenderTarget(m_camMask_Handle, m_camDepthSubInner_Handle);
        cmd.ClearRenderTarget(true, false, Color.clear);
        {
            MaterialPropertyBlock matPropertyBlock = new MaterialPropertyBlock();
            matPropertyBlock.SetTexture(k_shaderProperty_Tex_CameraDepthInnerGRT, m_camDepthInnerG_Handle);
            matPropertyBlock.SetTexture(k_shaderProperty_Tex_CameraDepthOuterGRT, m_camDepthOuterG_Handle);
            matPropertyBlock.SetTexture(k_shaderProperty_Tex_CameraDepthInnerLRT, m_camDepthInnerL_Handle);
            matPropertyBlock.SetTexture(k_shaderProperty_Tex_CameraDepthOuterLRT, m_camDepthOuterL_Handle);
            cmd.DrawMesh(subRenderer.Mesh, subRenderer.transform.localToWorldMatrix, m_subtrahendDepthMat, 0, 0, matPropertyBlock);
        }
        //利用被减集的内外表面深度图来绘制减集的外表面裁剪后的mask
        cmd.SetRenderTarget(m_camMask_Handle);
        cmd.ClearRenderTarget(true, false, Color.clear);
        {
            MaterialPropertyBlock matPropertyBlock = new MaterialPropertyBlock();
            matPropertyBlock.SetTexture(k_shaderProperty_Tex_CameraDepthOuterLRT, m_camDepthOuterL_Handle);
            cmd.DrawMesh(subRenderer.Mesh, subRenderer.transform.localToWorldMatrix, m_subtrahendDepthMat, 0, 1, matPropertyBlock);
        }
        //整合绘制完的深度图，有需要的话也可以同时整合mask
        cmd.SetRenderTarget(BuiltinRenderTextureType.None, destDepth);
        {
            MaterialPropertyBlock matPropertyBlock = new MaterialPropertyBlock();
            matPropertyBlock.SetTexture(k_shaderProperty_Tex_CameraDepthRT, sourceDepth);
            matPropertyBlock.SetTexture(k_shaderProperty_Tex_CameraMaskRT, m_camMask_Handle);
            matPropertyBlock.SetTexture(k_shaderProperty_Tex_CameraDepthSubInnerRT, m_camDepthSubInner_Handle);
            matPropertyBlock.SetTexture(k_shaderProperty_Tex_CameraDepthOuterLRT, m_camDepthOuterL_Handle);
            matPropertyBlock.SetTexture(k_shaderProperty_Tex_CameraDepthOuterGRT, m_camDepthOuterG_Handle);
            cmd.DrawProcedural(Matrix4x4.identity, m_complementDepthMat, 0, MeshTopology.Triangles, 3, 1, matPropertyBlock);
        }
    }

    private void DrawRenderersColor(ref CommandBuffer cmd, RTHandle destDepth)
    {
        cmd.SetRenderTarget(m_camColor_Handle, destDepth);
        cmd.ClearRenderTarget(false, true, Color.clear);
        //
        var sList = ComplementManager.Instance.SubtrahendRendererList;
        var usedMList = ComplementManager.Instance.UsedMinuendRendererList;
        var unusedMList = ComplementManager.Instance.UnusedMinuendRendererList;
        //
        for (int i = 0; i < sList.Count; i++)
        {
            var s = sList[i];
            s.Material.SetFloat(k_shaderProperty_Cmd_ZTest, (int)CompareFunction.Equal);
            s.Material.SetFloat(k_shaderProperty_Cmd_ZWrite, 0);//zwrite off
            s.Material.SetFloat(k_shaderProperty_Cmd_CullMode, (int)CullMode.Front);
            cmd.DrawMesh(s.Mesh, s.transform.localToWorldMatrix, s.Material);
        }
        //
        for (int i = 0; i < usedMList.Count; i++)
        {
            var um = usedMList[i];
            um.Material.SetFloat(k_shaderProperty_Cmd_ZTest, (int)CompareFunction.Equal);
            um.Material.SetFloat(k_shaderProperty_Cmd_ZWrite, 0);//zwrite off
            cmd.DrawMesh(um.Mesh, um.transform.localToWorldMatrix, um.Material);
        }
        //
        for (int i = 0; i < unusedMList.Count; i++)
        {
            var uum = unusedMList[i];
            uum.Material.SetFloat(k_shaderProperty_Cmd_ZTest, (int)CompareFunction.LessEqual);
            uum.Material.SetFloat(k_shaderProperty_Cmd_ZWrite, 1);//zwrite on
            cmd.DrawMesh(uum.Mesh, uum.transform.localToWorldMatrix, uum.Material);
        }
    }

    private void BlitLowDepthToDepth(ref CommandBuffer cmd, RTHandle sourceDepth)
    {
        cmd.SetRenderTarget(BuiltinRenderTextureType.None, m_camDepth_Handle);
        {
            MaterialPropertyBlock matPropertyBlock = new MaterialPropertyBlock();
            matPropertyBlock.SetTexture(k_shaderProperty_Tex_CameraDepthRT, sourceDepth);
            cmd.DrawProcedural(Matrix4x4.identity, m_complementDepthMat, 1, MeshTopology.Triangles, 3, 1, matPropertyBlock);
        }
    }

    private void SetupComplementPass()
    {
        ReleaseComplementPass();
        //
        if (m_renderCam != null)
        {
            m_complementPassCmdBuffer = new CommandBuffer()
            {
                name = "Complement Pass"
            };
            //
            {
                m_complementPassCmdBuffer.SetRenderTarget(m_camMask_Handle, m_camDepthLowPingpong0_Handle);
                m_complementPassCmdBuffer.ClearRenderTarget(true, false, Color.clear);
                m_complementPassCmdBuffer.SetRenderTarget(m_camMask_Handle, m_camDepthLowPingpong1_Handle);
                m_complementPassCmdBuffer.ClearRenderTarget(true, false, Color.clear);
            }
            //
            var sList = ComplementManager.Instance.SubtrahendRendererList;
            ComplementManager.Instance.ResetMinuendRenderers();
            ComplementManager.Instance.OrderSubtrahendRenderers(transform.position, transform.forward);
            m_camLowPingpong = 0;
            for (int i = 0; i < sList.Count; i++)
            {
                var s = sList[i];
                var mList = ComplementManager.Instance.GetIntersectedMinuendRenderers(s);
                if (mList.Count > 0)
                {
                    //pingpong
                    if ((m_camLowPingpong & 1) == 0)
                    {
                        DrawRenderersDepth(ref m_complementPassCmdBuffer, s, mList, m_camDepthLowPingpong0_Handle, m_camDepthLowPingpong1_Handle);
                    }
                    else
                    {
                        DrawRenderersDepth(ref m_complementPassCmdBuffer, s, mList, m_camDepthLowPingpong1_Handle, m_camDepthLowPingpong0_Handle);
                    }
                    m_camLowPingpong++;
                }
            }
            //
            //绘制减集与被减集的Color
            if ((m_camLowPingpong & 1) == 0)
            {
                DrawRenderersColor(ref m_complementPassCmdBuffer, m_camDepthLowPingpong0_Handle);
                BlitLowDepthToDepth(ref m_complementPassCmdBuffer, m_camDepthLowPingpong0_Handle);
            }
            else
            {
                DrawRenderersColor(ref m_complementPassCmdBuffer, m_camDepthLowPingpong1_Handle);
                BlitLowDepthToDepth(ref m_complementPassCmdBuffer, m_camDepthLowPingpong1_Handle);
            }
            //
            m_renderCam.AddCommandBuffer(K_ComplementPassCameraEvent, m_complementPassCmdBuffer);
        }
    }
    #endregion

    #region ----Final Draw Pass----
    private const CameraEvent K_FinalDrawPassCameraEvent = CameraEvent.AfterEverything;
    private CommandBuffer m_finalDrawPassCmdBuffer;

    private void ReleaseFinalDrawPass()
    {
        if (m_renderCam != null && m_finalDrawPassCmdBuffer != null)
        {
            m_renderCam.RemoveCommandBuffer(K_FinalDrawPassCameraEvent, m_finalDrawPassCmdBuffer);
            m_finalDrawPassCmdBuffer.Release();
            m_finalDrawPassCmdBuffer = null;
        }
    }

    private void SetupFinalDrawPass()
    {
        ReleaseFinalDrawPass();
        //
        if (m_renderCam != null)
        {
            m_finalDrawPassCmdBuffer = new CommandBuffer()
            {
                name = "Final Draw Pass"
            };
            m_finalDrawPassCmdBuffer.Blit(m_camColor_Handle, BuiltinRenderTextureType.CameraTarget);
            m_renderCam.AddCommandBuffer(K_FinalDrawPassCameraEvent, m_finalDrawPassCmdBuffer);
        }
    }
    #endregion

    #region ----Control----
    private Vector2 m_lastFrameRightMousePosition;
    private Vector3 m_cam_EulerAngle;

    private void CameraControl()
    {
        //Rot
        if (Input.GetMouseButtonDown(1))
        {
            m_lastFrameRightMousePosition = Input.mousePosition;
        }
        else if (Input.GetMouseButton(1))
        {
            Vector2 _mousePosition = Input.mousePosition;
            Vector2 deltaMousePosition = _mousePosition - m_lastFrameRightMousePosition;
            deltaMousePosition *= -0.1f;
            m_cam_EulerAngle += 240 * Time.deltaTime * new Vector3(deltaMousePosition.y, -deltaMousePosition.x, 0);
            transform.eulerAngles = m_cam_EulerAngle;
            m_lastFrameRightMousePosition = _mousePosition;
        }
        //Move
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        transform.position += 5 * Time.deltaTime * (h * transform.right + v * transform.forward);
        if (Input.GetKey(KeyCode.E))
        {
            transform.position += 5 * Time.deltaTime * transform.up;
        }
        else if (Input.GetKey(KeyCode.Q))
        {
            transform.position -= 5 * Time.deltaTime * transform.up;
        }
    }
    #endregion

    #region ----Unity----
    private void OnEnable()
    {
        SetupRenderCam();
        SetupRTHandles();
    }

    private void Start()
    {
        SetupMaterials();
    }

    private void Update()
    {
        CameraControl();
    }

    private void OnPreRender()
    {
        SetupComplementPass();
        SetupFinalDrawPass();
        m_renderCam.SetTargetBuffers(m_camColor_Handle.ColorBuffer, m_camDepth_Handle.DepthBuffer);
    }

    private void OnPostRender()
    {
        m_renderCam.targetTexture = null;
    }

    private void OnDisable()
    {
        ReleaseComplementPass();
        ReleaseFinalDrawPass();
        ReleaseRTHandles();
    }

    private void OnDestroy()
    {
        ReleaseMaterials();
    }
    #endregion
}