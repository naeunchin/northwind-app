using OLTPSystem.DAL;
using OLTPSystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OLTPSystem.BLL
{
    public class OrderService
    {
        #region Data context setup
        private readonly NorthwindContext _context;

        public OrderService(NorthwindContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }
        #endregion
    }
}
