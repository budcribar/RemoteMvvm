using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components;

namespace HPSystemsTools
{
    // Note: Not implementing IDisposable here as it's already generated in the Remote.g.cs file
    public partial class PointerViewModel : ObservableObject
    {
        private BlazorPointerTest? Test;
        private bool _disposed = false;
        private bool _suppressNotifications = false;

        public PointerViewModel() { }

        public void Initialize(BlazorPointerTest test)
        {
            Test = test ?? throw new ArgumentNullException(nameof(test));
            Initialize();
        }

        // Override the OnPropertyChanged method to support suppressing notifications
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (!_suppressNotifications)
            {
                base.OnPropertyChanged(e);
            }
        }

        [RelayCommand]
        public void Initialize()
        {
            if (Test == null) return;

            Instructions = Test.Localized.CursorInstruction;
            ShowCursorTest = true;
            ShowBottom = false;
            ShowConfigSelection = false;
            ShowClickInstructions = false;
            ShowTimer = false;
            ResetClicks();
        }

        [RelayCommand]
        public void OnCursorTest()
        {
            if (Test == null) return;

            if (_clicks.ContainsKey("cursor"))
                _clicks["cursor"]++;
            else
                _clicks["cursor"] = 1;
            ShowCursorTest = false;
            ShowConfigSelection = true;
            Instructions = Test.Localized.CenterBtnSelection;

            // Start the timer in the Test class
            Test.StartTimer(2000);
        }

        [RelayCommand]
        public void OnClickTest(MouseEventArgs e)
        {
            if (Test == null) return;

            // More robust button mapping
            string clicked;
            switch (e.Button)
            {
                case 0: clicked = "left"; break;
                case 1: clicked = "center"; break;
                case 2: clicked = "right"; break;
                default: return; // Unknown button
            }

            // Skip center button clicks if not in 3-button mode
            if (!Is3Btn && clicked == "center")
                return;

            if (_clicks[clicked] < _expectedClicks[clicked])
            {
                _clicks[clicked]++;
            }
            if (IsPassed())
            {
                FinishTest();
            }
        }

        [RelayCommand]
        public void OnSelectDevice(string device)
        {
            if (Test == null) return;

            SelectedDevice = device;
            ShowConfigSelection = false;
            ShowClickInstructions = true;
            ShowBottom = true;
            ShowTimer = true;
            Instructions = string.Empty;

            // Start the timer in the Test class
            Test.StartTimer(180);
        }

        [RelayCommand]
        public void OnSelectNumButtons(int btnCount)
        {
            Set3BtnMode(btnCount);
        }

        [ObservableProperty]
        private bool _show = true;

        [ObservableProperty]
        private bool _showSpinner = false;

        [ObservableProperty]
        private int _clicksToPass = 2;

        private Dictionary<string, int> _clicks = new() { { "cursor", 0 }, { "left", 0 }, { "right", 0 }, { "center", 0 } };
        private Dictionary<string, int> _expectedClicks = new() { { "cursor", 1 }, { "left", 2 }, { "right", 2 }, { "center", 0 } };

        [ObservableProperty]
        private bool _is3Btn = false;

        [ObservableProperty]
        private int _testTimeoutSec = 180;

        [ObservableProperty]
        private string _instructions = string.Empty;

        [ObservableProperty]
        private bool _showCursorTest = true;

        [ObservableProperty]
        private bool _showConfigSelection = false;

        [ObservableProperty]
        private bool _showClickInstructions = false;

        [ObservableProperty]
        private bool _showTimer = false;

        [ObservableProperty]
        private bool _showBottom = false;

        [ObservableProperty]
        private string _timerText = string.Empty;

        [ObservableProperty]
        private string _selectedDevice = "mouse"; // or "touchpad"

        [ObservableProperty]
        private int _lastClickCount;

        // RelayCommand that suppresses PropertyChanged notifications while updating LastClickCount
        [RelayCommand]
        public void GetClicksWithoutNotification(string button)
        {
            try
            {
                // Suppress PropertyChanged notifications
                _suppressNotifications = true;

                // Update the property without triggering notifications
                LastClickCount = _clicks.TryGetValue(button, out var count) ? count : 0;
            }
            finally
            {
                // Always restore notifications
                _suppressNotifications = false;
            }
        }

        [RelayCommand]
        public void ResetClicks()
        {
            _clicks["cursor"] = 0;
            _clicks["left"] = 0;
            _clicks["right"] = 0;
            _clicks["center"] = 0;
        }

        [RelayCommand]
        public void CancelTest()
        {
            if (Test == null) return;

            Show = false;
            ShowSpinner = false;
            Test.CancelTest();
        }

        private bool IsPassed()
        {
            foreach (var btn in _clicks.Keys)
            {
                if (_clicks[btn] < _expectedClicks[btn])
                {
                    if (!Is3Btn && btn == "center")
                        continue;
                    return false;
                }
            }
            return true;
        }

        [RelayCommand]
        public void FinishTest()
        {
            if (Test == null) return;

            Show = false;
            ShowSpinner = false;
            var expectedStr = string.Join(", ", _expectedClicks.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            var clicksStr = string.Join(", ", _clicks.Select(kvp => $"{kvp.Key}={kvp.Value}"));

            Test.Log($"Test finished. Expected: {expectedStr} | Actual: {clicksStr}");
            Test.FinishTest(IsPassed());

        }

        private void Set3BtnMode(int btnCount)
        {
            Is3Btn = btnCount == 3;
            _expectedClicks["center"] = Is3Btn ? ClicksToPass : 0;
        }

        // Custom dispose method that will be called from the generated Dispose method
        internal void CustomDispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
