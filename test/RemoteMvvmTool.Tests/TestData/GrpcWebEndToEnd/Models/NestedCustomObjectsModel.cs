using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Generated.ViewModels
{
    public partial class TestViewModel : ObservableObject 
    { 
        public TestViewModel() 
        {
            Company = new CompanyInfo
            {
                Name = "Test Corp",
                EmployeeCount = 1500,
                Departments = new List<Department>
                {
                    new Department { Name = "Engineering", HeadCount = 200, Budget = 5000000.50 },
                    new Department { Name = "Sales", HeadCount = 150, Budget = 3000000.25 },
                    new Department { Name = "HR", HeadCount = 25, Budget = 750000.75 }
                }
            };
            
            IsActiveCompany = true;
            LastUpdate = new DateTime(2000, 1, 1, 0, 0, 0, 1, DateTimeKind.Utc); // Y2K with non-zero nanos
        }

        [ObservableProperty]
        private CompanyInfo _company = new();
        
        [ObservableProperty]
        private bool _isActiveCompany = false;

        [ObservableProperty]
        private DateTime _lastUpdate = DateTime.MinValue;
    }

    public class CompanyInfo
    {
        public string Name { get; set; } = "";
        public int EmployeeCount { get; set; }
        public List<Department> Departments { get; set; } = new();
    }

    public class Department
    {
        public string Name { get; set; } = "";
        public int HeadCount { get; set; }
        public double Budget { get; set; }
    }
}