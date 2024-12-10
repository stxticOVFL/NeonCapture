#include "steam/isteamtimeline.h"
#include <iostream>

extern "C" {
	static void (*callback)(const char*);

	__declspec(dllexport) void SetTimelineStateDescription(const char* pchDescription, float fltimeDelta) {
		SteamTimeline()->SetTimelineStateDescription(pchDescription, fltimeDelta);
	}

	__declspec(dllexport) void ClearTimelineStateDescription(float fltimeDelta) {
		SteamTimeline()->ClearTimelineStateDescription(fltimeDelta);
	}

	__declspec(dllexport) void AddTimelineEvent(const char* pchIcon, const char* pchTitle, const char* pchDescription, uint32 unPriority, float flStartOffsetSeconds, float flDurationSeconds, int ePossibleClip) {
		callback("Called TLevent");
		callback(pchTitle);
		SteamTimeline()->AddTimelineEvent(pchIcon, pchTitle, pchDescription, unPriority, flStartOffsetSeconds, flDurationSeconds, (ETimelineEventClipPriority)ePossibleClip);
		callback("After set");
	}

	__declspec(dllexport) void SetTimelineGameMode(int eMode) {
		SteamTimeline()->SetTimelineGameMode((ETimelineGameMode)eMode);
		callback("Gamemode set");
	}

	__declspec(dllexport) void SetLogCB(void (*cb)(const char*)) {
		callback = cb;
		callback("Callback test from SteamworksInterop!");
		//SetTimelineGameMode(k_ETimelineGameMode_Menus);
	}
}