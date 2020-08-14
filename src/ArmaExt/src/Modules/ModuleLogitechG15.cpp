#include "ModuleLogitechG15.hpp"
#include <filesystem>
#include <Windows.h>
#include <codecvt>

#include "Game/GameManager.hpp"
#include "RadioModule.hpp"

#include <fmt/format.h>

#define LOGI_LCD_TYPE_MONO    (0x00000001)

#define LOGI_LCD_MONO_BUTTON_0 (0x00000001)
#define LOGI_LCD_MONO_BUTTON_1 (0x00000002)
#define LOGI_LCD_MONO_BUTTON_2 (0x00000004)
#define LOGI_LCD_MONO_BUTTON_3 (0x00000008)

const int LOGI_LCD_MONO_WIDTH = 160;
const int LOGI_LCD_MONO_HEIGHT = 43;


static bool (*LogiLcdInit)(wchar_t* friendlyName, int lcdType);
static bool (*LogiLcdIsConnected)(int lcdType);
static bool (*LogiLcdIsButtonPressed)(int button);
static void (*LogiLcdUpdate)();
static void (*LogiLcdShutdown)();

// Monochrome LCD functions
static bool (*LogiLcdMonoSetBackground)(BYTE monoBitmap[]);
static bool (*LogiLcdMonoSetText)(int lineNumber, wchar_t* text);






void ModuleLogitechG15::Run() {
    std::vector<bool> isTransmitting(4, false);
    std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;

    while (shouldRun) {
        LogiLcdUpdate();

        auto firstSR = GRadioModule.GetFirstSRRadio();
        auto firstLR = GRadioModule.GetFirstLRRadio();

        std::optional<std::string> radio1, radio2, radio3, radio4;

        if (firstSR) {
            if (firstSR->currentChannel != -1)
                radio1 = fmt::format("SR C{} ({})", firstSR->currentChannel, firstSR->channels[firstSR->currentChannel].frequency);
            if (firstSR->currentAltChannel != -1)
                radio2 = fmt::format("SR A{} ({})", firstSR->currentAltChannel, firstSR->channels[firstSR->currentAltChannel].frequency);

        } else {
            //LogiLcdMonoSetText(0, L"No SR");
        }

        if (firstLR) {
            if (firstLR->currentChannel != -1)
                radio2 = fmt::format("LR C{} ({})", firstLR->currentChannel, firstLR->channels[firstLR->currentChannel].frequency);
            if (firstLR->currentAltChannel != -1)
                radio3 = fmt::format("LR A{} ({})", firstLR->currentAltChannel, firstLR->channels[firstLR->currentAltChannel].frequency);

        }
        else {
            //LogiLcdMonoSetText(1, L"No LR");
        }


        constexpr auto maxSegmentLength = (28 / 2);
        auto displayText1 = converter.from_bytes(
            fmt::format("{} {}",
                fmt::format("{1:>{0}}", maxSegmentLength, radio2 ? std::string_view(*radio2).substr(0, maxSegmentLength) : ""sv),
                fmt::format("{1:>{0}}", maxSegmentLength, radio4 ? std::string_view(*radio4).substr(0, maxSegmentLength) : ""sv)
            )
        );
        auto displayText2 = converter.from_bytes(
            fmt::format("{} {}",
                fmt::format("{1:<{0}}", maxSegmentLength, radio1 ? std::string_view(*radio1).substr(0, maxSegmentLength) : ""sv),
                fmt::format("{1:<{0}}", maxSegmentLength, radio3 ? std::string_view(*radio4).substr(0, maxSegmentLength) : ""sv)
            )
        );
        while (!LogiLcdMonoSetText(2, displayText1.data())) {
            displayText1.pop_back();
        }
        while (!LogiLcdMonoSetText(3, displayText2.data())) {
            displayText2.pop_back();
        }


        if (firstSR && LogiLcdIsButtonPressed(LOGI_LCD_MONO_BUTTON_0) != isTransmitting[0]) {
            isTransmitting[0] = LogiLcdIsButtonPressed(LOGI_LCD_MONO_BUTTON_0);
            GRadioModule.DoRadioTransmit(firstSR->id, firstSR->currentChannel, isTransmitting[0]);
        }
        if (firstSR && LogiLcdIsButtonPressed(LOGI_LCD_MONO_BUTTON_1) != isTransmitting[1]) {
            isTransmitting[1] = LogiLcdIsButtonPressed(LOGI_LCD_MONO_BUTTON_1);
            GRadioModule.DoRadioTransmit(firstSR->id, firstSR->currentAltChannel, isTransmitting[1]);
        }
        if (firstLR && LogiLcdIsButtonPressed(LOGI_LCD_MONO_BUTTON_2) != isTransmitting[2]) {
            isTransmitting[2] = LogiLcdIsButtonPressed(LOGI_LCD_MONO_BUTTON_2);
            GRadioModule.DoRadioTransmit(firstLR->id, firstLR->currentChannel, isTransmitting[2]);
        }
        if (firstLR && LogiLcdIsButtonPressed(LOGI_LCD_MONO_BUTTON_3) != isTransmitting[3]) {
            isTransmitting[3] = LogiLcdIsButtonPressed(LOGI_LCD_MONO_BUTTON_3);
            GRadioModule.DoRadioTransmit(firstLR->id, firstLR->currentAltChannel, isTransmitting[3]);
        }




        std::this_thread::sleep_for(16ms);
    }
}


void ModuleLogitechG15::ModuleInit() {

    char path[MAX_PATH];
    HMODULE hm = NULL;

    if (GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
        GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        (LPCSTR)&RVExtension, &hm) == 0)
    {
        int ret = GetLastError();
        fprintf(stderr, "GetModuleHandle failed, error = %d\n", ret);
        // Return or however you want to handle an error.
    }
    if (GetModuleFileName(hm, path, sizeof(path)) == 0)
    {
        int ret = GetLastError();
        fprintf(stderr, "GetModuleFileName failed, error = %d\n", ret);
        // Return or however you want to handle an error.
    }




    std::string dllPath = (std::filesystem::path(path).parent_path() / "LogitechLcd.dll").lexically_normal().string();

    auto logiDLL = LoadLibraryExA(dllPath.c_str(), nullptr, 0);

    if (!logiDLL) return;

    LogiLcdInit = reinterpret_cast<decltype(LogiLcdInit)>(GetProcAddress(logiDLL, "LogiLcdInit"));
    LogiLcdIsConnected = reinterpret_cast<decltype(LogiLcdIsConnected)>(GetProcAddress(logiDLL, "LogiLcdIsConnected"));
    LogiLcdIsButtonPressed = reinterpret_cast<decltype(LogiLcdIsButtonPressed)>(GetProcAddress(logiDLL, "LogiLcdIsButtonPressed"));
    LogiLcdUpdate = reinterpret_cast<decltype(LogiLcdUpdate)>(GetProcAddress(logiDLL, "LogiLcdUpdate"));
    LogiLcdShutdown = reinterpret_cast<decltype(LogiLcdShutdown)>(GetProcAddress(logiDLL, "LogiLcdShutdown"));
    LogiLcdMonoSetBackground = reinterpret_cast<decltype(LogiLcdMonoSetBackground)>(GetProcAddress(logiDLL, "LogiLcdMonoSetBackground"));
    LogiLcdMonoSetText = reinterpret_cast<decltype(LogiLcdMonoSetText)>(GetProcAddress(logiDLL, "LogiLcdMonoSetText"));

    LogiLcdInit(L"TacControl", LOGI_LCD_TYPE_MONO);

    if (!LogiLcdIsConnected(LOGI_LCD_TYPE_MONO)) return;


    Thread::ModuleInit();

}
