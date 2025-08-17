namespace HPSystemsTools.ViewModels
{
    public enum ThermalStateEnum
    {
        Unknown,
        MaybeRunningHot, // DTS not found
        MaybeOk, // DTS not found
        RunningHot,
        Ok,
        StressLevelExceeded,
        Pass,
        Fail,
        MaybePass,
        MaybeFail,
        CheckInProgress,
        Reset
    }
}
