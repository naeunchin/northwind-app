using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OLTPSystem.DAL;
using OLTPSystem.ViewModels;

namespace OLTPSystem.BLL
{
    public class ProductService
    {
        #region Data context setup
        private readonly NorthwindContext _context;

        public ProductService(NorthwindContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }
        #endregion
    }
}
