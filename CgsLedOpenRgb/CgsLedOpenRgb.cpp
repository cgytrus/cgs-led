#include "CgsLedOpenRgb.hpp"
#include "CgsLedRgbController.hpp"
#include "SettingsManager.h"
#include <QHBoxLayout>
#include <nlohmann/json.hpp>

ResourceManagerInterface* CgsLedOpenRgb::s_res = nullptr;

OpenRGBPluginInfo CgsLedOpenRgb::GetPluginInfo() {
    OpenRGBPluginInfo info;
    info.Name = "CG's LED";
    info.Description = "haiiiiii";
    info.Version = VERSION_STRING;
    info.Commit = GIT_COMMIT_ID;
    info.URL = "https://github.com/cgytrus/cgs-led";
    info.Icon.load(":/icon.png");

    info.Location = OPENRGB_PLUGIN_LOCATION_SETTINGS;
    info.Label = "CG's LED";
    info.TabIconString = "CG's LED";
    info.TabIcon.load(":/icon.png");

    return info;
}

unsigned int CgsLedOpenRgb::GetPluginAPIVersion() {
    return OPENRGB_PLUGIN_API_VERSION;
}

void CgsLedOpenRgb::Load(ResourceManagerInterface* res) {
    CgsLedOpenRgb::s_res = res;

    json settings = res->GetSettingsManager()->GetSettings("CgsLed");
    if (!settings.contains("port"))
        settings["port"] = "COM5";
    if (!settings.contains("baud"))
        settings["baud"] = 12000000;
    if (!settings.contains("brightness"))
        settings["brightness"] = 40u;
    res->GetSettingsManager()->SetSettings("CgsLed", settings);

    res->RegisterDetectionEndCallback(&DetectDevices, nullptr);
    DetectDevices(nullptr);
}

QWidget* CgsLedOpenRgb::GetWidget() {
    QWidget* widget =  new QWidget(nullptr);
    QHBoxLayout* layout = new QHBoxLayout();

    widget->setLayout(layout);
    layout->addWidget(new QLabel("Allo, allo?"));

    return widget;
}

QMenu* CgsLedOpenRgb::GetTrayMenu() {
    //QMenu* menu = new QMenu("CG's LED");

    return nullptr;
}

void CgsLedOpenRgb::Unload() { }

void CgsLedOpenRgb::DetectDevices(void*) {
    json settings = CgsLedOpenRgb::s_res->GetSettingsManager()->GetSettings("CgsLed");

    if (!settings.contains("port") || !settings.contains("baud"))
        return;

    auto ports = serial_port::getSerialPorts();
    if (!std::any_of(ports.begin(), ports.end(), [&](std::string x) { return x == settings["port"].get<std::string>(); }))
        return;

    auto* controller = new CgsLedRgbController(
        settings["port"].get<std::string>().c_str(),
        settings["baud"].get<int>(),
        settings.contains("brightness") ? settings["brightness"].get<unsigned int>() : 40u
    );

    CgsLedOpenRgb::s_res->RegisterRGBController(controller);
}

CgsLedOpenRgb::CgsLedOpenRgb() { }

CgsLedOpenRgb::~CgsLedOpenRgb() { }
