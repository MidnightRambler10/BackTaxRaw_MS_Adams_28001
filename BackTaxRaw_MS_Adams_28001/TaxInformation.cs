using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackTaxRaw_MS_Adams_28001
{
    public class TaxInformation
    {
        public string? YEAR { get; set; }

        public string? COUNTY_TAX_DUE { get; set; }
        public string? COUNTY_PAID { get; set; }
        public string? COUNTY_BALANCE { get; set; }

        public string? CITY_TAX_DUE { get; set; }
        public string? CITY_PAID { get; set; }
        public string? CITY_BALANCE { get; set; }

        public string? SCHOOL_TAX_DUE { get; set; }
        public string? SCHOOL_PAID { get; set; }
        public string? SCHOOL_BALANCE { get; set; }

        public string? TOTAL_TAX_DUE { get; set; }
        public string? TOTAL_PAID { get; set; }
        public string? TOTAL_BALANCE { get; set; }

        public string? LAST_PAYMENT_DATE { get; set; }
    }
}
