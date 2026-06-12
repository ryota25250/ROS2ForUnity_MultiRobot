using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Tessera;
using System.Linq;
using System;
using UnityEngine.Events;
namespace Tessera
{
    public enum TesseraMultipassPassType
    {
        Generator,
        Event,
    }
    [Serializable]
    public class TesseraMultipassPass
    {
        public TesseraMultipassPassType passType;
        public TesseraGenerator generator;
        public UnityEvent generateEvent;
        public UnityEvent clearEvent;
    }
    public class TesseraMultipassGenerator : MonoBehaviour
    {
        public List<Vector3> RoadTilePositions = new List<Vector3>();
        public TesseraMultipassPass[] passes;
        public void Clear()
        {
            foreach (var pass in passes)
            {
                if (pass.passType == TesseraMultipassPassType.Generator)
                {
                    pass.generator.Clear();
                }
                else if (pass.passType == TesseraMultipassPassType.Event)
                {
                    pass.clearEvent?.Invoke();
                }
                else
                {
                    throw new Exception($"Unknown passType {pass.passType}");
                }
            }
        }
        public void Generate()
        {
            RoadTilePositions.Clear();
            foreach(var pass in passes)
            {
                if (pass.passType == TesseraMultipassPassType.Generator && pass.generator != null)
                {
                    var completion = pass.generator.Generate();
                    //Debug.Log("タイル生成成功: " + completion.success); 
                    if (completion.success)
                    {
                        foreach (var instance in completion.tileInstances)
                        {
                            //生成したタイルの名前出力
                            //Debug.Log(instance.Tile.name);
                            if (instance.Tile.name.StartsWith("Road_"))//"Road_"がつくタイルの名前のとき
                            {
                                RoadTilePositions.Add(instance.Position);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogError("タイル生成に失敗しました！");
                    }
                }
                else if (pass.passType == TesseraMultipassPassType.Event)
                {
                    pass.generateEvent?.Invoke();
                }
                else
                {
                    throw new Exception($"Unknown passType {pass.passType}");
                }
            }
        }
    }
}