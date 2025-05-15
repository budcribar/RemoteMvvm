using System.ComponentModel;
using SampleApp.ViewModels;
namespace TestProject
{
    public class UnitTest1
    {
        [Fact]
        public void InitialValues_AreSetCorrectly()
        {
            // Arrange & Act
            var viewModel = new SampleViewModel();

            // Assert
            Assert.Equal("Initial Name", viewModel.Name);
            Assert.Equal(0, viewModel.Count);
            Assert.Equal("Ready", viewModel.NonObservableStatus);
        }

        [Fact]
        public void NameProperty_WhenSet_RaisesPropertyChanged()
        {
            // Arrange
            var viewModel = new SampleViewModel();
            string? receivedPropertyName = null;
            viewModel.PropertyChanged += (sender, e) =>
            {
                receivedPropertyName = e.PropertyName;
            };
            var newName = "New Test Name";

            // Act
            viewModel.Name = newName;

            // Assert
            Assert.Equal(newName, viewModel.Name);
            Assert.Equal(nameof(SampleViewModel.Name), receivedPropertyName);
        }

        [Fact]
        public void CountProperty_WhenSet_RaisesPropertyChanged()
        {
            // Arrange
            var viewModel = new SampleViewModel();
            string? receivedPropertyName = null;
            viewModel.PropertyChanged += (object? sender, PropertyChangedEventArgs e) =>
            {
                if (e.PropertyName == nameof(SampleViewModel.Count))
                {
                    receivedPropertyName = e.PropertyName;
                }
            };
            var newCount = 10;

            // Act
            viewModel.Count = newCount;

            // Assert
            Assert.Equal(newCount, viewModel.Count);
            Assert.Equal(nameof(SampleViewModel.Count), receivedPropertyName);
        }

        [Fact]
        public void IncrementCountCommand_ExecutesCorrectly()
        {
            // Arrange
            var viewModel = new SampleViewModel();
            var initialCount = viewModel.Count;
            string? namePropertyChanged = null;
            string? countPropertyChanged = null;

            viewModel.PropertyChanged += (sender, e) => {
                if (e.PropertyName == nameof(SampleViewModel.Name)) namePropertyChanged = e.PropertyName;
                if (e.PropertyName == nameof(SampleViewModel.Count)) countPropertyChanged = e.PropertyName;
            };

            // Act
            viewModel.IncrementCountCommand.Execute(null);

            // Assert
            Assert.Equal(initialCount + 1, viewModel.Count);
            Assert.Equal($"Count is now {viewModel.Count}", viewModel.Name);
            Assert.Equal(nameof(SampleViewModel.Name), namePropertyChanged);
            Assert.Equal(nameof(SampleViewModel.Count), countPropertyChanged);
        }

        [Fact]
        public async Task DelayedIncrementCommand_ExecutesCorrectly()
        {
            // Arrange
            var viewModel = new SampleViewModel();
            var initialCount = viewModel.Count;
            var delay = 10; // Small delay for testing
            string? namePropertyChanged = null;
            string? countPropertyChanged = null;

            viewModel.PropertyChanged += (sender, e) => {
                if (e.PropertyName == nameof(SampleViewModel.Name)) namePropertyChanged = e.PropertyName;
                if (e.PropertyName == nameof(SampleViewModel.Count)) countPropertyChanged = e.PropertyName;
            };

            // Act
            // For AsyncRelayCommand<T>, ExecuteAsync takes an object parameter.
            // If your command method takes specific parameters, the generated command will expect them.
            await viewModel.DelayedIncrementCommand.ExecuteAsync(delay);

            // Assert
            Assert.Equal(initialCount + 5, viewModel.Count);
            Assert.Equal($"Count updated to {viewModel.Count} after delay.", viewModel.Name);
            Assert.Equal(nameof(SampleViewModel.Name), namePropertyChanged);
            Assert.Equal(nameof(SampleViewModel.Count), countPropertyChanged);
        }

        [Fact]
        public async Task DelayedIncrementCommand_WithNegativeDelay_DoesNotThrowAndCountUnchanged()
        {
            // Arrange
            var viewModel = new SampleViewModel();
            var initialCount = viewModel.Count;
            var initialName = viewModel.Name;
            var delay = -100; // Negative delay

            bool countChanged = false;
            viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(SampleViewModel.Count))
                {
                    countChanged = true;
                }
            };

            // Act
            // ExecuteAsync will not throw if the command method handles the exception/invalid state.
            await viewModel.DelayedIncrementCommand.ExecuteAsync(delay);

            // Assert
            Assert.Equal(initialCount, viewModel.Count); // Count should not change
            Assert.Equal(initialName, viewModel.Name);   // Name should not change
            Assert.False(countChanged); // PropertyChanged for Count should not have been raised
        }

        [Fact]
        public void SetNameToValueCommand_ExecutesCorrectly_WithNonNullValue()
        {
            // Arrange
            var viewModel = new SampleViewModel();
            var testValue = "Specific Name";
            string? receivedPropertyName = null;
            viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(SampleViewModel.Name))
                {
                    receivedPropertyName = e.PropertyName;
                }
            };

            // Act
            viewModel.SetNameToValueCommand.Execute(testValue);

            // Assert
            Assert.Equal(testValue, viewModel.Name);
            Assert.Equal(nameof(SampleViewModel.Name), receivedPropertyName);
        }

        [Fact]
        public void SetNameToValueCommand_ExecutesCorrectly_WithNullValue()
        {
            // Arrange
            var viewModel = new SampleViewModel();
            string? testValue = null;
            string? receivedPropertyName = null;
            viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(SampleViewModel.Name))
                {
                    receivedPropertyName = e.PropertyName;
                }
            };

            // Act
            viewModel.SetNameToValueCommand.Execute(testValue);

            // Assert
            Assert.Equal("Default Name from Command", viewModel.Name);
            Assert.Equal(nameof(SampleViewModel.Name), receivedPropertyName);
        }
    }
}
