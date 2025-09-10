// Demo: What the enhanced PropertyDiscoveryUtility would generate 
// for a TestViewModel with a Company property

// BEFORE (Flat categorization):
// Company -> "Generated.ViewModels.CompanyInfo" (Complex type, no details)

// AFTER (Hierarchical type graph):
/*
Client ViewModel Properties
??? Company
?   ??? Name: "Acme Corp"
?   ??? EmployeeCount: 150
?   ??? IsActive: true
?   ??? Founded: 1995-03-15
?   ??? Departments [3 items]
?       ??? [Item] (Department)
?           ??? Name: "Engineering"
?           ??? Budget: 500000.00
?           ??? Manager
?           ?   ??? Name: "John Smith"
?           ?   ??? Title: "Engineering Manager"
?           ?   ??? Email: "john.smith@acme.com"
?           ??? Employees [12 items]
?               ??? [Item] (Employee)
?                   ??? Name: "Jane Doe"
?                   ??? Title: "Senior Developer"
?                   ??? Salary: 95000.00
?                   ??? StartDate: 2020-01-15
??? LastUpdate: 1/1/2000 12:00:00 AM
??? IsActiveCompany: true
*/

// Generated WinForms code structure:
public static void LoadCompanyHierarchy() 
{
    try 
    {
        // Company (Complex)
        if (vm.Company != null) 
        {
            var companyNode = new TreeNode("Company");
            companyNode.Tag = new PropertyNodeInfo { 
                PropertyName = "Company", 
                PropertyPath = "Company", 
                Object = vm.Company, 
                IsComplexProperty = true 
            };
            rootNode.Nodes.Add(companyNode);
            
            try 
            {
                // Company.Name (Simple)
                var nameValue = vm.Company.Name?.ToString() ?? "<null>";
                var nameNode = new TreeNode("Name: " + nameValue);
                nameNode.Tag = new PropertyNodeInfo { 
                    PropertyName = "Name", 
                    PropertyPath = "Company.Name", 
                    Object = vm.Company, 
                    IsSimpleProperty = true 
                };
                companyNode.Nodes.Add(nameNode);
            }
            catch (Exception ex) 
            {
                var nameErrorNode = new TreeNode("Name: <error - " + ex.GetType().Name + ">");
                companyNode.Nodes.Add(nameErrorNode);
            }
            
            try 
            {
                // Company.EmployeeCount (Simple)
                var employeecountValue = vm.Company.EmployeeCount.ToString();
                var employeecountNode = new TreeNode("EmployeeCount: " + employeecountValue);
                employeecountNode.Tag = new PropertyNodeInfo { 
                    PropertyName = "EmployeeCount", 
                    PropertyPath = "Company.EmployeeCount", 
                    Object = vm.Company, 
                    IsSimpleProperty = true 
                };
                companyNode.Nodes.Add(employeecountNode);
            }
            catch (Exception ex) 
            {
                var employeecountErrorNode = new TreeNode("EmployeeCount: <error - " + ex.GetType().Name + ">");
                companyNode.Nodes.Add(employeecountErrorNode);
            }
            
            try 
            {
                // Company.Departments (Collection)
                if (vm.Company.Departments != null) 
                {
                    var departmentsNode = new TreeNode("Departments [" + vm.Company.Departments.Count + " items]");
                    departmentsNode.Tag = new PropertyNodeInfo { 
                        PropertyName = "Departments", 
                        PropertyPath = "Company.Departments", 
                        Object = vm.Company, 
                        IsCollectionProperty = true 
                    };
                    companyNode.Nodes.Add(departmentsNode);
                    
                    // Departments[Item] (Complex collection item)
                    try 
                    {
                        if (vm.Company.Departments[0] != null) 
                        {
                            var departmentsitemNode = new TreeNode("Departments[Item]");
                            departmentsitemNode.Tag = new PropertyNodeInfo { 
                                PropertyName = "Departments[Item]", 
                                PropertyPath = "Company.Departments[Item]", 
                                Object = vm.Company.Departments[0], 
                                IsComplexProperty = true 
                            };
                            departmentsNode.Nodes.Add(departmentsitemNode);
                            
                            // Department.Name, Department.Manager, etc. would be recursively generated here...
                        }
                    }
                    catch (Exception ex) 
                    {
                        var departmentsitemErrorNode = new TreeNode("Departments[Item]: <error - " + ex.GetType().Name + ">");
                        departmentsNode.Nodes.Add(departmentsitemErrorNode);
                    }
                }
                else 
                {
                    var departmentsNode = new TreeNode("Departments [null]");
                    companyNode.Nodes.Add(departmentsNode);
                }
            }
            catch (Exception ex) 
            {
                var departmentsErrorNode = new TreeNode("Departments: <error - " + ex.GetType().Name + ">");
                companyNode.Nodes.Add(departmentsErrorNode);
            }
        }
        else 
        {
            var companyNode = new TreeNode("Company [null]");
            rootNode.Nodes.Add(companyNode);
        }
    }
    catch (Exception ex) 
    {
        var companyErrorNode = new TreeNode("Company: <error - " + ex.GetType().Name + ">");
        rootNode.Nodes.Add(companyErrorNode);
    }
}

// Key Benefits:
// 1. ? True Object Structure: Shows actual properties like Name, EmployeeCount, Departments
// 2. ? Recursive Expansion: Departments can expand to show individual Department objects
// 3. ? Collection Item Structure: Shows what's inside collections, not just type names
// 4. ? Proper Property Paths: Company.Departments[Item].Manager.Name for full navigation
// 5. ? Error Resilience: Each property access is try/catch protected
// 6. ? Null Safety: Handles null objects gracefully at every level