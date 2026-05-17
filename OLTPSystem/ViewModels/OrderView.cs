using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OLTPSystem.ViewModels
{
    public class OrderView
    {
        public int OrderID { get; set; }
        public string CustomerID { get; set; }
        public string CustomerCompanyName { get; set; }
        public int? EmployeeID { get; set; }
        public string EmployeeFullName { get; set; }
        public DateTime? OrderDate { get; set; }
        public DateTime? ShippedDate { get; set; }
        public decimal? Freight { get; set; }
        public string ShipCity { get; set; }
        public string ShipCountry { get; set; }
        public string ShipperName { get; set; }
    }
}
