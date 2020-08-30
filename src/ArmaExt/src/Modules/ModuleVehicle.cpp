#include "ModuleVehicle.hpp"


#include "Networking/NetworkController.hpp"
#include "Networking/Serialize.hpp"

template <>
void VehicleProperty<float>::Set(std::string_view newVal) {
    value = Util::parseArmaNumber(newVal);
}
template <>
void VehicleProperty<float>::Serialize(JsonArchive& ar) {
    ar.Serialize(name.c_str(), value);
}

template <>
void VehicleProperty<bool>::Set(std::string_view newVal) {
    value = Util::isTrue(newVal);
}
template <>
void VehicleProperty<bool>::Serialize(JsonArchive& ar) {
    ar.Serialize(name.c_str(), value);
}

void VehicleCrewMember::Serialize(JsonArchive& ar) {
    ar.Serialize("name", name);
    ar.Serialize("role", role);
    ar.Serialize("cargoIndex", cargoIndex);
}

void ModuleVehicle::UpdateProperty(std::string_view name, std::string_view value) {
    auto found = vehicleProperties.find(name);
    if (found == vehicleProperties.end()) {
        Util::BreakToDebuggerIfPresent();
        return;
    }
    (*found)->Set(value);
}

ModuleVehicle::ModuleVehicle() {
#define VEC_PROP_CREATE(name, type, default)  vehicleProperties.insert(std::make_unique<VehicleProperty<type>>(#name, default));

VEHICLE_PROPERTIES(VEC_PROP_CREATE)

#undef VEC_PROP_CREATE
}

void ModuleVehicle::OnGameMessage(const std::vector<std::string_view>& function,
                                  const std::vector<std::string_view>& arguments) {

    bool stateHasChanged = false;

    if (function[0] == "Update") {
        for (size_t i = 0; i < arguments.size(); i+=2) {
            auto propName = arguments[i];
            auto propVal = arguments[i + 1];

            UpdateProperty(propName, propVal);
            stateHasChanged = true;
        }
    } else if (function[0] == "VecLeft") {
        isInVehicle = false;
        SetDefaultProperties();
        stateHasChanged = true;
    } else if (function[0] == "VecEntered") {
        isInVehicle = true;
        stateHasChanged = true;
    }
    else if (function[0] == "CrewUpdate") {

        // arguments array of \n seperated arrays
        //https://community.bistudio.com/wiki/fullCrew with object replaced by name
        vehicleCrew.clear();
        for (auto& it : arguments) {
            auto split = Util::split(it, '\n');
            auto name = split[0];
            auto role = split[1];
            auto cargoIndex = Util::parseArmaNumberToInt(split[2]);
            vehicleCrew.emplace_back(name, role, cargoIndex);
        }
  
        stateHasChanged = true;
    }

    if (stateHasChanged)
        GNetworkController.SendStateUpdate();
}

void ModuleVehicle::OnNetMessage(std::span<std::string_view> function, const nlohmann::json& arguments,
    const std::function<void(std::string_view)>& replyFunc) {}

void ModuleVehicle::SerializeState(JsonArchive& ar) {
    JsonArchive properties;
    for (auto& it : vehicleProperties) {
        it->Serialize(properties);
    }
    ar.Serialize("props", properties);
    ar.Serialize("isInVehicle", isInVehicle);
    ar.Serialize("crew", vehicleCrew);
}

void ModuleVehicle::SetDefaultProperties() {
#define VEC_PROP_DEFAULT(name, type, default)  reinterpret_cast<VehicleProperty<type>*>((*vehicleProperties.find(#name )).get())->Set(default);

    VEHICLE_PROPERTIES(VEC_PROP_DEFAULT)

#undef VEC_PROP_DEFAULT
}