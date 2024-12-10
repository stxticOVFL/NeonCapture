using System;
using System.Runtime.InteropServices;
using System.Security;
using Steamworks;
using UnityEngine;
using UnityEngine.Windows.WebCam;

namespace NeonCapture
{
    internal static class SteamNatives
    {
        static DLLFromMemory dll;
        internal static bool Setup()
        {
            dll = new(Resources.r.SteamworksInterop);
            SetLogCB = dll.GetDelegateFromFuncName<SetLogCB_D>("SetLogCB");
            SetTimelineStateDescription = dll.GetDelegateFromFuncName<SetTimelineStateDescription_D>("SetTimelineStateDescription");
            ClearTimelineStateDescription = dll.GetDelegateFromFuncName<ClearTimelineStateDescription_D>("ClearTimelineStateDescription");
            AddTimelineEvent = dll.GetDelegateFromFuncName<AddTimelineEvent_D>("AddTimelineEvent");
            SetTimelineGameMode = dll.GetDelegateFromFuncName<SetTimelineGameMode_D>("SetTimelineGameMode");
            return true;
        }

        public delegate void ResponseDelegate(string s);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void SetLogCB_D(ResponseDelegate response);
        internal static SetLogCB_D SetLogCB;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void SetTimelineStateDescription_D(string pchDescription, float flTimeDelta);
        internal static SetTimelineStateDescription_D SetTimelineStateDescription;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ClearTimelineStateDescription_D(float flTimeDelta);
        internal static ClearTimelineStateDescription_D ClearTimelineStateDescription;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void AddTimelineEvent_D(string pchIcon, string pchTitle, string pchDescription, uint unPriority, float flStartOffsetSeconds, float flDurationSeconds, int ePossibleClip);
        internal static AddTimelineEvent_D AddTimelineEvent;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void SetTimelineGameMode_D(int eMode);
        internal static SetTimelineGameMode_D SetTimelineGameMode;
    }

    public enum ETimelineGameMode
    {
        k_ETimelineGameMode_Invalid,
        k_ETimelineGameMode_Playing,
        k_ETimelineGameMode_Staging,
        k_ETimelineGameMode_Menus,
        k_ETimelineGameMode_LoadingScreen
    }

    public enum ETimelineEventClipPriority
    {
        k_ETimelineEventClipPriority_Invalid,
        k_ETimelineEventClipPriority_None,
        k_ETimelineEventClipPriority_Standard,
        k_ETimelineEventClipPriority_Featured,
    }

    public static class SteamTimeline
    {
        public static void SetTimelineStateDescription(string pchDescription, float flTimeDelta) =>
            SteamNatives.SetTimelineStateDescription(pchDescription, flTimeDelta);

        public static void ClearTimelineStateDescription(float flTimeDelta) =>
            SteamNatives.ClearTimelineStateDescription(flTimeDelta);

        public static void AddTimelineEvent(string pchIcon, string pchTitle, string pchDescription, uint unPriority, float flStartOffsetSeconds, float flDurationSeconds, ETimelineEventClipPriority ePossibleClip) =>
            SteamNatives.AddTimelineEvent(pchIcon, pchTitle, pchDescription, unPriority, flStartOffsetSeconds, flDurationSeconds, (int)ePossibleClip);

        public static void SetTimelineGameMode(ETimelineGameMode eMode) =>
            SteamNatives.SetTimelineGameMode((int)eMode);
    }
}