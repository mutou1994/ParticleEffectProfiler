using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using System;
using Unity.VisualScripting;

public class CSVEffectEvlaHelper
{
    private StringBuilder _stringBuilder = null;
    private static CSVEffectEvlaHelper _instance = null;
    private static string _directoryPath = "";
    private string _csvPath = "";
    private FileStream _fileStream;
    private StreamWriter _streamWriter;

    public static CSVEffectEvlaHelper GetInstance()
    {
        if (_instance == null)
        {
            _instance = new CSVEffectEvlaHelper();
            _instance.Init();
        }
        return _instance;
    }

    public static void DestroyInstance()
    {
        if(_instance != null)
        {
            _instance.Release();
            _instance = null;
        }
    }

    string DiretoryPath
    {
        get
        {
            if(string.IsNullOrEmpty(_directoryPath))
            {
                _directoryPath = string.Format("{0}/../LogOut/EffectProfiler/", Application.dataPath);
            }
            if(!Directory.Exists(_directoryPath))
            {
                Directory.CreateDirectory(_directoryPath);
            }
            return _directoryPath;
        }
    }

    public void Init()
    {
        _stringBuilder = new StringBuilder();
    }

    public void Release()
    {
        if(_stringBuilder != null)
        {
            _stringBuilder.Clear();
        }
        //_stringBuilder = null;
    }

    public void RecordLog(string effectPath, TestEffectQuality quality, bool bInActiveNode, bool bInValidNode, bool bRenderLostMat, bool bParticleMultiMats, bool bHigherQualityNode,
                        float duration, int particleSystemCount, int maxParticleCount, int textureCount, int texturePixels, int meshTriangles,
                        int memorySize, int maxDrawCall, int pixDrawAverage, int pixActualDrawAverage, int pixRate, int cullingSupport)
    {
        if (_instance == null) return;
        var advancer = GetParticleEffectData.GetAdvancer(quality);
        _stringBuilder.AppendLineFormat("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18}",
            effectPath, quality.ToString(),
            bInActiveNode ? "*是" : "否", bInValidNode ? "*是" : "否",
            bRenderLostMat ? "*是" : "否", bParticleMultiMats ? "*是" : "否", bHigherQualityNode ? "*是" : "否",
            Mathf.Approximately(duration, 0) ? "*0" : GetLogDataStr(duration, advancer.duration),
            GetLogDataStr(particleSystemCount, advancer.particleSystemCount), GetLogDataStr(maxParticleCount, advancer.particleCount),
            GetLogDataStr(meshTriangles, advancer.meshTriangleCount),GetLogDataStr(textureCount, advancer.textureCount),
            GetLogDataStr(texturePixels, advancer.texturePixels),GetLogDataStr(memorySize, advancer.memorySize),
            GetLogDataStr(maxDrawCall, advancer.maxDrawCall), GetLogDataStr(pixDrawAverage, advancer.pixDrawAverage),
            GetLogDataStr(pixActualDrawAverage, advancer.pixActualDrawAverage), GetLogDataStr(pixRate, advancer.pixRate),
            cullingSupport > 0 ? "是":"*否");
    }

    string GetLogDataStr(int value, int advancer)
    {
        if (value <= advancer) return value.ToString();
        return string.Format("*{0}({1})", value, advancer);
    }

    string GetLogDataStr(float value, float advancer)
    {
        if (value <= advancer) return value.ToString("F2");
        return string.Format("*{0}({1})", value.ToString("F2"), advancer.ToString("F2"));
    }

    public void SaveLog(TestEffectQuality quality)
    {
        if (_stringBuilder == null || _stringBuilder.Length == 0) return;
        string strTime = string.Format("{0}-{1}-{2}-{3}-{4}-{5}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);
        _csvPath = string.Format("{0}/data_{1}{2}.csv", DiretoryPath, strTime, quality.ToString());
        _fileStream = new FileStream(_csvPath, FileMode.OpenOrCreate, FileAccess.Write);
        _streamWriter = new StreamWriter(_fileStream, System.Text.Encoding.UTF8);

        _streamWriter.WriteLine("特效路径,品质,是否存在未激活节点,是否存在无效节点,是否丢失材质,是否单粒子包含多个材质,节点质量高于文件质量,时长,粒子组件数,最大粒子数,模型总面数,贴图数量,贴图总像素,贴图内存,最大DrawCall,原填充像素点,实际填充像素点,平均没像素OverDraw,是否支持剔除");
        _streamWriter.Write(_stringBuilder.ToString());

        _streamWriter.Flush();
        _streamWriter.Close();
        _fileStream.Close();
        _streamWriter = null;
        _fileStream = null;

    }
}
