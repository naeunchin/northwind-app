using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OLTPSystem.ViewModels
{
    public class CustomerLookupView
    {
        public string CustomerID { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
    }

    public class EmployeeLookupView
    {
        public int EmployeeID { get; set; }
        public string FullName { get; set; } = string.Empty;
    }

    public class ShipperLookupView
    {
        public int ShipperID { get; set; }
        public string CompanyName { get; set; } = string.Empty;
    }
}
