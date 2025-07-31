#pragma once

#include "OpenRGBPluginInterface.h"
#include "ResourceManagerInterface.h"

#include <QObject>
#include <QString>
#include <QtPlugin>
#include <QWidget>

class CgsLedOpenRgb : public QObject, public OpenRGBPluginInterface {
    Q_OBJECT
    Q_PLUGIN_METADATA(IID OpenRGBPluginInterface_IID)
    Q_INTERFACES(OpenRGBPluginInterface)

public:
    CgsLedOpenRgb();
    ~CgsLedOpenRgb();

    OpenRGBPluginInfo GetPluginInfo() override;
    unsigned int GetPluginAPIVersion() override;

    void Load(ResourceManagerInterface* res) override;
    QWidget* GetWidget() override;
    QMenu* GetTrayMenu() override;
    void Unload() override;

    static void DetectDevices(void*);

    static ResourceManagerInterface* s_res;
};
