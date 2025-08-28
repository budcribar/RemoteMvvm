using HP.Telemetry;
using HPSystemsTools.ViewModels;
using HPSystemsTools.Views;
using Microsoft.JSInterop;
using PeakSWC.Mvvm.Remote;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ToolFrameworkPackage;

namespace HPSystemsTools
{
   
    public partial class HP3LSThermalTest : BlazorToolBase<HP3LSThermalTestView>
    {
        public HP3LSThermalTestViewModel ViewModel { get; }
        private readonly HP3LSThermalTestResultMetadata _resultMetadata = new();

        private StringBuilder logMsg = new();
        // Change from UsbTest
        public override Guid Id => new Guid("8141AD76-FB77-4149-949E-2F0566B4BD3C");

        public override List<string> Instances => new();
        protected override IResultMetaData ResultMetaData => _resultMetadata;

        public override bool HandlesUpdateProgress => false;
        public override bool HandlesDuration => false;
        public override string ToolVersion => "1.0";

        public HP3LSThermalTest(Task<IJSRuntime> jsRuntimeTask) : base(jsRuntimeTask)
        {
            //ViewModel = new HP3LSThermalTestViewModel();

            ViewModel = new HP3LSThermalTestViewModel(new ServerOptions { Port = 50052, UseHttps = false });
        }

        public override bool ExPreInstall() => true;

        public void FinishTest(bool passed)
        {
            LogMsg("Test completed\n");
            _resultMetadata.IsTestPassed = passed;
            FrameworkContext.Cancel();
        }

        public void CancelTest()
        {
            LogMsg("User canceled\n");
            _resultMetadata.IsCancelled = true;
            FrameworkContext.Cancel();
        }

        public override async Task<ITestResult> ExExecute(TimeSpan duration, IFrameworkContext framework, string instance = null, string commandLineArgs = null)
        {
            _resultMetadata.Reset();
            logMsg = new StringBuilder();

            await PauseExecution(/*TimeSpan.FromMinutes(5)*/);

            if (ViewModel.Zones.ContainsKey(Zone.CPUZ_0)) LogMsg($"CPUZ_0 Name: {ViewModel.Zones[Zone.CPUZ_0].DeviceName}\n");
            if (ViewModel.Zones.ContainsKey(Zone.CPUZ_1)) LogMsg($"CPUZ_1 Name: {ViewModel.Zones[Zone.CPUZ_1].DeviceName}\n");        
            LogMsg($"Test Settings\n CPU Temprature Threshold: {ViewModel.TestSettings.CpuTemperatureThreshold}\n CPU Load Threshold: {ViewModel.TestSettings.CpuLoadThreshold}\n CPU Load TimeSpan: {ViewModel.TestSettings.CpuLoadTimeSpan}\n");
            Log(logMsg.ToString());

            return new BoolTestResult(ResultMetaData);
        }

        public override bool ExUninstall() => true;

        public void LogMsg(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            logMsg.AppendLine($"{value}");
        }

        private void Log(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            FrameworkContext.LogData += $"{value}";
        }
    }
}
