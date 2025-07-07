using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.AspNetCore.Components.Web;

namespace HPSystemsTools
{
  
    public partial class PointerViewModel : ObservableObject
    {
        private BlazorPointerTest? Test;
        private System.Timers.Timer? _timer;

        public PointerViewModel() { }

        public void Initialize(BlazorPointerTest test)
        {
            Test = test;
            Instructions = Test.Localized.CursorInstruction;
            ShowCursorTest = true;
            ShowBottom = false;
            ShowConfigSelection = false;
            ShowClickInstructions = false;
            ShowTimer = false;
        }

        public void OnCursorTest()
        {
            if (Clicks.ContainsKey("cursor"))
                Clicks["cursor"]++;
            else
                Clicks["cursor"] = 1;
            ShowCursorTest = false;
            ShowConfigSelection = true;
            Instructions = Test?.Localized.CenterBtnSelection ?? string.Empty;
            StartTimer(20);
        }

        public void OnClickTest(MouseEventArgs e)
        {
            var buttons = new[] { "left", "center", "right" };
            var clicked = buttons[e.Button];
            if ((!Is3Btn && clicked == "center"))
                return;
            if (Clicks[clicked] < ExpectedClicks[clicked])
            {
                Clicks[clicked]++;
            }
            if (IsPassed())
            {
                FinishTest();
            }
        }

        public void OnSelectDevice(string device, int btnCount)
        {
            SelectedDevice = device;
            ShowConfigSelection = false;
            ShowClickInstructions = true;
            ShowBottom = true;
            ShowTimer = true;
            Set3BtnMode(btnCount);
            Instructions = string.Empty;
            StartTimer(180);
        }

        public void StartTimer(int seconds)
        {
            TestTimeoutSec = seconds;
            TimerText = Test?.Localized.TimerDesc.Replace("%s", TestTimeoutSec.ToString()) ?? $"{TestTimeoutSec}";
            ShowTimer = true;
            _timer?.Stop();
            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += (s, e) =>
            {
                if (TestTimeoutSec > 0)
                {
                    TestTimeoutSec--;
                    TimerText = Test?.Localized.TimerDesc.Replace("%s", TestTimeoutSec.ToString()) ?? $"{TestTimeoutSec}";
                }
                if (TestTimeoutSec == 0)
                {
                    _timer.Stop();
                    CancelTest();
                }
            };
            _timer.Start();
        }

        [ObservableProperty]
        public partial bool Show { get; set; }

        [ObservableProperty]
        public partial bool ShowSpinner { get; set; }

        // New properties for pointer test logic
        public int ClicksToPass { get; set; } = 2;
        public Dictionary<string, int> Clicks { get; set; } = new() { { "cursor", 0 }, { "left", 0 }, { "right", 0 }, { "center", 0 } };
        public Dictionary<string, int> ExpectedClicks { get; set; } = new() { { "cursor", 1 }, { "left", 2 }, { "right", 2 }, { "center", 0 } };

        [ObservableProperty]
        public partial bool Is3Btn { get; set; } = false;

        [ObservableProperty]
        public partial int TestTimeoutSec { get; set; } = 180;

        [ObservableProperty]
        public partial string Instructions { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool ShowCursorTest { get; set; } = true;

        [ObservableProperty]
        public partial bool ShowConfigSelection { get; set; }

        [ObservableProperty]
        public partial bool ShowClickInstructions { get; set; }

        [ObservableProperty]
        public partial bool ShowTimer { get; set; }

        [ObservableProperty]
        public partial bool ShowBottom { get; set; }

        [ObservableProperty]
        public partial string TimerText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string SelectedDevice { get; set; } = "mouse"; // or "touchpad"

        // --- Additions for Blazor UI logic ---
        public void ResetClicks()
        {
            Clicks["cursor"] = 0;
            Clicks["left"] = 0;
            Clicks["right"] = 0;
            Clicks["center"] = 0;
        }

        public void CancelTest()
        {
            Show = false;
            ShowSpinner = false;
            ResetClicks();
            Test?.CancelTest();
        }

        public bool IsPassed()
        {
            foreach (var btn in Clicks.Keys)
            {
                if (Clicks[btn] < ExpectedClicks[btn])
                {
                    if (!Is3Btn && btn == "center")
                        continue;
                    return false;
                }
            }
            return true;
        }

        public void FinishTest()
        {
            Show = false;
            ShowSpinner = false;
            var expectedStr = string.Join(", ", ExpectedClicks.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            var clicksStr = string.Join(", ", Clicks.Select(kvp => $"{kvp.Key}={kvp.Value}"));

            Test?.Log($"Test finished. Expected: {expectedStr} | Actual: {clicksStr}");
            Test?.FinishTest(IsPassed());
            ResetClicks();
        }

        public void Set3BtnMode(int btnCount)
        {
            Is3Btn = btnCount == 3;
            ExpectedClicks["center"] = Is3Btn ? ClicksToPass : 0;
        }
    }
}
