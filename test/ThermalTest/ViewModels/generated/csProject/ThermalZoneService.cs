using HP.Telemetry;
using HPSystemsTools.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPSystemsTools.Services
{
    public class ThermalZoneService
    {
        /// <summary>
        /// The zone this service monitors.
        /// </summary>
        public Zone Zone { get; }

        private readonly Dictionary<DateTime, int> _processorLoads = new();
        private readonly Dictionary<DateTime, int> _temperatures = new();
        private readonly Dictionary<DateTime, int> _fanSpeeds = new();

        /// <summary>
        /// The model holding the latest telemetry values for this zone.
        /// </summary>
        public ThermalZoneModel Model { get; set; }

        public ThermalZoneService(Zone zone)
        {
            Zone = zone;
            Model = new ThermalZoneModel();
        }

        /// <summary>
        /// Resets all telemetry data for this zone.
        /// </summary>
        private void Reset()
        {
            _processorLoads.Clear();
            _temperatures.Clear();
            _fanSpeeds.Clear();
        }

        /// <summary>
        /// Adds a telemetry reading to the service and updates the model.
        /// </summary>
        public ThermalZoneModel Add(ITelemetryReading r)
        {
            if (r.Zone != Zone)
                return Model;

            Model.IsActive = true;

            switch (r.Property)
            {
                case TelemetryProperty.LOAD:
                    if (!_processorLoads.ContainsKey(r.Timestamp) && r.TryGetMeasure<int>(out var load) && load.HasValue)
                    {
                        Model.ProcessorLoad = load.Value;
                        _processorLoads.Add(r.Timestamp, load.Value);
                    }
                    break;
                case TelemetryProperty.TEMPERATURE:
                    if (!_temperatures.ContainsKey(r.Timestamp) && r.TryGetMeasure<int>(out var temp) && temp.HasValue)
                    {
                        Model.Temperature = temp.Value;
                        _temperatures.Add(r.Timestamp, temp.Value);
                    }
                    break;
                case TelemetryProperty.FAN_RPM:
                    if (!_fanSpeeds.ContainsKey(r.Timestamp) && r.TryGetMeasure<int>(out var fan) && fan.HasValue)
                    {
                        Model.FanSpeed = fan.Value;
                        _fanSpeeds.Add(r.Timestamp, fan.Value);
                    }
                    break;
                case TelemetryProperty.NAME:
                    Model.DeviceName = r.ToString() ?? "";
                    break;
                default:
                    // Ignore unknown properties
                    break;
            }
            return Model;
        }

        /// <summary>
        /// Gets the current state of the zone based on the telemetry and settings.
        /// </summary>
        public ThermalStateEnum GetState(TestSettingsModel? settings, out DateTime firstSeenInState)
        {
            firstSeenInState = DateTime.Now;
            if (settings == null)
                return ThermalStateEnum.Unknown;

            if (Model.ProcessorLoad >= settings.CpuLoadThreshold)
            {
                Reset();
                return ThermalStateEnum.StressLevelExceeded;
            }

            bool dtsDefined = TestSettingsModel.DTS.ContainsKey(Model.DeviceName ?? "UNDEFINED");
            int temperatureThreshold = settings.GetTemperatureThreshold(Model.DeviceName);

            ThermalStateEnum? latestState = null;
            foreach (var t in _temperatures.OrderByDescending(kv => kv.Key))
            {
                var state = GetTemperatureState(t.Value, temperatureThreshold, dtsDefined);
                if (latestState == null)
                    latestState = state;
                else if (state != latestState)
                    break;
                firstSeenInState = t.Key;
            }
            return latestState ?? ThermalStateEnum.Unknown;
        }

        /// <summary>
        /// Gets the test status for the zone.
        /// </summary>
        public ThermalStateEnum GetStatus(TestSettingsModel? settings, ThermalStateEnum zoneState, int secondsInState)
        {
            if (settings == null)
                return ThermalStateEnum.Unknown;

            if (zoneState == ThermalStateEnum.StressLevelExceeded)
                return ThermalStateEnum.Reset;

            if (secondsInState < settings.CpuLoadTimeSpan)
                return ThermalStateEnum.CheckInProgress;

            return zoneState switch
            {
                ThermalStateEnum.MaybeRunningHot => ThermalStateEnum.MaybeFail,
                ThermalStateEnum.MaybeOk => ThermalStateEnum.MaybePass,
                ThermalStateEnum.RunningHot => ThermalStateEnum.Fail,
                ThermalStateEnum.Ok => ThermalStateEnum.Pass,
                _ => ThermalStateEnum.Unknown,
            };
        }

        /// <summary>
        /// Gets the progress percentage for the zone.
        /// </summary>
        public int GetProgress(TestSettingsModel? settings, ThermalStateEnum zoneState, int secondsInState)
        {
            if (settings == null
                || zoneState == ThermalStateEnum.Unknown
                || zoneState == ThermalStateEnum.StressLevelExceeded)
                return 0;

            if (secondsInState >= settings.CpuLoadTimeSpan)
                return 100;

            return secondsInState * 100 / settings.CpuLoadTimeSpan;
        }

        /// <summary>
        /// Gets the spread (variation) in fan speeds as a percentage.
        /// </summary>
        private int GetFanSpread()
        {
            if (!_fanSpeeds.Any())
                return 0;

            int max = _fanSpeeds.Values.Max();
            int min = _fanSpeeds.Values.Min();
            int spread = max - min;
            return max == 0 ? 0 : 100 * spread / max;
        }

        /// <summary>
        /// Gets the spread (variation) in temperatures as a percentage.
        /// </summary>
        private int GetTemperatureSpread()
        {
            if (!_temperatures.Any())
                return 0;

            int max = _temperatures.Values.Max();
            int min = _temperatures.Values.Min();
            int spread = max - min;
            return max == 0 ? 0 : 100 * spread / max;
        }

        /// <summary>
        /// Determines the thermal state based on temperature, threshold, and DTS definition.
        /// </summary>
        private static ThermalStateEnum GetTemperatureState(int temperature, int threshold, bool dtsDefined)
        {
            if (temperature >= threshold)
                return dtsDefined ? ThermalStateEnum.RunningHot : ThermalStateEnum.MaybeRunningHot;
            return dtsDefined ? ThermalStateEnum.Ok : ThermalStateEnum.MaybeOk;
        }
    }
}
