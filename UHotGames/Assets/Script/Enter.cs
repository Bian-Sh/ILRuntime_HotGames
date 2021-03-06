﻿using ILRuntime.Runtime.Intepreter;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ILRuntime.Runtime.Stack;
using ILRuntime.CLR.Method;
using ILRuntime.CLR.TypeSystem;
using System;
using System.Linq;
using UnityEngine.UI;

public class Enter : MonoBehaviour
{
	public static string ConfigURL = "http://gelunbandu.gelunjiaoyu.com/wenjianxiazai/cdn/Config.txt";
	public bool UseAB;
	private Transform trUIAlert;
	private void Start()
	{
#if UNITY_ANDROID
        MonoInstancePool.getInstance<SDK_Orientation>(true).ShowBar();
#endif
		Screen.fullScreen = true;
		MonoInstancePool.getInstance<SDK_Photo>(true);
		MonoInstancePool.getInstance<AntiScriptSplit>(true);
		MonoInstancePool.getInstance<SDK_WeChat>(true);

		trUIAlert = UStaticFuncs.FindChildComponent<Transform>(transform, "UIAlert");

#if UNITY_IOS
		MonoInstancePool.getInstance<SDK_AppleInApp>(true);
#endif

		gameObject.AddComponent<UConsoleDebug>();
#if !UNITY_WEBGL
		var fa = "fa" + Utils_Plugins.Util_GetBundleVersion();
		AOutput.Log($"local version {fa}");
		if (!PlayerPrefs.HasKey(fa))
		{
			AOutput.Log($"start copy files {fa}");
			MonoInstancePool.getInstance<UCopyFilesFromStreamingAssets>().OnCopy("versions.txt", UStaticFuncs.ConfigSaveDir, () =>
			{
				InitRemoteConfig();
				PlayerPrefs.SetInt("fa" + Utils_Plugins.Util_GetBundleVersion(), 1);
			});
		}
		else
		{
#endif
			InitRemoteConfig();
#if !UNITY_WEBGL
		}
#endif
	}


	bool bIsLocal;
	private void InitRemoteConfig()
	{
		ConfigDownloader.Instance.StartToDownload(ConfigURL, () =>
		{
			bIsLocal = false;

			ParseConfig();
		}, () =>
		{
			bIsLocal = true;
			UConfigManager.bUsingAb = false;
			Invoke("InitRemoteConfig", 3);
		});

	}

	private void ParseConfig(bool bChecked = false)
	{
		if (!bChecked)
		{
			CheckNewVersion();
			return;
		}

		UConfigManager.bUsingAb = ConfigDownloader.Instance.OnGetIntValue("useab") == 1;

#if UNITY_EDITOR
		UConfigManager.bUsingAb = UseAB;
#endif

		if (UConfigManager.bUsingAb)
		{
			StartCoroutine(OnDownloadDll(ConfigDownloader.Instance.OnGetValue("dll")));
		}
		else
		{
			LoadDll(File.ReadAllBytes("Assets/RemoteResources/Dll/AHotGames.bytes")
				, File.ReadAllBytes("Assets/RemoteResources/Dll/AHotGames.pdb"));
		}
	}

	WWW www;
	IEnumerator OnDownloadDll(string dllPath, float delay = 0)
	{
		if (delay > 0)
		{
			yield return new WaitForSeconds(delay);
		}
		AOutput.Log($"dllPath {dllPath}");
		www = new WWW(dllPath);
		yield return www;
		if (string.IsNullOrEmpty(www.error))
		{
			if (dllPath.EndsWith(".ab"))
			{
				LoadDll(www.assetBundle.LoadAsset<TextAsset>("AHotGames").bytes, null);
			}
			else
			{
				var dllBytes = www.bytes;
#if UNITY_EDITOR
				www = new WWW(dllPath.Replace(".bytes", ".pdb"));
				AOutput.Log($"www {www.url}");
				yield return www;
				LoadDll(dllBytes, www.bytes);
#else
				LoadDll(dllBytes, null);
#endif
			}
		}
		else
		{
			StartCoroutine(OnDownloadDll(ConfigDownloader.Instance.OnGetValue("dll"), 3));
			AOutput.Log($"www {www.url} error {www.error}");
		}
		www = null;
	}
	private void LoadDll(byte[] bytes, byte[] pdbBytes)
	{
		StartCoroutine(DelayLoadDll(bytes, pdbBytes));
	}
	IEnumerator DelayLoadDll(byte[] bytes, byte[] pdbBytes)
	{
		yield return new WaitForEndOfFrame();

		ILRuntimeHandler.Instance.DoLoadDll("AHotGames", bytes, pdbBytes);

		ILRuntimeHandler.Instance.SetUnityMessageReceiver(MonoInstancePool.getInstance<UEmitMessage>(true).gameObject);

		ILRuntimeHandler.Instance.OnLoadClass("AEntrance", new GameObject("AEntrance"), false, UConfigManager.bUsingAb.ToString());
		ILRuntimeHandler.Instance.EmitMessage(bIsLocal ? "local" : "remote");
		ILRuntimeHandler.Instance.EmitMessage($"resPath:{ConfigDownloader.Instance.OnGetValue("resPath")}");
	}

	void CheckNewVersion()
	{
		var newversionkey = "";
		var newversionmustdownkey = "";
		var newversionurlkey = "";
		var nvandignorekey = "";
#if UNITY_IOS
		newversionkey = "nvios";
		newversionmustdownkey = "nviosm";
		newversionurlkey = "nviosurl";
#elif UNITY_ANDROID
        newversionkey = "nvand";
        newversionmustdownkey = "nvandm";
        newversionurlkey = "nvandurl";
        nvandignorekey = "nvandignore";
#elif UNITY_STANDALONE
		newversionkey = "nvwin";
		newversionmustdownkey = "nvwinm";
		newversionurlkey = "nvwinurl";
#endif
		var remoteVersion = ConfigDownloader.Instance.OnGetValue(newversionkey);
		if (!string.IsNullOrEmpty(remoteVersion))
		{
			if (VersionIsSmall(Utils_Plugins.Util_GetBundleVersion(), remoteVersion))
			{
				var anvandignore = ConfigDownloader.Instance.OnGetValue(nvandignorekey).Split(',');
				var newversionmustdown = ConfigDownloader.Instance.OnGetIntValue(newversionmustdownkey);
				var newversionurl = ConfigDownloader.Instance.OnGetValue(newversionurlkey);
				if (anvandignore.Contains(Utils_Plugins.Util_GetBundleVersion()))
				{
					if (PlayerPrefs.GetString("ignore") == remoteVersion)
					{
						ParseConfig(true);
						return;
					}
					UIAlert.Show($"有新版本可更新，本版本({Utils_Plugins.Util_GetBundleVersion()})配置为可忽略更新，点击“确定”按钮更新，点击“取消”按钮跳过版本{remoteVersion}更新。", () =>
					{
						Application.OpenURL(newversionurl);
						Invoke("ShowUIAlert", 0.2f);
					}, false, false, () =>
					{
						PlayerPrefs.SetString("ignore", remoteVersion);
						ParseConfig(true);
					}, trUIAlert, true);
					return;
				}
				if (newversionmustdown == 1)
				{
					UIAlert.Show(string.Format("发现最新版本{0}，本版本有重要更新，请更新后重试。", remoteVersion), () =>
					{
						Application.OpenURL(newversionurl);
						Invoke("ShowUIAlert", 0.2f);
					}, false, false, () => { Application.Quit(); }, trUIAlert, true);
				}
				else
				{
					UIAlert.Show(string.Format("发现最新版本{0}，是否要更新？", remoteVersion), () =>
					{
						Application.OpenURL(newversionurl);
					}, false, false, () =>
					{
						ParseConfig(true);
					}, trUIAlert);
				}
			}
			else
			{
				ParseConfig(true);
			}
		}
		else
		{
			ParseConfig(true);
		}
	}
	private bool VersionIsSmall(string localVersion, string remoteVersion)
	{
		var alocal = localVersion.Split('.');
		var aremote = remoteVersion.Split('.');
		for (var i = 0; i < alocal.Length; i++)
		{
			if (i >= aremote.Length)
			{
				return false;
			}
			if (alocal[i] == aremote[i])
			{
				continue;
			}
			return typeParser.intParse(alocal[i]) < typeParser.intParse(aremote[i]);
		}
		return alocal.Length < aremote.Length;
	}
}
