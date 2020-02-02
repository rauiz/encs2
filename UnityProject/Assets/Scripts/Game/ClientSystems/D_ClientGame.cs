using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Entities;
using Unity.Networking.Transport;
using UnityEngine;


public struct MC_OnClientConnected : IComponentData
{
}

public struct MC_OnTextNotified : IComponentData
{
    public int TextHandle;
}

public struct MC_OnVisorNotified : IComponentData
{
    public int TileHandle;
    public int FeedbackHandle;
    public int Intensity;
}