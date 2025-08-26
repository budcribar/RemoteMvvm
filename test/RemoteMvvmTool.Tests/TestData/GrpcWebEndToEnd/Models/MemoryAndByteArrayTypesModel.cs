using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Generated.ViewModels
{
    public partial class TestViewModel : ObservableObject 
    { 
        public TestViewModel() 
        {
            // Test memory-based types and byte arrays
            ImageData = new byte[] { 255, 128, 64, 32, 16, 8, 4, 2, 1, 0 };
            
            // Memory<byte> - should be supported as BytesValue
            var bytes = new byte[] { 100, 200, 50, 150 };
            BufferData = new Memory<byte>(bytes);
            
            // Regular collections with bytes for comparison
            BytesList = new List<byte> { 10, 20, 30 };
            
            DataLength = ImageData.Length;
            IsCompressed = false;
        }

        [ObservableProperty]
        private byte[] _imageData = Array.Empty<byte>();

        [ObservableProperty]
        private Memory<byte> _bufferData;

        [ObservableProperty]
        private List<byte> _bytesList = new();

        [ObservableProperty]
        private int _dataLength = 0;

        [ObservableProperty]
        private bool _isCompressed = true;
    }
}