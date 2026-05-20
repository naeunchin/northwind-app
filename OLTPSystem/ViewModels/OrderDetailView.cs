using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OLTPSystem.ViewModels
{
    public class OrderDetailView
    {
        public int OrderID { get; set; }
        public int ProductID { get; set; }
        public decimal UnitPrice { get; set; }
        public short Quantity { get; set; }
        public float Discount { get; set; }
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// Calculates the final line item total cost, accounting for quantity totals and any applied discount percentages.
        /// </summary>
        public decimal LineTotal
        {
            get
            {
                decimal grossTotal = UnitPrice * Quantity;
                decimal discountAmount = grossTotal * (decimal)Discount;
                return grossTotal - discountAmount;
            }
        }
    }
}
