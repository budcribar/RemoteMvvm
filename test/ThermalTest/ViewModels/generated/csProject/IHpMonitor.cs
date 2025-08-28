using System;
using System.Collections.Generic;

namespace HP.Telemetry
{
    public enum TelemetryProperty
    {
        SMART_ADAPTER_NO_SUPPORT,
        SMART_ADAPTER_RATING_MEETS_REQUIREMENT,
        SMART_ADAPTER_BELOW_REQUIREMENT,
        SMART_ADAPTER_TOO_SMALL,
        SMART_ADAPTER_NOT_FUNCTIONING,
        BATTERY_CHARGE_TREND_IDLE,
        BATTERY_CHARGE_TREND_CHARGING,
        BATTERY_CHARGE_TREND_DISCHARGING,

        BATTERY_FULL_CHARGE_CAPACITY,
        BATTERY_REMAINING_CAPACITY,
        BATTERY_REMAINING_PERCENTAGE,
        BATTERY_GENUINE_HP,
        BATTERY_CT_NUMBER,
        BATTERY_SERIAL_NUMBER,
        BATTERY_MANUFACTURER_NAME,
        BATTERY_DEVICE_NAME,
        BATTERY_DEVICE_CHEMISTRY,
        BATTERY_WEAR,
        BATTERY_COUNT,
        BATTERY_01_07_STATUS,
        POWER_ONLINE,
        POWER_OFFLINE,
        CPU_TEMPERATURE,
        GPU_TEMPERATURE,
        SYSTEM_TEMPERATURE,
        BATTERY_TEMPERATURE,
        FAN_STATUS,
        FAN_RPM,
        FAN_TARGET_RPM,
        HP_THERMAL_UNKNOWN,
        ADAPTER_TYPE,
        LOAD,
        NAME,
        TEMPERATURE,
        UNKNOWN

    }

    public enum UnitOfMeasure
    {
        DEGREE_CELSIUS,
        RPM,
        PERCENTAGE,
        MILLIAMPERE,
        MILLIAMPEREPERHOUR,
        MILLIVOLT,
        MINUTE,
        BOOLEAN,
        FLAG,
        NONE,
    }

    public enum Zone
    {
        Other,
        CPUZ_0,
        CPUZ_1,
        GFXZ_0,
        EXTZ_0,
        LOCZ_0,
        BATZ_0,
        CHGZ_0,
        SK1Z_0,
        SK2Z_0,
        PCHZ_0
    }

    public interface ITelemetryReading
    {
        public bool IsValid { get; }
        public int Index { get; set; }
        public TelemetryProperty Property { get; }
        public Zone Zone { get; set; }
        public string GetCaption(bool useIndex = false);
        public UnitOfMeasure UnitOfMeasure { get; }
        public DateTime Timestamp { get; set; }
        public string ResultCaption { get; }
        public bool TryGetMeasure<U>(out Nullable<U> value) where U : struct;
    }
    public interface IDeviceMonitor
    {
        TimeSpan MinimumEventPeriod { get; }
        Boolean IsActive { get; set; }
        IDisposable Subscribe(IObserver<IEnumerable<ITelemetryReading>> observer);
    }
    public interface IPowerMonitor : IDeviceMonitor { }
    public interface IThermalMonitor : IDeviceMonitor { }
    public interface IBiosNumericSensorMonitor : IDeviceMonitor { }
    public interface IProcessorLoadMonitor : IDeviceMonitor { }
}
