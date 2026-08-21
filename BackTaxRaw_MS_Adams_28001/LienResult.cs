using System;

namespace BackTaxRaw_MS_Adams_28001
{
    public class LienResult
    {
        public Lien Lien { get; set; }

        public string? PARCEL { get; set; }
        public string? ADDRESS { get; set; }
        public string? OWNER { get; set; }
        public string? ACRES { get; set; }
        public string? LAND_VALUE { get; set; }
        public string? IMPROVEMENTS { get; set; }
        public string? TOTAL_VALUE { get; set; }
        public string? ASSESSED { get; set; }

        public string? PPIN { get; set; }
        public string? TOWNSHIP { get; set; }
        public string? LEGAL { get; set; }
        public string? TAX_DISTRICT { get; set; }
        public string? SECTION { get; set; }
        public string? RANGE { get; set; }

        public string? TAX_YEAR { get; set; }
        public string? RECORDS_LAST_UPDATED { get; set; }

        public string? EXEMPT_CODE { get; set; }
        public string? HOMESTEAD_CODE { get; set; }
        public string? BOOK { get; set; }
        public string? PAGE { get; set; }

        public TaxInformation? TAX_INFORMATION { get; set; }


        public string? TAX_SALE_HISTORY_JSON { get; set; }
    }
}