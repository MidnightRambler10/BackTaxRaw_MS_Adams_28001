using System;
using System.Collections.Generic;

namespace BackTaxRaw_MS_Adams_28001
{
    public class LienRecord
    {
        public Lien SourceLien { get; set; } = null!;

        public string TaxYear { get; set; } = string.Empty;
        public string RecordsLastUpdated { get; set; } = string.Empty;
        public DateTime DateCaptured { get; set; }

        public string Owner { get; set; } = string.Empty;
        public string MailingAddress { get; set; } = string.Empty;
        public string Acres { get; set; } = string.Empty;
        public string LandValue { get; set; } = string.Empty;
        public string Improvements { get; set; } = string.Empty;
        public string TotalValue { get; set; } = string.Empty;
        public string AssessedValue { get; set; } = string.Empty;
        public string Parcel { get; set; } = string.Empty;
        public string PropertyAddress { get; set; } = string.Empty;

        public string CountyTaxDue { get; set; } = string.Empty;
        public string CountyPaid { get; set; } = string.Empty;
        public string CountyBalance { get; set; } = string.Empty;

        public string CityTaxDue { get; set; } = string.Empty;
        public string CityPaid { get; set; } = string.Empty;
        public string CityBalance { get; set; } = string.Empty;

        public string SchoolTaxDue { get; set; } = string.Empty;
        public string SchoolPaid { get; set; } = string.Empty;
        public string SchoolBalance { get; set; } = string.Empty;

        public string TotalTaxDue { get; set; } = string.Empty;
        public string TotalPaid { get; set; } = string.Empty;
        public string TotalBalance { get; set; } = string.Empty;

        public string LastPaymentDate { get; set; } = string.Empty;

        public string ExemptCode { get; set; } = string.Empty;
        public string HomesteadCode { get; set; } = string.Empty;
        public string TaxDistrict { get; set; } = string.Empty;
        public string PPIN { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string Township { get; set; } = string.Empty;
        public string Range { get; set; } = string.Empty;
        public string Book { get; set; } = string.Empty;
        public string Page { get; set; } = string.Empty;
        public string LegalDescription { get; set; } = string.Empty;

        public List<TaxSaleHistory> TaxSaleHistory { get; set; } = new List<TaxSaleHistory>();
    }
}
