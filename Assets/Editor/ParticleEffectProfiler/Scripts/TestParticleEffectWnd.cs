using System.Collections.Generic;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEditor;
using System.IO;
using System;
using System.Reflection;
using System.Drawing.Printing;
using System.Linq;
using static UnityEngine.ParticleSystem;


public class TestParticleEffectWnd : EditorWindow
{
    enum TestType
    {
        默认目录 = 0,
        选中目录,
        选中特效,
    }

    static string testScenePath = "Assets/Editor/ParticleEffectProfiler/TestScene/EffectEvla.unity";
    static string TestScenePath
    {
        get
        {
            string _cache = EditorPrefs.GetString("ParticleEffectProfiler_TestScenePath");
            if (!string.IsNullOrEmpty(_cache))
                testScenePath = _cache;
            if (string.IsNullOrEmpty(testScenePath))
            {
                SelectTestScenePath();
            }
            return testScenePath;
        }
    }

    static List<string> effectFolders = new List<string>()
    {
        "Assets/Editor/Examples/Effect",
    };

    static TestParticleEffectWnd wnd;
    //是否在运行状态下测试
    static bool bTestInPlayMode = true;
    bool m_RecordLog = false;
    bool m_bPauseOnError = false;

    TestEffectQuality testQuality = TestEffectQuality.All;
    TestEffectQuality currentTestQuality = TestEffectQuality.High;
    TestType testType = TestType.默认目录;

    int testIndex = 0;
    int totalEffectNum = 0;
    List<string> effectList;

    bool bPreviewEffect = false;
    bool bTesting = false;
    bool bPause = false;
    bool bFinished = false;
    bool bFinishedRefresh = false;

    #region 当前正在测试的特效数据
    public AnimationCurve 粒子数量;
    public AnimationCurve DrawCall;
    public AnimationCurve Overdraw;

    //当前特效播放时长
    float m_Duration;

    //特效运行时间
    [Range(0, 10)]
    public int m_TestDuration = 3;
    bool m_bOnlyLoopDuration = true;

    EffectEvla m_EffectEvla;
    ParticleSystem[] m_ParticleSystems;
    Animator[] m_Animators;
    Animation[] m_Animations;
    Renderer[] m_Renderers;
    MethodInfo m_CalculateEffectUIDataMethod;
    int m_ParticleCount = 0;
    int m_MaxParticleCount = 0;
    int m_MaxDrawCall = 0;
    int m_CullingSupport = 0;
    float m_CurTestingTime = 0;

    bool m_bInActiveNode = false;
    bool m_bParticleMultiMats = false;
    bool m_bInValidParticle = false;
    bool m_bInValidRenderer = false;
    bool m_bInValidAnimator = false;
    bool m_bRendererLostMat = false;
    bool m_bHigherQualityNode = false;
    int m_MeshTriangles = 0;

    GameObject m_CurEffectAsset;
    GameObject m_CurrentEffect;
    ParticleEffectCurve m_CurveParticleCount;
    ParticleEffectCurve m_CurveDrawCallCount;
    ParticleEffectCurve m_CurveOverdraw;
    #endregion

    static bool RunToTestSceneOrSetTestPending(TestType testType, TestEffectQuality testQuality, bool bForce = false)
    {
        if (string.IsNullOrEmpty(TestScenePath)) return false;
        if(EditorApplication.isPlaying && !EditorSceneManager.GetActiveScene().path.ToLower().Replace("\\", "/").Equals(TestScenePath.ToLower().Replace("\\", "/")))
        {
            if(bForce || EditorUtility.DisplayDialog("提示", "本次测试需要在特定场景进行,是否立即跳转？", "是", "否"))
            {
                EditorPrefs.SetBool("TestParticleEffectPending_OpenTestScene", true);
                EditorPrefs.SetInt("TestParticleEffectPending_TestType", (int)testType);
                EditorPrefs.SetInt("TestParticleEffectPending_TestQuality", (int)testQuality);
                EditorApplication.ExitPlaymode();
            }
            return false;
        }
        if((bTestInPlayMode && EditorApplication.isPlaying == false) || !EditorSceneManager.GetActiveScene().path.ToLower().Replace("\\", "/").Equals(TestScenePath.ToLower().Replace("\\", "/")))
        {
            string msg = string.Empty;
            if(!bForce)
            {
                if (bTestInPlayMode && !EditorSceneManager.GetActiveScene().path.ToLower().Replace("\\", "/").Equals(TestScenePath.ToLower().Replace("\\", "/")))
                {
                    msg = "本次测试需要在特定场景，并且在编辑器运行状态下进行，是否立即跳转场景并运行？";
                }
                else if(bTestInPlayMode)
                {
                    msg = "本次测试需要在编辑器运行状态下进行，是否立即启动运行？";
                }
                else
                {
                    msg = "本次测试需要在特定场景下进行，是否立即跳转？";
                }
            }
            if(bForce || EditorUtility.DisplayDialog("提示", msg, "是", "否"))
            {
                if(!EditorSceneManager.GetActiveScene().path.ToLower().Replace("\\", "/").Equals(TestScenePath.ToLower().Replace("\\", "/")))
                {
                    EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                    EditorSceneManager.OpenScene(TestScenePath);
                }
                if(bTestInPlayMode)
                {
                    EditorPrefs.SetInt("TestParticleEffectPending_TestType", (int)testType);
                    EditorPrefs.SetInt("TestParticleEffectPending_TestQuality", (int)testQuality);
                    EditorApplication.EnterPlaymode();
                }
                return !bTestInPlayMode;
            }
            return false;
        }
        return true;
    }

    static void CreateTestParticleEffectWnd()
    {
        if(wnd)
        {
            wnd.Close();
        }
        GetWindow<TestParticleEffectWnd>("ParticleEffectProfiler");
        if(wnd != null)
        {
            wnd.minSize = new Vector2(300, 300);
            wnd.Show();
        }
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if(state == PlayModeStateChange.EnteredPlayMode)
        {
            CheckTestEffectsPending();
            //EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }
        else if(state == PlayModeStateChange.EnteredEditMode)
        {
            if(!bTesting)
            {
                CheckExitPlayAndSwitchScenePending();
            }
            else
            {
                bTesting = false;
                bPause = false;
                bFinished = false;
                Repaint();
            }
            //EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }
    }

    private void CheckExitPlayAndSwitchScenePending()
    {
        if (!EditorPrefs.HasKey("TestParticleEffectPending_OpenTestScene")) return;
        bool exitPlaymodeAndSwitchScene = EditorPrefs.GetBool("TestParticleEffectPending_OpenTestScene", false);
        if (exitPlaymodeAndSwitchScene)
        {
            testType = (TestType)EditorPrefs.GetInt("TestParticleEffectPending_TestType", 0);
            testQuality = (TestEffectQuality)EditorPrefs.GetInt("TestParticleEffectPending_TestQuality", 0);
            EditorPrefs.DeleteKey("TestParticleEffectPending_OpenTestScene");
            EditorPrefs.DeleteKey("TestParticleEffectPending_TestType");
            EditorPrefs.DeleteKey("TestParticleEffectPending_TestQuality");
            RunToTestSceneOrSetTestPending(testType, testQuality, true);
        }
    }

    private void CheckTestEffectsPending()
    {
        if (!EditorPrefs.HasKey("TestParticleEffectPending_TestType")) return;
        testType = (TestType)EditorPrefs.GetInt("TestParticleEffectPending_TestType", 0);
        testQuality = (TestEffectQuality)EditorPrefs.GetInt("TestParticleEffectPending_TestQuality", 0);
        EditorPrefs.DeleteKey("TestParticleEffectPending_TestType");
        EditorPrefs.DeleteKey("TestParticleEffectPending_TestQuality");
        switch (testType)
        {
            case TestType.默认目录:
                OnTestDefaultFolderEffects();
                break;
            case TestType.选中目录:
                OnTestSelectedFolderEffects();
                break;
            case TestType.选中特效:
                OnTestSelectedEffects();
                break;
            default:
                break;
        }
    }

    private static float GetParticleDelay(ParticleSystem particle)
    {
        var mode = particle.main.startDelay.mode;
        switch (mode)
        {
            case ParticleSystemCurveMode.Constant:
                return particle.main.startDelay.constant;
            case ParticleSystemCurveMode.TwoConstants:
                return particle.main.startDelay.constantMax;
            case ParticleSystemCurveMode.Curve:
                return particle.main.startDelay.curve.length;
            case ParticleSystemCurveMode.TwoCurves:
                return particle.main.startDelay.curveMax.length;
        }
        return 0;
    }

    private static float GetParticleLifeTime(ParticleSystem particle)
    {
        var mode = particle.main.startLifetime.mode;
        switch (mode)
        {
            case ParticleSystemCurveMode.Constant:
                return particle.main.startLifetime.constant;
            case ParticleSystemCurveMode.TwoConstants:
                return particle.main.startLifetime.constantMax;
            case ParticleSystemCurveMode.Curve:
                return particle.main.startLifetime.curve.length;
            case ParticleSystemCurveMode.TwoCurves:
                return particle.main.startLifetime.curveMax.length;
        }
        return 0;
    }

    private static float GetParticleLastEmissionTime(ParticleSystem particle)
    {
        var mode = particle.emission.rateOverTime.mode;
        float rate = 0;
        switch (mode)
        {
            case ParticleSystemCurveMode.Constant:
                rate = particle.emission.rateOverTime.constant;
                break;
            case ParticleSystemCurveMode.TwoConstants:
                rate = particle.emission.rateOverTime.constantMax;
                break;
            case ParticleSystemCurveMode.Curve:
                rate = particle.emission.rateOverTime.curve.length;
                break;
            case ParticleSystemCurveMode.TwoCurves:
                rate = particle.emission.rateOverTime.curveMax.length;
                break;
        }
        if(rate > 0)
        {
            return particle.main.duration;
        }
        mode = particle.emission.rateOverDistance.mode;
        float ratedistance = 0;
        switch (mode)
        {
            case ParticleSystemCurveMode.Constant:
                ratedistance = particle.emission.rateOverDistance.constant;
                break;
            case ParticleSystemCurveMode.TwoConstants:
                ratedistance = particle.emission.rateOverDistance.constantMax;
                break;
            case ParticleSystemCurveMode.Curve:
                ratedistance = particle.emission.rateOverDistance.curve.length;
                break;
            case ParticleSystemCurveMode.TwoCurves:
                ratedistance = particle.emission.rateOverDistance.curveMax.length;
                break;
        }
        if (ratedistance > 0)
        {
            return particle.main.duration;
        }

        int burstCount = particle.emission.burstCount;
        if(burstCount > 0)
        {
            float time = 0;
            for(int i=0; i < burstCount; i++)
            {
                var burst = particle.emission.GetBurst(i);
                time = Math.Max(time, burst.time);
            }
            return time;
        }
        return 0;
    }
    
    private static float GetParticleDuration(ParticleSystem particle)
    {
        float delay = GetParticleDelay(particle);
        float lifeTime = GetParticleLifeTime(particle);
        float lastEmissitonTime = GetParticleLastEmissionTime(particle);
        return delay + lastEmissitonTime + lifeTime;
    }

    private static float GetEffectDuration(GameObject effect, TestEffectQuality quality)
    {
        float duration = 0;
        var particles = GetComponentsByQuality<ParticleSystem>(effect, quality);
        bool bNotAsset = string.IsNullOrEmpty(AssetDatabase.GetAssetPath(effect));
        if (particles != null && particles.Length > 0)
        {
            foreach(var p in particles)
            {
                if(p.main.loop)
                {
                    duration = -1;
                    break;
                }
                else
                {
                    duration = Math.Max(duration, GetParticleDuration(p));
                }
            }
        }
        if(duration >= 0)
        {
            var animators = GetComponentsByQuality<Animator>(effect, quality);
            if(animators!= null && animators.Length > 0)
            {
                foreach(var anim in animators)
                {
                    var clips = anim.runtimeAnimatorController.animationClips;
                    if(clips != null && clips.Length > 0)
                    {
                        for(int i=0; i < clips.Length; i++)
                        {
                            var clip = clips[i];
                            if(clip != null)
                            {
                                if(clip.isLooping)
                                {
                                    duration = -1;
                                    break;
                                }
                                else
                                {
                                    duration = Math.Max(duration, clip.length / anim.speed);
                                }
                            }
                        }
                    }
                    if (duration < 0) break;
                }
            }
        }
        if (duration >= 0)
        {
            var animations = GetComponentsByQuality<Animation>(effect, quality);
            if (animations != null && animations.Length > 0)
            {
                foreach (var anim in animations)
                {
                    var clipInfo = anim.clip;
                    if (clipInfo.isLooping)
                    {
                        duration = -1;
                        break;
                    }
                    else
                    {
                        duration = Math.Max(duration, clipInfo.length);
                    }
                }
            }
        }
        return duration;
    }
    private float TestDuration
    {
        get
        {
            if (!m_bOnlyLoopDuration || m_Duration < 0) return m_TestDuration;
            return m_Duration;
        }
    }
    private TestEffectQuality GetEffectQuality(string effectPath)
    {
        
        if (effectPath.Contains("_low")) return TestEffectQuality.Low;
        if (effectPath.Contains("_middle")) return TestEffectQuality.Middle;
        return TestEffectQuality.High;
    }


    private static TestEffectQuality GetEffectNodeQuality(Transform node, out bool bOnly)
    {
        string nodeName = node.name;
        bOnly = nodeName.Contains("_only");
        if (nodeName.EndsWith("_low") || nodeName.EndsWith("_low_only")) return TestEffectQuality.Low;
        if (nodeName.EndsWith("_middle") || nodeName.EndsWith("_middle_only")) return TestEffectQuality.Middle;
        if (nodeName.EndsWith("_high") || nodeName.EndsWith("_high_only")) return TestEffectQuality.High;
        return TestEffectQuality.All;
    }

    private bool CheckEffectQualityLayerInclude(GameObject effect, TestEffectQuality quality)
    {
        if (quality < TestEffectQuality.High || quality > TestEffectQuality.Low) return false;
        Transform[] transforms = effect.GetComponentsInChildren<Transform>(true);
        foreach(var trans in transforms)
        {
            var _quality = GetEffectNodeQuality(trans, out bool bOnly);
            if(_quality == quality) return true;
        }
        return false;
    }

    private void SetEffectQualityLayer(GameObject effect, TestEffectQuality quality)
    {
        if (quality == TestEffectQuality.All) return;
        Transform[] transforms = effect.GetComponentsInChildren<Transform>();
        foreach(var trans in transforms)
        {
            bool bOnly = false;
            var _quality = GetEffectNodeQuality(trans, out bOnly);
            if ((_quality == TestEffectQuality.All) || (_quality == TestEffectQuality.Low &&!bOnly)) return;
            bool bShow = (_quality == quality) || (_quality > quality && !bOnly);
            var renderer = trans.GetComponent<Renderer>();
            var particleSystem = trans.GetComponent<ParticleSystem>();
            var animator = trans.GetComponent<Animator>();
            var animation = trans.GetComponent<Animation>();
            if(renderer != null)
            {
                renderer.enabled = bShow;
            }
            if(particleSystem != null)
            {
                if(!bShow && bTestInPlayMode)
                    particleSystem.Stop();
            }
            if(animator != null)
            {
                animator.enabled = bShow;
            }
            if(animation != null)
            {
                animation.enabled = bShow;
                if(!bShow && bTestInPlayMode)
                    animation.Stop();
            }
        }
    }

    private static T[] GetComponentsByQuality<T>(GameObject effect, TestEffectQuality quality) where T : Component
    {
        return effect.GetComponentsInChildren<T>(true).Where(c =>
        {
            bool bOnly;
            TestEffectQuality _quality = GetEffectNodeQuality(c.transform, out bOnly);
            return (_quality == TestEffectQuality.All) || (_quality == quality) || (_quality > quality && !bOnly);
        }).ToArray();
    }

    private void OnStartTestOneEffect(string prefabPath)
    {
        string currentPath = m_EffectEvla.GetEffectEvlaData().effectPath;
        Application.targetFrameRate = ParticleEffectCurve.FPS;
        m_SimulateTime = 0;
        m_CurTestingTime = 0;

        m_MaxParticleCount = 0;
        m_MaxDrawCall = 0;
        m_CullingSupport = 1;
        m_CurveParticleCount.Clear();
        m_CurveDrawCallCount.Clear();
        m_CurveOverdraw.Clear();
        m_EffectEvla.Clear();

        m_Duration = 0;
        m_bInActiveNode = false;
        m_bInValidAnimator = false;
        m_bInValidParticle = false;
        m_bInValidRenderer = false;
        m_bParticleMultiMats = false;
        m_bRendererLostMat = false;
        m_bHigherQualityNode = false;
        m_MeshTriangles = 0;
        
        if(m_CurrentEffect != null)
        {
            DestroyImmediate(m_CurrentEffect);
            m_Animators = null;
            m_ParticleSystems = null;
            m_CurEffectAsset = null; 
            m_CurrentEffect= null;
        }

        m_CurEffectAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if(m_CurrentEffect == null)
        {
            m_CurrentEffect = PrefabUtility.InstantiatePrefab(m_CurEffectAsset) as GameObject;
        }
        if(m_CurrentEffect != null)
        {
            if(!bTestInPlayMode)
            {
                m_CurrentEffect.hideFlags = HideFlags.DontSave;
            }

            m_EffectEvla.SetEffectPath(prefabPath);
            m_EffectEvla.SetQuality(currentTestQuality);
            m_Duration = GetEffectDuration(m_CurrentEffect, currentTestQuality);
            m_ParticleSystems = GetComponentsByQuality<ParticleSystem>(m_CurrentEffect, currentTestQuality);
            m_Animators = GetComponentsByQuality<Animator>(m_CurrentEffect, currentTestQuality);
            m_Animations = GetComponentsByQuality<Animation>(m_CurrentEffect, currentTestQuality);
            m_Renderers = GetComponentsByQuality<Renderer>(m_CurrentEffect, currentTestQuality);
            if (!bPreviewEffect)
            {
                CheckAllNode();
                //CheckParticleSystemInfo();
                //CheckAnimatorInfo();
                //CheckRendererInfo();
                if(m_bPauseOnError)
                {
                    if(m_bInActiveNode || m_bInValidParticle || m_bInValidAnimator || m_bInValidRenderer || m_bRendererLostMat || m_bParticleMultiMats || m_bHigherQualityNode || Mathf.Approximately(m_Duration, 0))
                    {
                        bPause = true;
                    }
                }
            }
            SetEffectQualityLayer(m_CurrentEffect, currentTestQuality);
        }
    }

    HashSet<Mesh> meshMap = new HashSet<Mesh>();
    private void CheckAllNode()
    {
        meshMap.Clear();
        var trans = m_CurrentEffect.GetComponentsInChildren<Transform>(true);
        if (trans == null || trans.Length == 0) return;
        var fileQuality = GetEffectQuality(effectList[testIndex]);
        for(int i=0; i < trans.Length; i++)
        {
            Transform node = trans[i];
            if (!node.gameObject.activeSelf)
            {
                m_bInActiveNode = true;
                Debug.LogError("存在未激活节点:" + m_CurrentEffect.name + " -> " + node.gameObject.name, node.gameObject);
            }
            var nodeQuality = GetEffectNodeQuality(node, out _);
            if(nodeQuality != TestEffectQuality.All && nodeQuality < fileQuality)
            {
                m_bHigherQualityNode = true;
                Debug.LogError("节点质量分级高于文件质量分级，请检查！" + m_CurrentEffect.name + " -> " + node.gameObject.name, node.gameObject);
            }
            CheckNodeParticleSystemInfo(node);
            CheckNodeAnimationInfo(node);
            CheckNodeAnimatorInfo(node);
            CheckNodeRendererInfo(node);
        }
    }

    private void CheckNodeParticleSystemInfo(Transform node)
    {
        //if (m_ParticleSystems == null || m_ParticleSystems.Length <= 0) return;
        //foreach(var particle in m_ParticleSystems)
        //{
            var particle = node.GetComponent<ParticleSystem>();
            if (particle == null) return;

            if (bTestInPlayMode)
                particle.Play();

            if(!particle.emission.enabled)
            {
                m_bInValidParticle = true;
                Debug.LogError("粒子节点Emission未激活:" + m_CurrentEffect.name + " -> " + particle.gameObject.name, particle.gameObject);
            }

            var r = particle.GetComponent<ParticleSystemRenderer>();
            if (r != null &&(r.sharedMaterials.Length - (r.trailMaterial==null?0:1) > 1))
            {
                m_bParticleMultiMats= true;
                Debug.LogError("粒子节点存在多个材质球,请检查！材质数量：" + r.sharedMaterials.Length + " " + m_CurrentEffect.name + " -> " + particle.gameObject.name, particle.gameObject);
            }
        //}
    }

    private void CheckNodeAnimationInfo(Transform node)
    {
        var anim = node.GetComponent<Animation>();
        if (anim == null) return;
        if(!anim.enabled)
        { 
            m_bInValidAnimator = true;
            Debug.LogError("动画节点未激活:" + m_CurrentEffect.name + " -> " + anim.gameObject.name, anim.gameObject);
        }
        if(anim.clip == null)
        {
            m_bInValidAnimator = true;
            Debug.LogError("动画节点缺失动画片段:" + m_CurrentEffect.name + " -> " + anim.gameObject.name, anim.gameObject);
        }
    }
    private void CheckNodeAnimatorInfo(Transform node)
    {
        //if (m_Animators == null || m_Animators.Length <= 0) return;
        //foreach(var anim in m_Animators)
        //{
            var anim = node.GetComponent<Animator>();
            if (anim == null) return;
            if (!anim.enabled)
            {
                m_bInValidAnimator = true;
                Debug.LogError("动画节点未激活:" + m_CurrentEffect.name + " -> " + anim.gameObject.name, anim.gameObject);
            }
            if(anim.runtimeAnimatorController == null)
            {
                m_bInValidAnimator = true;
                Debug.LogError("动画节点缺失Controller:" + m_CurrentEffect.name + " -> " + anim.gameObject.name, anim.gameObject);
            }
            else
            {
                var clips = anim.runtimeAnimatorController.animationClips;
                if(clips == null || clips.Length <= 0)
                {
                    m_bInValidAnimator = true;
                    Debug.LogError("动画节点缺失动画片段:" + m_CurrentEffect.name + " -> " + anim.gameObject.name, anim.gameObject);
                }
                else
                {
                    for(int i=0;i<clips.Length;i++)
                    {
                        if (clips[i] == null)
                        {
                            m_bInValidAnimator = true;
                            Debug.LogError("动画节点缺失动画片段:" + m_CurrentEffect.name + " -> " + anim.gameObject.name, anim.gameObject);
                        }
                    }
                }
            }
        //}
    }
    private void CheckNodeRendererInfo(Transform node)
    {
        //if (m_CurrentEffect == null) return;
        //var renderers = m_CurrentEffect.GetComponentsInChildren<Renderer>();
        //if (renderers == null || renderers.Length <= 0) return;
        
        //foreach(var r in renderers)
        //{
            var r = node.GetComponent<Renderer>();
            if (r == null) return;
            if (!r.enabled)
            {
                m_bInValidRenderer = true;
                Debug.LogError("Renderer节点未激活:" + m_CurrentEffect.name + " -> " + r.gameObject.name, r.gameObject);
            }

            Material[] mats = r.sharedMaterials;
            if(mats == null || mats.Length <= 0)
            {
                m_bRendererLostMat = true;
                Debug.LogError("Renderer节点缺失材质球:" + m_CurrentEffect.name + " -> " + r.gameObject.name, r.gameObject);
            }
            else
            {
                for(int i=0; i<mats.Length;i++)
                {
                    if (mats[i] == null)
                    {
                        m_bRendererLostMat = true;
                        Debug.LogError(string.Format("Renderer节点缺失材质球,材质数量{0},缺失第{1}个 {2} -> {3}", mats.Length, i, m_CurrentEffect.name, r.gameObject.name), r.gameObject);
                    }
                }
            }

            if(r is SkinnedMeshRenderer || r is MeshRenderer)
            {
                Mesh mesh = null;
                if (r is SkinnedMeshRenderer)
                {
                    mesh = (r as SkinnedMeshRenderer).sharedMesh;
                }
                else
                {
                    MeshFilter filter = r.GetComponent<MeshFilter>();
                    if (filter != null)
                    {
                        mesh = filter.sharedMesh;
                    }
                }
                if(mesh != null)
                {
                    if(meshMap.Add(mesh))
                    {
                        m_MeshTriangles += mesh.triangles.Length / 3;
                    }
                }
                else
                {
                    m_bInValidRenderer = true;
                    Debug.LogError("Renderer节点缺失Mesh" + m_CurrentEffect.name + " -> " + r.gameObject.name, r.gameObject);
                }
            }
        //}
    }

    private void OnEnable()
    {
        wnd = this;
        bTestInPlayMode = EditorApplication.isPlaying;
        bTesting = false;
        bPause = false;
        bFinished = false;
        bFinishedRefresh = false;
        m_ParticleSystems = null;
        m_Animators = null;
        m_Animations = null;
        m_Renderers = null;
        m_CurEffectAsset = null;
        if(m_CurrentEffect != null)
        {
            DestroyImmediate(m_CurrentEffect);
            m_CurrentEffect = null;
        }
        string effectFolderStr = EditorPrefs.GetString("ParticleEffectProfiler_EffectFolder");
        if (!string.IsNullOrEmpty(effectFolderStr))
        {
            effectFolders.Clear();
            effectFolders.AddRange(effectFolderStr.Split(';'));
        }
        //testType = TestType.默认目录;
        //testQuality = TestEffectQuality.All;
        //if(bTestInPlayMode)
        //{
        //    if(!EditorApplication.isPlaying || !EditorSceneManager.GetActiveScene().path.Equals(TestScenePath))
        //    {
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
        //    }
        //}
        SceneView.duringSceneGui += DuringSceneGUI;
    }

    private void OnDisable()
    {
        //EditorPrefs.DeleteKey("TestParticleEffectPending");
        SceneView.duringSceneGui -= DuringSceneGUI;
    }

    private void OnDestroy()
    {
        wnd = null;
        if(effectList != null)
        {
            effectList.Clear();
            effectList = null;
        }
        if(meshMap != null)
        {
            meshMap.Clear();
            meshMap = null;
        }
        
        EditorPrefs.DeleteKey("TestParticleEffectPending_TestType");
        EditorPrefs.DeleteKey("TestParticleEffectPending_TestQuality");
        EditorPrefs.DeleteKey("TestParticleEffectPending_OpenTestScene");
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        SceneView.duringSceneGui -= DuringSceneGUI;
    }

    Vector2 cullingTipScrollPos = Vector2.zero;
    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button("选择测试场景", GUILayout.Width(120)))
        {
            SelectTestScenePath();
        }
        GUILayout.TextField(TestScenePath);
        EditorGUILayout.EndHorizontal();

        int count = effectFolders.Count;
        GUILayout.Label("默认特效目录:", GUILayout.Width(120));
        for(int i=0; i< count;i++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.TextField(effectFolders[i]);
            if(GUILayout.Button("删除", GUILayout.Width(50)))
            {
                if(!bTesting)
                {
                    effectFolders.RemoveAt(i);
                    count--;
                    if (count <= 0)
                    {
                        EditorPrefs.DeleteKey("ParticleEffectProfiler_EffectFolder");
                    }
                    else
                    {
                        EditorPrefs.SetString("ParticleEffectProfiler_EffectFolder", string.Join(";", effectFolders.ToArray()));
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("添加特效目录"))
        {
            AddEffectFolder();
        }
        GUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("运行模式下测试:", GUILayout.Width(90));
        var _bTestInPlayMode = EditorGUILayout.Toggle(bTestInPlayMode, GUILayout.Width(18)) || EditorApplication.isPlaying;
        GUILayout.Label("记录日志:", GUILayout.Width(55));
        m_RecordLog = EditorGUILayout.Toggle(m_RecordLog, GUILayout.Width(15));
        GUILayout.Label("报错自动暂停", GUILayout.Width(80));
        m_bPauseOnError = EditorGUILayout.Toggle(m_bPauseOnError, GUILayout.Width(15));
        GUILayout.Label("选择测试类型", GUILayout.Width(80));
        var _testType = (TestType)EditorGUILayout.EnumPopup(testType, GUILayout.MinWidth(70));
        GUILayout.Label("选择测试质量", GUILayout.Width(80));
        var _testQuality = (TestEffectQuality)EditorGUILayout.EnumPopup(testQuality);
        GUILayout.Label("特效运行时间:", GUILayout.Width(90));
        m_TestDuration = EditorGUILayout.IntSlider(m_TestDuration, 0, 10, GUILayout.MaxWidth(120), GUILayout.MinWidth(105));
        GUILayout.Label("仅用于循环特效:", GUILayout.Width(90));
        m_bOnlyLoopDuration = EditorGUILayout.Toggle(m_bOnlyLoopDuration, GUILayout.Width(15));
        if (!bTesting)
        {
            bTestInPlayMode = _bTestInPlayMode;
            testType = _testType;
            testQuality = _testQuality;
        }else if(bTestInPlayMode != _bTestInPlayMode || testType != _testType || testQuality != _testQuality)
        {
            //EditorUtility.DisplayDialog("提示", "当前测试正在进行中", "确定");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button("预览特效", GUILayout.Height(30)))
        {
            if(!bTesting || bPreviewEffect)
            {
                bPreviewEffect = true;
                bTesting = false;
                bPause = false;
                if(!EditorApplication.isPlaying)
                {
                    bTestInPlayMode = false;
                }
                OnTestSelectedEffects();
            }
        }

        if(!bTesting)
        {
            if(GUILayout.Button("开始测试", GUILayout.Height(30)))
            {
                bPreviewEffect = false;
                if(testType == TestType.默认目录)
                {
                    OnTestDefaultFolderEffects();
                }
                else if(testType == TestType.选中目录)
                {
                    OnTestSelectedFolderEffects();
                }else if(testType == TestType.选中特效)
                {
                    OnTestSelectedEffects();
                }
            }
        }
        else
        {
            if(GUILayout.Button(bPause?"继续":"暂停", GUILayout.Height(30)))
            {
                bPause = !bPause;
            }
        }

        if(GUILayout.Button("停止", GUILayout.Height(30)))
        {
            bTesting = false;
            bFinished = false;
            bPause = false;
            bPreviewEffect = false;
        }
        EditorGUILayout.EndHorizontal(); 

        if(m_CurrentEffect != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("当前测试特效:", GUILayout.Width(80));
            EditorGUILayout.ObjectField(m_CurEffectAsset, typeof(GameObject), false, GUILayout.MaxWidth(150));
            if(GUILayout.Button("应用修改", GUILayout.Width(120)))
            {
                if(PrefabUtility.GetPrefabInstanceStatus(m_CurrentEffect) == PrefabInstanceStatus.Connected)
                {
                    HideFlags flags = m_CurrentEffect.hideFlags;
                    if(flags == HideFlags.DontSave)
                    {
                        m_CurrentEffect.hideFlags = HideFlags.None;
                    }

                    PrefabUtility.ApplyPrefabInstance(m_CurrentEffect, InteractionMode.UserAction);
                    m_CurrentEffect.hideFlags = flags;
                }
            }
            GUILayout.Space(10);
            if(m_Duration < 0)
            {
                GUILayout.Label("特效时长:循环", GUILayout.Width(90));
            }
            else
            {
                GUILayout.Label(string.Format("特效时长:{0}", m_Duration.ToString("F2")), GUILayout.Width(100));
            }

            GUILayout.Label(string.Format("进度:{0}/{1}...运行时间:{2}", testIndex, totalEffectNum, m_CurTestingTime.ToString("F2")), GUILayout.MaxWidth(180));
            float leftSec = Math.Max(0, (totalEffectNum - testIndex) * 1 + TestDuration - m_CurTestingTime);
            GUILayout.Label(string.Format("预计剩余:{0}:{1}:{2}", ((int)leftSec / 3600).ToString("D2"), ((int)leftSec % 3600).ToString("D2"), (leftSec % 60).ToString("F2")), GUILayout.MaxWidth(130));
            EditorGUILayout.EndHorizontal();
            if(!bPreviewEffect)
            {
                if (粒子数量 != null)
                {
                    GUILayout.Label("粒子数量:" + m_MaxParticleCount);
                    EditorGUILayout.CurveField(粒子数量, GUILayout.Height(50));
                }
                if(DrawCall != null)
                {
                    GUILayout.Label("DrawCall:");
                    EditorGUILayout.CurveField(DrawCall, GUILayout.Height(50));
                }
                if (Overdraw != null)
                {
                    GUILayout.Label("OverDraw:");
                    EditorGUILayout.CurveField(Overdraw, GUILayout.Height(50));
                }

                if(m_ParticleSystems != null && m_ParticleSystems.Length > 0)
                {
                    GUILayout.Label("ParticleSystem以下选项会导致粒子离开相机视野后无法自动剔除:", EditorStyles.largeLabel);
                    cullingTipScrollPos = EditorGUILayout.BeginScrollView(cullingTipScrollPos, GUILayout.MaxHeight(300));
                    m_CullingSupport = 1;
                    foreach(ParticleSystem item in m_ParticleSystems)
                    {
                        string str = GetParticleEffectData.CheckCulling(item, "    ");
                        if (!string.IsNullOrEmpty(str))
                        {
                            EditorGUILayout.ObjectField(item, typeof(ParticleSystem), true, GUILayout.MaxWidth(300));
                            EditorGUILayout.HelpBox(str, MessageType.Warning);
                            m_CullingSupport = 0;
                        }
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
        }
        if(bFinished || bPause)
        {
            GUILayout.Space(10);
            GUIStyle customStyle = new GUIStyle(GUI.skin.label);
            customStyle.normal.textColor = Color.red;
            customStyle.alignment = TextAnchor.MiddleCenter;
            string title = bPreviewEffect ? "预览" : "测试";
            if(bFinished)
            {
                GUILayout.Label(title + "结束", customStyle);
            }
            else if(bPause)
            {
                GUILayout.Label(title + "暂停", customStyle);
            }
        }
    }

    string[] m_Labels = new string[20];
    void DuringSceneGUI(SceneView sceneView)
    {
        if ((bTestInPlayMode && !EditorApplication.isPlaying) || !(bTesting || bFinished) || bPreviewEffect || m_CurrentEffect == null) return;
        var effectEvlaData = GetEffectEvlaData();
        TestEffectQuality quality = effectEvlaData.quality;
        int index = 0;
        m_Labels[index] = GetParticleEffectData.GetEffectQualityStr(effectEvlaData.quality);
        m_Labels[++index] = GetParticleEffectData.GetEffectDurationStr(m_Duration, quality);

        m_Labels[++index] = GetParticleEffectData.GetInActiveNodeStr(m_bInActiveNode);
        m_Labels[++index] = GetParticleEffectData.GetInvalidNodeStr(m_bInValidParticle, m_bInValidRenderer, m_bInValidAnimator);
        m_Labels[++index] = GetParticleEffectData.GetLostMaterialStr(m_bRendererLostMat);
        m_Labels[++index] = GetParticleEffectData.GetParticleSystemMultiMatsStr(m_bParticleMultiMats);
        m_Labels[++index] = GetParticleEffectData.GetHigherQualityNodeStr(m_bHigherQualityNode);

        m_Labels[++index] = GetParticleEffectData.GetParticleSystemCountStr(m_ParticleSystems.Length, quality);
        m_Labels[++index] = GetParticleEffectData.GetParticleCountStr(quality, GetParticleCount(), GetMaxParticleCount());
        m_Labels[++index] = GetParticleEffectData.GetOnlyParticleEffecDrawCallStr(quality, m_MaxDrawCall);
        m_Labels[++index] = GetParticleEffectData.GetMeshTriangleCountStr(m_MeshTriangles, quality);
        m_Labels[++index] = GetParticleEffectData.GetGetRuntimeMemorySizeStr(m_Renderers, quality);
        m_Labels[++index] = GetParticleEffectData.GetPixDrawAverageStr(effectEvlaData.GetPixDrawAverage());
        m_Labels[++index] = GetParticleEffectData.GetPixActualDrawAverageStr(effectEvlaData.GetPixActualDrawAverage());
        m_Labels[++index] = GetParticleEffectData.GetPixRateStr(quality, effectEvlaData.GetPixRate());
        m_Labels[++index] = GetParticleEffectData.GetCullingSupportedStr(m_CullingSupport);
        ShowUI();
    }

    void ShowUI()
    {
        //开始绘制GUI
        Handles.BeginGUI();
        //规定GUI显示区域
        GUILayout.BeginArea(new Rect(Screen.width - 400, Screen.height - m_Labels.Length * 15, 400, m_Labels.Length * 15));
        GUIStyle style = new GUIStyle();
        style.richText = true;
        style.fontStyle = FontStyle.Bold;
        for(int i=0;i<m_Labels.Length;i++)
        {
            if (!string.IsNullOrEmpty(m_Labels[i]))
            {
                GUILayout.Label(m_Labels[i], style);
            }
        }
        GUILayout.EndArea();

        Handles.EndGUI(); 
    }

    void OnTestSelectedEffects()
    {
        var selects = Selection.gameObjects;
        if (selects == null || selects.Length == 0) return;
        if (!RunToTestSceneOrSetTestPending(TestType.选中特效, testQuality)) return;

        effectList = new List<string>();
        for(int i=0; i < selects.Length; i++)
        {
            string path = AssetDatabase.GetAssetPath(selects[i]);
            if (!string.IsNullOrEmpty(path))
            {
                if(testQuality != TestEffectQuality.All)
                {
                    var quality = GetEffectQuality(path);
                    //if (testQuality != quality) continue;
                    if (quality > testQuality) continue;
                    if (testQuality != quality && !CheckEffectQualityLayerInclude(selects[i], testQuality)) continue;
                }
                effectList.Add(path);
            }
        }
        if(effectList.Count > 0)
        {
            InitData();
            bTesting = true;
        }
    }

    void OnTestSelectedFolderEffects()
    {
        var obj = Selection.activeObject;
        if (obj == null) return;
        string folder = AssetDatabase.GetAssetPath(obj);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(Application.dataPath.Replace("Assets", "") + folder)) return;
        if (!RunToTestSceneOrSetTestPending(TestType.选中目录, testQuality)) return;
        OnTestEffectsByFolderAndQuality(folder, TestType.选中目录, testQuality);
        if (effectList.Count > 0)
        {
            InitData();
            bTesting = true;
        }
    }

    void OnTestDefaultFolderEffects()
    {
        if (effectFolders == null || effectFolders.Count <= 0) return;
        if (!RunToTestSceneOrSetTestPending(TestType.默认目录, testQuality)) return;
        for(int i=0;i<effectFolders.Count;i++)
        {
            OnTestEffectsByFolderAndQuality(effectFolders[i], TestType.默认目录, testQuality);
        }
        if(effectList.Count > 0)
        {
            InitData();
            bTesting = true;
        }
    }

    void OnTestEffectsByFolderAndQuality(string folder, TestType testType, TestEffectQuality quality)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new string[] { folder });
        effectList = new List<string>(guids.Length);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || path.EndsWith(".meta")) continue;
            if(quality != TestEffectQuality.All)
            {
                var _quality = GetEffectQuality(path);
                //if (quality != _quality) continue;
                if (_quality > quality) continue;
                if(quality != _quality)
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if(!CheckEffectQualityLayerInclude(go, quality))
                    {
                        continue;
                    }
                }
            }
            
            effectList.Add(path);
        }
    }

    private float m_SimulateTime = 0;
    private void Update()
    {
        if (bTestInPlayMode && !EditorApplication.isPlaying) return;
        if(bFinishedRefresh)
        {
            bFinishedRefresh = false;
            Repaint();
        }
        if (!(bTesting || (bFinished && m_Duration < 0))) return;
        //if(effectList == null || effectList.Count == 0)
        //{
        //    Debug.LogError("No Effect Waiting For Test!");
        //    OnTestFinished();
        //   return;
        //}

        if(!bPause)
        {
            if(!bTestInPlayMode)
            {
                if(m_CurrentEffect != null && m_ParticleSystems != null)
                {
                    m_SimulateTime += Time.deltaTime;
                    foreach(var ps in m_ParticleSystems)
                    {
                        if(ps != null)
                        {
                           // Debug.LogError(bPreviewEffect + " " + bTesting);
                            ps.Simulate(m_SimulateTime);
                        }
                    }

                    foreach(var anim in m_Animators)
                    {
                        if(anim != null && anim.runtimeAnimatorController != null && anim.runtimeAnimatorController.animationClips.Length > 0)
                        {
                            for(int i=0; i<anim.runtimeAnimatorController.animationClips.Length; i++)
                            {
                                var clip = anim.runtimeAnimatorController.animationClips[i];
                                if (clip != null)
                                {
                                    clip.SampleAnimation(anim.gameObject, m_SimulateTime);
                                }
                            }
                        }
                    }
                }                
            }

            if (bTesting)
            {
                m_CurTestingTime += Time.deltaTime;
                if ((testIndex == 0 && (currentTestQuality == testQuality || currentTestQuality == GetEffectQuality(effectList[testIndex]))) || m_CurTestingTime >= TestDuration)
                {
                    if (m_CurrentEffect != null)
                    {
                        OnOneEffectTestFinished();
                    }
                    if (testIndex >= effectList.Count)
                    {
                        OnTestFinished();
                        return;
                    }
                    OnStartTestOneEffect(effectList[testIndex]);
                    if(testQuality == TestEffectQuality.All)
                    {
                        while (currentTestQuality <= TestEffectQuality.Low)
                        {
                            currentTestQuality++;
                            if (CheckEffectQualityLayerInclude(m_CurrentEffect, currentTestQuality))
                                break;
                        }
                        if (currentTestQuality > TestEffectQuality.Low)
                        {
                            testIndex++;
                            if(testIndex < effectList.Count)
                            {
                                currentTestQuality = testQuality == TestEffectQuality.All ? GetEffectQuality(effectList[testIndex]) : testQuality;;
                            }
                        }
                    }
                    else
                    {
                        testIndex++;
                    }
                }
            }
            if (m_CurrentEffect != null && m_ParticleSystems != null)
            {
                RecordParticleCount();
                m_EffectEvla.Update();

                UpdateParticleCountCurve();
                UpdateDrawCallCurve();
                UpdateOverdrawCurve();
            }

            if (!bPreviewEffect)
            {
                //强制更新界面，因为OnGUI方法不会每帧刷新
                Repaint();
            }
        }
    }

    private void OnOneEffectTestFinished()
    {
        if (m_CurrentEffect == null) return;
        if(!bPreviewEffect)
        {
            var effectEvlaData = GetEffectEvlaData();
            TestEffectQuality quality = effectEvlaData.quality;
            int textureCount = 0;
            int texturePixels = 0;
            bool bRenderLostMat = m_bRendererLostMat;
            
            int memorySize = GetParticleEffectData.GetRuntimeMemorySize(m_Renderers, quality, out textureCount, out texturePixels);
            int particleSystemCount = m_ParticleSystems.Length;

            int maxParticleCount = m_MaxParticleCount;
            int maxDrawCall = m_MaxDrawCall;
            int pixDrawAverage = effectEvlaData.GetPixDrawAverage();
            int pixActualDrawAverage = effectEvlaData.GetPixActualDrawAverage();
            int pixRate = effectEvlaData.GetPixRate();

            bool bInValidNode = m_bInValidParticle || m_bInValidRenderer || m_bInValidAnimator;
            if(m_RecordLog && !GetParticleEffectData.IsWellEffect(effectEvlaData.effectPath, quality, m_bInActiveNode, bInValidNode, bRenderLostMat, m_bParticleMultiMats, m_bHigherQualityNode,
                        m_Duration, particleSystemCount, maxParticleCount, textureCount, texturePixels, m_MeshTriangles,
                        memorySize, maxDrawCall, pixRate, m_CullingSupport))
            {
                CSVEffectEvlaHelper.GetInstance().RecordLog(effectEvlaData.effectPath, quality, m_bInActiveNode, bInValidNode, bRenderLostMat, m_bParticleMultiMats, m_bHigherQualityNode,
                        m_Duration, particleSystemCount, maxParticleCount, textureCount, texturePixels, m_MeshTriangles,
                        memorySize, maxDrawCall, pixDrawAverage, pixActualDrawAverage, pixRate, m_CullingSupport);
            }
        }
    }

    private void OnTestFinished()
    {
        if(!bPreviewEffect && m_RecordLog)
        {
            CSVEffectEvlaHelper.GetInstance().SaveLog(testQuality);
        }
        CSVEffectEvlaHelper.GetInstance().Release();
        bFinished = true;
        bFinishedRefresh = true;
        bTesting = false;
        bPause = false;
        //bPreviewEffect = false;
        effectList = null;
    }

    void InitData()
    {
        CSVEffectEvlaHelper.GetInstance().Release();
        m_SimulateTime = 0;
        m_CurTestingTime = 0;
        bFinished = false;
        bFinishedRefresh = false;
        m_CurveParticleCount = new ParticleEffectCurve();
        m_CurveDrawCallCount = new ParticleEffectCurve();
        m_CurveOverdraw = new ParticleEffectCurve();
        m_EffectEvla = new EffectEvla(Camera.main, bPreviewEffect);
        testIndex = 0;
        totalEffectNum = effectList != null ? effectList.Count : 0;
        currentTestQuality = testQuality;
        if(testQuality == TestEffectQuality.All)
        {
            currentTestQuality = totalEffectNum > 0 ? GetEffectQuality(effectList[0]) : TestEffectQuality.High;
        }
        if(m_CurrentEffect != null)
        {
            DestroyImmediate(m_CurrentEffect);
            m_CurrentEffect = null;
            m_CurEffectAsset = null;
        }
#if UNITY_2017_1_OR_NEWER
        m_CalculateEffectUIDataMethod = typeof(ParticleSystem).GetMethod("CalculateEffectUIData", BindingFlags.Instance | BindingFlags.NonPublic);

#else
        m_CalculateEffectUIDataMethod = typeof(ParticleSystem).GetMethod("CountSubEmitterParticles", BindingFlags.Instance | BindingFlags.NonPublic);
#endif
    }

    public EffectEvlaData GetEffectEvlaData()
    {
        return m_EffectEvla.GetEffectEvlaData();
    }

    public void RecordParticleCount()
    {
        m_ParticleCount = 0;
        foreach(var ps in m_ParticleSystems)
        {
            if (ps == null) continue;
            int count = 0;
#if UNITY_2017_1_OR_NEWER
            object[] invokeArgs = { count, 0.0f, Mathf.Infinity };
            m_CalculateEffectUIDataMethod.Invoke(ps, invokeArgs);
            count = (int)invokeArgs[0];
#else
            object[] invokeArgs = { count };
            m_CalculateEffectUIDataMethod.Invoke(ps, invokeArgs);
            count = (int)invokeArgs[0];
            count += ps.particleCount;
#endif
            m_ParticleCount += count;
        }
        if(m_MaxParticleCount < m_ParticleCount)
        {
            m_MaxParticleCount = m_ParticleCount; 
        }
    }

    public int GetParticleCount()
    {
        return m_ParticleCount;
    }

    public int GetMaxParticleCount()
    {
        return m_MaxParticleCount;
    }

    void UpdateParticleCountCurve()
    {
        粒子数量 = m_CurveParticleCount.UpdateAnimationCurve(m_ParticleCount, m_Duration < 0, Mathf.CeilToInt(TestDuration));
    }

    void UpdateDrawCallCurve()
    {
        int drawCall = GetParticleEffectData.GetOnlyParticleEffecDrawCall();
        m_MaxDrawCall = Math.Max(drawCall, m_MaxDrawCall);
        DrawCall = m_CurveDrawCallCount.UpdateAnimationCurve(drawCall, m_Duration < 0, Mathf.CeilToInt(TestDuration));
    }

    void UpdateOverdrawCurve()
    {
        EffectEvlaData effectEvlaData = GetEffectEvlaData();
        Overdraw = m_CurveOverdraw.UpdateAnimationCurve(effectEvlaData.GetPixRate(), m_Duration < 0, Mathf.CeilToInt(TestDuration));
    }

    private static void SelectTestScenePath()
    {
        testScenePath = EditorUtility.OpenFilePanelWithFilters("请选择用于测试的场景(.unity)", Application.dataPath, new string[] { "unity", "unity" });
        if (!string.IsNullOrEmpty(testScenePath))
        {
            testScenePath = testScenePath.Replace(Application.dataPath, "Assets").ToLower().Replace("\\", "/");
            EditorPrefs.SetString("ParticleEffectProfiler_TestScenePath", testScenePath);
        }
    }

    private static void AddEffectFolder()
    {
        string effectFolder = EditorUtility.OpenFolderPanel("请选择项目特效存储目录", Application.dataPath, "");
        if (!string.IsNullOrEmpty(effectFolder) && Directory.Exists(effectFolder))
        {
            effectFolder = effectFolder.Replace(Application.dataPath, "Assets").ToLower().Replace("\\", "/");
            if(!effectFolders.Contains(effectFolder))
            {
                effectFolders.Add(effectFolder);
                EditorPrefs.SetString("ParticleEffectProfiler_EffectFolder", string.Join(";", effectFolders.ToArray()));
            }
        }
    }

    [MenuItem("ParticleEffectProfiler/打开特效测试界面", false, 1)]
    [MenuItem("Assets/特效/打开特效测试界面")]
    private static void ShowTestParticleEffectWnd()
    {
        CreateTestParticleEffectWnd();
    }
}
