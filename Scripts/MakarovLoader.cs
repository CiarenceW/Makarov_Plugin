using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using Receiver2ModdingKit;
using UnityEngine;

[BepInPlugin("Ciarence.Makarov", "Makarov", "1.0.0")]
public class MakarovLoader : BaseUnityPlugin
{
	private void Awake()
	{
		// Plugin startup logic
		Logger.LogInfo($"Plugin " + ((BepInPlugin)Attribute.GetCustomAttribute(typeof(MakarovLoader), typeof(BepInPlugin))).GUID + " is loaded!");

		Logger.LogDebugWithColor(ConsoleColor.Blue, "piss");
	}
}
