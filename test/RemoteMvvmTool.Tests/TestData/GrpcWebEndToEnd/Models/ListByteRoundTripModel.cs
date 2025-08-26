using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Generated.ViewModels
{
    public partial class TestViewModel : ObservableObject 
    { 
        public TestViewModel() 
        {
            // Test List<byte> specifically to verify it's handled as bytes not repeated uint32
            // Using unique values to make validation easier
            BytesList = new List<byte> { 11, 22, 33, 44, 55, 66 }; // All unique values
            
            // Also test with empty list
            EmptyBytesList = new List<byte>();
            
            // And ReadOnlyMemory<byte> for comparison - using unique values
            ReadOnlyBuffer = new ReadOnlyMemory<byte>(new byte[] { 77, 88 }); // Unique values
            
            // Regular properties for verification - all unique
            ByteCount = BytesList.Count; // Should be 6
            HasData = BytesList.Any();   // Should be true (1)
            MaxByte = BytesList.Max();   // Should be 66 (will be duplicate)
            MinByte = BytesList.Min();   // Should be 11 (will be duplicate)
        }

        [ObservableProperty]
        private List<byte> _bytesList = new();

        [ObservableProperty]
        private List<byte> _emptyBytesList = new();

        [ObservableProperty]
        private ReadOnlyMemory<byte> _readOnlyBuffer;

        [ObservableProperty]
        private int _byteCount = 0;

        [ObservableProperty]
        private bool _hasData = false;

        [ObservableProperty]
        private byte _maxByte = 0;

        [ObservableProperty]
        private byte _minByte = 0;
    }
}