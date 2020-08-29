#include "script_component.hpp"

params ["_radio", "_radioChannel", "_startTransmit"];

if (_radio isEqualType []) then {
   if (_startTransmit) then {
        if (!call TFAR_fnc_isAbleToUseRadio) exitWith {call TFAR_fnc_unableToUseHint;false};

        private _frequency = [_radio, _radioChannel + 1] call TFAR_fnc_getChannelFrequency;
        [_radio, _radioChannel, _frequency, false] call TFAR_fnc_doLRTransmit;
        TF_tangent_lr_pressed = true;
    } else {
        private _frequency = [_radio, _radioChannel + 1] call TFAR_fnc_getChannelFrequency;
        [_radio, _radioChannel, _frequency, false] call TFAR_fnc_doLRTransmitEnd;
        TF_tangent_lr_pressed = false;
    };
} else {
   if (_startTransmit) then {
        if (!call TFAR_fnc_isAbleToUseRadio) exitWith {call TFAR_fnc_unableToUseHint;false};

        private _frequency = [_radio, _radioChannel + 1] call TFAR_fnc_getChannelFrequency;
        [_radio, _radioChannel, _frequency, false] call TFAR_fnc_doSRTransmit;
        TF_tangent_sw_pressed = true;
    } else {
        private _frequency = [_radio, _radioChannel + 1] call TFAR_fnc_getChannelFrequency;
        [_radio, _radioChannel, _frequency, false] call TFAR_fnc_doSRTransmitEnd;
        TF_tangent_sw_pressed = false;
    };
}



