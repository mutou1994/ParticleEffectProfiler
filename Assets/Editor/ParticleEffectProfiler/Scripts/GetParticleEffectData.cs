#if UNITY_EDITOR
using PlasticPipe.PlasticProtocol.Messages;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;


public enum TestEffectQuality
{
    All = 0,
    High,
    Middle,
    Low,
}

public struct EffectDataAdvancer
{
    public int duration;
    public int particleSystemCount;
    public int particleCount;
    public int memorySize;
    public int texturePixels;
    public int textureCount;
    public int meshTriangleCount;
    public int maxDrawCall;
    public int pixRate;
    //1表示支持剔除 0表示无要求
    public int cullingSupport;

    public int pixDrawAverage;
    public int pixActualDrawAverage;

    public bool IsWell(float duration, int particleSystemCount, int particleCount, int textureCount, int texturePixels, int meshTriangleCount, int memorySize, int maxDrawCall, int pixRate, int cullingSupport)
    {
        return !Mathf.Approximately(duration, 0) && duration <= this.duration &&
            particleSystemCount <= this.particleSystemCount &&
            particleCount <= this.particleCount &&
            memorySize <= this.memorySize &&
            texturePixels <= this.texturePixels &&
            textureCount <= this.textureCount &&
            meshTriangleCount <= this.meshTriangleCount &&
            maxDrawCall <= this.maxDrawCall &&
            pixRate <= this.pixRate &&
            cullingSupport >= this.cullingSupport;
    }
}

/// <summary>
/// 处理特效整体相关的数据
/// </summary>
public class GetParticleEffectData {

    static EffectDataAdvancer[] EffectDataAdvancer = new EffectDataAdvancer[]
    {
        //All
        new EffectDataAdvancer
        {
            duration=10, particleSystemCount=10, particleCount=100, textureCount=10, texturePixels=1024*1024, meshTriangleCount=5000, memorySize= 1000*1024, maxDrawCall=10, pixRate=5, cullingSupport=1
        },
        //High
        new EffectDataAdvancer
        {
            duration=10, particleSystemCount=15, particleCount=100, textureCount=10, texturePixels=2048*2048, meshTriangleCount=5000, memorySize= 1000*1024, maxDrawCall=10, pixRate=5, cullingSupport=1
        },
        //Middle
        new EffectDataAdvancer
        {
            duration=10, particleSystemCount=10, particleCount=80, textureCount=10, texturePixels=2048*2048, meshTriangleCount=4000, memorySize= 1000*1024, maxDrawCall=10, pixRate=5, cullingSupport=1
        },
        //Low
        new EffectDataAdvancer
        {
            duration=10, particleSystemCount=8, particleCount=60, textureCount=8, texturePixels=1024*1024, meshTriangleCount=3000, memorySize= 1000*1024, maxDrawCall=8, pixRate=5, cullingSupport=1
        },
    };

    public static EffectDataAdvancer GetAdvancer(TestEffectQuality quality)
    {
        return EffectDataAdvancer[(int)quality];
    }

    public static bool IsWellEffect(string effectPath, TestEffectQuality quality, bool bInActiveNode, bool bInvalidNode, bool bRenderLostMaterial, bool bParticleMultiMat, bool bHigherQualityNode,
        float duration, int particleSystemCount, int particleCount, int textureCount, int texturePixels, int meshTriangleCount,
        int memorySize, int maxDrawCall, int pixRate, int cullingSupport)
    {
        if (quality < TestEffectQuality.All || quality > TestEffectQuality.Low) return false;
        if (bInActiveNode) return false;
        if (bInvalidNode) return false;
        if (bRenderLostMaterial) return false;
        if (bParticleMultiMat) return false;
        if (bHigherQualityNode) return false;

        var advancer = GetAdvancer(quality);
        return advancer.IsWell(duration, particleSystemCount, particleCount, textureCount, texturePixels, meshTriangleCount, memorySize, maxDrawCall, pixRate, cullingSupport);
    }

    public static string GetEffectQualityStr(TestEffectQuality quality)
    {
        return string.Format("特效品质:{0}", quality.ToString());
    }

    public static int GetRuntimeMemorySize(Renderer[] rendererList , TestEffectQuality quality, out int textureCount, out int pixelCount)
    {
        var textures = new List<Texture>();
        textureCount = 0;
        pixelCount = 0;
        int sumSize = 0;

        foreach (Renderer item in rendererList)
        {
            var mats = item.sharedMaterials;
            if(mats != null && mats.Length > 0)
            {
                for(int i=0; i< mats.Length; i++)
                {
                    var mat = mats[i];
                    if(mat != null)
                    {
                        var texs = mat.GetTexturePropertyNames();
                        foreach(var texName in texs)
                        {
                            Texture texture = mat.GetTexture(texName);
                            if(texture != null && !textures.Contains(texture))
                            {
                                textures.Add(texture);
                                textureCount++;
                                sumSize = sumSize + GetStorageMemorySize(texture);
                                pixelCount += texture.width * texture.height;
                            }
                        }
                    }
                }
            }
        }
        return sumSize;
    }

    private static int GetStorageMemorySize(Texture texture)
    {
        return (int)InvokeInternalAPI("UnityEditor.TextureUtil", "GetStorageMemorySize", texture);
    }

    private static object InvokeInternalAPI(string type, string method, params object[] parameters)
    {
        var assembly = typeof(AssetDatabase).Assembly;
        var custom = assembly.GetType(type);
        var methodInfo = custom.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
        return methodInfo != null ? methodInfo.Invoke(null, parameters) : 0;
    }

    public static string GetParticleSystemMultiMatsStr(bool bParticleMultiMats)
    {
        return bParticleMultiMats ? "<color=red>粒子节点存在多个材质,请检查!</color>" : string.Empty;
    }

    public static string GetHigherQualityNodeStr(bool bHigherQualityNode)
    {
        return bHigherQualityNode ? "<color=red>节点质量分级高于文件分级,请检查!</color>" : string.Empty;
    }

    public static string GetInActiveNodeStr(bool bInActiveNode)
    {
        return bInActiveNode ? "<color=red>存在未激活节点,若不需要请删除,请检查!</color>" : string.Empty;
    }

    public static string GetInvalidNodeStr(bool bInvalidParticle, bool bInvalidRenderer, bool bInValidAnimator)
    {
        string str = string.Empty;
        if (bInvalidParticle)
        {
            str = "粒子";
        }
        if (bInvalidRenderer)
        {
            str += string.IsNullOrEmpty(str) ? "Renderer" : "、Renderer";
        }
        if(bInValidAnimator)
        {
            str += string.IsNullOrEmpty(str) ? "Animator" : "、Animator";
        }
        if(!string.IsNullOrEmpty(str))
        {
            str = string.Format("<color=red>存在无效{0}节点,请检查！</color>", str);
        }
        return str;
    }

    public static string GetEffectDurationStr(float duration, TestEffectQuality quality)
    {
        var advancer = GetAdvancer(quality);
        string str = string.Empty;
        if(Mathf.Approximately(duration, 0))
        {
            str = string.Format("特效时长:<color=red>0</color>,请检查！", FormatColorMax(duration, advancer.duration));
        }
        else if(duration > 0)
        {
            str = string.Format("特效时长:{0} 建议: < {1}", FormatColorMax(duration, advancer.duration), advancer.duration);
        }
        else
        {
            str = "特效时长:循环";
        }
        return str;
    }

    public static string GetGetRuntimeMemorySizeStr(Renderer[] renderers, TestEffectQuality quality)
    {
        var advancer = GetAdvancer(quality);
        int maxTextureCount = advancer.textureCount;
        int maxMemorySize = advancer.memorySize;
        int maxPixelCount = advancer.texturePixels;
        int textureCount;
        int pixelCount;
        int memorySize = GetRuntimeMemorySize(renderers, quality, out textureCount, out pixelCount);
        string memorySizeStr = EditorUtility.FormatBytes(memorySize);
        string maxMemorySizeStr = EditorUtility.FormatBytes(maxMemorySize);

        if (maxMemorySize > memorySize)
            memorySizeStr = string.Format("<color=green>{0}</color>", memorySizeStr);
        else
            memorySizeStr = string.Format("<color=red>{0}</color>", memorySizeStr);

        return string.Format("贴图所占用的内存：{0}   建议：< {1}\n总像素：{2}   建议 < {3}\n贴图数量：{4}     建议：<{5}", 
            memorySizeStr, maxMemorySizeStr, 
            FormatColorMax(pixelCount, maxPixelCount), maxPixelCount,
            FormatColorMax(textureCount, maxTextureCount), maxTextureCount);
    }

    public static string GetParticleSystemCountStr(int particleSystemCount, TestEffectQuality quality)
    {
        var advancer = GetAdvancer(quality);
        int max = advancer.particleSystemCount;
        return string.Format("粒子组件数量：{0}     建议：< {1}", FormatColorMax(particleSystemCount, max), max);
    }
    
    public static string GetMeshTriangleCountStr(int triangleCount, TestEffectQuality quality)
    {
        if(triangleCount <= 0) return string.Empty;
        var advancer = GetAdvancer(quality);
        int max = advancer.meshTriangleCount;
        return string.Format("模型三角面熟练:{0}   建议 < {1}", FormatColorMax(triangleCount, max), max);
    }

    public static string GetLostMaterialStr(bool bLostMaterial)
    {
        string str = string.Empty;
        if(bLostMaterial)
        {
            str = "<color=red>存在丢失材质的Renderer,请检查！！</color>";
        }
        return str;
    }

    public static int GetOnlyParticleEffecDrawCall()
    {
        //因为Camera 实际上渲染了两次，一次用作取样，一次用作显示。 狂飙这里给出了详细的说明：https://networm.me/2019/07/28/unity-particle-effect-profiler/#drawcall-%E6%95%B0%E5%80%BC%E4%B8%BA%E4%BB%80%E4%B9%88%E6%AF%94%E5%AE%9E%E9%99%85%E5%A4%A7-2-%E5%80%8D
        int drawCall = UnityEditor.UnityStats.batches / 2;
        return drawCall;
    }

    public static string GetOnlyParticleEffecDrawCallStr(TestEffectQuality quality, int drawCall)
    {
        var advancer = GetAdvancer(quality);
        int max = advancer.maxDrawCall;
        return string.Format("DrawCall: {0}   最高：{1}   建议：< {2}", FormatColorMax(GetOnlyParticleEffecDrawCall(), max), FormatColorMax(drawCall, max), max);
    }

    public static string GetPixDrawAverageStr(int pixDrawAverage)
    {
        //index = 0：默认按高品质的算，这里你可以根本你们项目的品质进行修改。
        //EffectEvlaData[] effectEvlaData = particleEffectGo.GetEffectEvlaData();
        //int pixDrawAverage = effectEvlaData[0].GetPixDrawAverage();
        return string.Format("特效原填充像素点：{0}", FormatColorValue(pixDrawAverage));
    }

    public static string GetPixActualDrawAverageStr(int pixActualDrawAverage)
    {
        //EffectEvlaData[] effectEvlaData = particleEffectGo.GetEffectEvlaData();
        //int pixActualDrawAverage = effectEvlaData[0].GetPixActualDrawAverage();
        return string.Format("特效实际填充像素点：{0}", FormatColorValue(pixActualDrawAverage));
    }

    public static string GetPixRateStr(TestEffectQuality quality, int pixRate)
    {
        var advancer = GetAdvancer(quality);
        int max = advancer.pixRate;
        //int max = 4;
        //EffectEvlaData[] effectEvlaData = particleEffectGo.GetEffectEvlaData();
        //int pixRate = effectEvlaData[0].GetPixRate();
        return string.Format("平均每像素overdraw率：{0}   建议：< {1}", FormatColorMax(pixRate, max), max);
    }

    public static string GetParticleCountStr(TestEffectQuality quality, int particleCount, int maxParticleCount)
    {
        var advancer = GetAdvancer(quality);
        int max = advancer.particleCount;
        return string.Format("粒子数量：{0}   最高：{1}   建议：< {2}", FormatColorMax(particleCount, max), FormatColorMax(maxParticleCount, max), max);
    }

    public static string GetCullingSupportedString(ParticleSystem[] particleSystems, TestEffectQuality quality)
    {
        string text = "";
        foreach (ParticleSystem item in particleSystems)
        {
            string str = CheckCulling(item);
            if (!string.IsNullOrEmpty(str))
            {
                text += item.gameObject.name + ":" + str + "\n\n";
            }
        }
        return text;
    }

    public static string GetCullingSupportedStr(int cullingSupported)
    {
        return string.Format("是否支持粒子自动剔除:{0}", cullingSupported > 0 ? "<color=green>支持</color>" : "<color=red>不支持</color>");
    }

    public static string CheckCulling(ParticleSystem particleSystem, string split = "\n")
    {
        List<string> tips = new List<string>();
        //string text = "";

        if (particleSystem.main.gravityModifier.mode != ParticleSystemCurveMode.Constant)
        {
            //text += "\nGravityModifier 使用了非Constant";
            tips.Add("GravityModifier 使用了非Constant");
        }

        if (particleSystem.main.simulationSpace != ParticleSystemSimulationSpace.Local)
        {
            //text += "\nSimulationSpace 不等于 Local";
            tips.Add("SimulationSpace 不等于 Local");
        }

        if (particleSystem.collision.enabled)
        {
            //text += "\n勾选了 Collision";
            tips.Add("勾选了 Collision");
        }

        if (particleSystem.emission.enabled)
        {
            var distance = particleSystem.emission.rateOverDistance;
            var mode = distance.mode;
            if ((mode != ParticleSystemCurveMode.Constant && mode != ParticleSystemCurveMode.TwoConstants) || (distance.constant+distance.constantMin+distance.constantMax > 0))
            {
                //text += "\nEmission 使用了RateOverDistance(非线性运算)";
                tips.Add("Emission 使用了RateOverDistance(非线性运算)");
            }
        }

        if (particleSystem.externalForces.enabled)
        {
            //text += "\n勾选了 External Forces";
            tips.Add("勾选了 External Forces");
        }

        if (particleSystem.forceOverLifetime.enabled)
        {
            if (GetIsRandomized(particleSystem.forceOverLifetime.x)
                || GetIsRandomized(particleSystem.forceOverLifetime.y)
                || GetIsRandomized(particleSystem.forceOverLifetime.z)
                || particleSystem.forceOverLifetime.randomized)
            {
                //text += "\nForce Over Lifetime使用了Current(非线性运算)";
                tips.Add("Force Over Lifetime使用了Current(非线性运算)");
            }
        } 
        if (particleSystem.inheritVelocity.enabled)
        {
            if (GetIsRandomized(particleSystem.inheritVelocity.curve))
            {
                //text += "\nInherit Velocity使用了Current(非线性运算)";
                tips.Add("Inherit Velocity使用了Current(非线性运算)");
            }
        } 
        if (particleSystem.noise.enabled)
        {
            //text += "\n勾选了 Noise";
            tips.Add("勾选了 Noise");
        } 
        if (particleSystem.rotationBySpeed.enabled)
        {
            //text += "\n勾选了 Rotation By Speed";
            tips.Add("勾选了 Rotation By Speed");
        }
        if (particleSystem.rotationOverLifetime.enabled)
        {
            if (GetIsRandomized(particleSystem.rotationOverLifetime.x)
                || GetIsRandomized(particleSystem.rotationOverLifetime.y)
                || GetIsRandomized(particleSystem.rotationOverLifetime.z))
            {
                //text += "\nRotation Over Lifetime使用了Current(非线性运算)";
                tips.Add("Rotation Over Lifetime使用了Current(非线性运算)");
            }
        } 
        if (particleSystem.shape.enabled)
        {
            ParticleSystemShapeType shapeType = (ParticleSystemShapeType)particleSystem.shape.shapeType;
            switch (shapeType)
            {
                case ParticleSystemShapeType.Cone:
                //case ParticleSystemShapeType.ConeVolume:
#if UNITY_2017_1_OR_NEWER
                case ParticleSystemShapeType.Donut:
#endif
                case ParticleSystemShapeType.Circle:
                    if(particleSystem.shape.arcMode != ParticleSystemShapeMultiModeValue.Random)
                    {
                        //text += "\nShape的Circle-Arc使用了Random模式";
                        tips.Add("Shape的Cone、Donut或Circle的Arc使用了非Random模式");
                    }
                    break;
                case ParticleSystemShapeType.SingleSidedEdge:
                    if (particleSystem.shape.radiusMode != ParticleSystemShapeMultiModeValue.Random)
                    {
                        //text += "\nShape的Edge-Radius使用了Random模式";
                        tips.Add("Shape的Edge-Radius使用了非Random模式");
                    }
                    break;
                default:
                    break;
            }
        } 
        if (particleSystem.subEmitters.enabled)
        {
            //text += "\n勾选了 SubEmitters";
            tips.Add("勾选了 SubEmitters");
        } 
        if (particleSystem.trails.enabled)
        {
            //text += "\n勾选了 Trails";
            tips.Add("勾选了 Trails");
        } 
        if (particleSystem.trigger.enabled)
        {
            //text += "\n勾选了 Trigger";
            tips.Add("勾选了 Trigger");
        }
        if (particleSystem.velocityOverLifetime.enabled)
        {
            if (GetIsRandomized(particleSystem.velocityOverLifetime.x)
                || GetIsRandomized(particleSystem.velocityOverLifetime.y)
                || GetIsRandomized(particleSystem.velocityOverLifetime.z))
            {
                //text += "\nVelocity Over Lifetime使用了Current(非线性运算)";
                tips.Add("Velocity Over Lifetime使用了Current(非线性运算)");
            }
        }
        if (particleSystem.limitVelocityOverLifetime.enabled)
        {
            //text += "\n勾选了 Limit Velocity Over Lifetime";
            tips.Add("勾选了 Limit Velocity Over Lifetime");
        }
        
        return string.Join(split, tips.ToArray());
    }

    static bool GetIsRandomized(ParticleSystem.MinMaxCurve minMaxCurve)
    {
        bool flag = AnimationCurveSupportsProcedural(minMaxCurve.curveMax);

        bool result;
        if (minMaxCurve.mode != ParticleSystemCurveMode.TwoCurves && minMaxCurve.mode != ParticleSystemCurveMode.TwoConstants)
        {
            result = flag;
        }
        else
        {
            bool flag2 = AnimationCurveSupportsProcedural(minMaxCurve.curveMin);
            result = (flag && flag2);
        }

        return result;
    }

    static bool AnimationCurveSupportsProcedural(AnimationCurve curve)
    {
        //switch (AnimationUtility.IsValidPolynomialCurve(curve)) //保护级别，无法访问，靠
        //{
        //    case AnimationUtility.PolynomialValid.Valid:
        //        return true;
        //    case AnimationUtility.PolynomialValid.InvalidPreWrapMode:
        //        break;
        //    case AnimationUtility.PolynomialValid.InvalidPostWrapMode:
        //        break;
        //    case AnimationUtility.PolynomialValid.TooManySegments:
        //        break;
        //}
        return false; //只能默认返回false了
    }

    static string FormatColorValue(int value)
    {
        return string.Format("<color=green>{0}</color>", value);
    }

    static string FormatColorMax(int value, int max)
    {
        if (max > value)
            return string.Format("<color=green>{0}</color>", value);
        else
            return string.Format("<color=red>{0}</color>", value);
    }

    static string FormatColorMax(float value, float max)
    {
        if (max > value)
            return string.Format("<color=green>{0}</color>", value.ToString("F2"));
        else
            return string.Format("<color=red>{0}</color>", value.ToString("F2"));
    }
}
#endif