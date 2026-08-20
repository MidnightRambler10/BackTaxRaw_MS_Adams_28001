using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Threading;
using HtmlAgilityPack;
using Microsoft.Data.SqlClient;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace BackTaxRaw_MS_Adams_28001
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            List<Lien> liens = LoadLiens();

            if (liens.Count < 1)
            {
                SendEmailNotification(
                    "No Open Adams County Liens Found",
                    "No open tax liens were loaded for FIPCode 28001 for the current auction year. Scraper processing has been stopped."
                );

                return;
            }

            var options = new ChromeOptions();
            options.AddArgument("--start-maximized");

            IWebDriver driver = new ChromeDriver(options);
            driver = OpenUrl(driver);

            IterateLiens(driver, liens);
        }

        private static IWebDriver OpenUrl(IWebDriver driver)
        {
            driver.Navigate().GoToUrl("https://www.deltacomputersystems.com/MS/MS01/plinkquerym.html");
            return driver;
        }

        private static void IterateLiens(IWebDriver driver, List<Lien> liens)
        {
            List<(Lien Lien, string ErrorMessage, DateTime ErrorDateTime)> errorList =
                new List<(Lien Lien, string ErrorMessage, DateTime ErrorDateTime)>();
            List<LienRecord> lienRecords = new List<LienRecord>();

            foreach (Lien lien in liens)
            {
                try
                {
                    LienRecord lienRecord = Scrape(driver, lien);
                    lienRecords.Add(lienRecord);
                    driver = OpenUrl(driver);
                }
                catch (Exception ex)
                {
                    errorList.Add((
                        lien,
                        ex.Message,
                        DateTime.Now
                    ));

                    driver = OpenUrl(driver);

                    continue;
                }
            }

            SaveToDB(lienRecords);

            if (errorList.Count > 0)
            {
                StringBuilder body = new StringBuilder();
                body.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;color:#222;\">");
                body.Append("<h2 style=\"color:#b42318;margin-bottom:8px;\">Adams County Tax Lien Scraper Errors</h2>");
                body.Append($"<p style=\"margin:0 0 16px;\"><strong>Total failed liens:</strong> {errorList.Count}</p>");
                body.Append("<table style=\"border-collapse:collapse;width:100%;max-width:900px;\">");
                body.Append("<thead><tr style=\"background-color:#f2f4f7;\">");
                body.Append("<th style=\"border:1px solid #d0d5dd;padding:10px;text-align:left;\">Tax Lien ID</th>");
                body.Append("<th style=\"border:1px solid #d0d5dd;padding:10px;text-align:left;\">Date/Time</th>");
                body.Append("<th style=\"border:1px solid #d0d5dd;padding:10px;text-align:left;\">Error Message</th>");
                body.Append("</tr></thead><tbody>");

                foreach ((Lien Lien, string ErrorMessage, DateTime ErrorDateTime) error in errorList)
                {
                    body.Append("<tr>");
                    body.Append($"<td style=\"border:1px solid #d0d5dd;padding:10px;\">{WebUtility.HtmlEncode(error.Lien.TaxLienID)}</td>");
                    body.Append($"<td style=\"border:1px solid #d0d5dd;padding:10px;white-space:nowrap;\">{error.ErrorDateTime:yyyy-MM-dd HH:mm:ss}</td>");
                    body.Append($"<td style=\"border:1px solid #d0d5dd;padding:10px;\">{WebUtility.HtmlEncode(error.ErrorMessage)}</td>");
                    body.Append("</tr>");
                }

                body.Append("</tbody></table></div>");

                SendEmailNotification(
                    "Adams County Tax Lien Scraper - Errors",
                    body.ToString()
                );
            }
        }

        private static void SaveToDB(List<LienRecord> lienRecords)
        {
            string connectionString =
                "Server=DataServer;Database=ContentGrabber;Trusted_Connection=True;TrustServerCertificate=True;";

            const string createTableQuery = @"
IF OBJECT_ID(N'dbo.BackTaxRaw_MS_Adams_28001', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.BackTaxRaw_MS_Adams_28001;
END;

CREATE TABLE dbo.BackTaxRaw_MS_Adams_28001
(
    [TaxLienID] NVARCHAR(1000) NULL,
    [APN] NVARCHAR(1000) NULL,
    [AdvNum] NVARCHAR(1000) NULL,
    [ParcelID] NVARCHAR(1000) NULL,
    [AuctionYear] NVARCHAR(1000) NULL,
    [SourceTaxYear] NVARCHAR(1000) NULL,
    [TaxYear] NVARCHAR(1000) NULL,
    [RecordsLastUpdated] NVARCHAR(1000) NULL,
    [DateCaptured] DATETIME2 NOT NULL,
    [Owner] NVARCHAR(1000) NULL,
    [MailingAddress] NVARCHAR(MAX) NULL,
    [Acres] NVARCHAR(1000) NULL,
    [LandValue] NVARCHAR(1000) NULL,
    [Improvements] NVARCHAR(1000) NULL,
    [TotalValue] NVARCHAR(1000) NULL,
    [AssessedValue] NVARCHAR(1000) NULL,
    [Parcel] NVARCHAR(1000) NULL,
    [PropertyAddress] NVARCHAR(1000) NULL,
    [CountyTaxDue] NVARCHAR(1000) NULL,
    [CountyPaid] NVARCHAR(1000) NULL,
    [CountyBalance] NVARCHAR(1000) NULL,
    [CityTaxDue] NVARCHAR(1000) NULL,
    [CityPaid] NVARCHAR(1000) NULL,
    [CityBalance] NVARCHAR(1000) NULL,
    [SchoolTaxDue] NVARCHAR(1000) NULL,
    [SchoolPaid] NVARCHAR(1000) NULL,
    [SchoolBalance] NVARCHAR(1000) NULL,
    [TotalTaxDue] NVARCHAR(1000) NULL,
    [TotalPaid] NVARCHAR(1000) NULL,
    [TotalBalance] NVARCHAR(1000) NULL,
    [LastPaymentDate] NVARCHAR(1000) NULL,
    [ExemptCode] NVARCHAR(1000) NULL,
    [HomesteadCode] NVARCHAR(1000) NULL,
    [TaxDistrict] NVARCHAR(1000) NULL,
    [PPIN] NVARCHAR(1000) NULL,
    [Section] NVARCHAR(1000) NULL,
    [Township] NVARCHAR(1000) NULL,
    [Range] NVARCHAR(1000) NULL,
    [Book] NVARCHAR(1000) NULL,
    [Page] NVARCHAR(1000) NULL,
    [LegalDescription] NVARCHAR(MAX) NULL,
    [TaxSaleHistory] NVARCHAR(MAX) NULL
);";

            const string insertQuery = @"
INSERT INTO dbo.BackTaxRaw_MS_Adams_28001
(
    [TaxLienID], [APN], [AdvNum], [ParcelID], [AuctionYear], [SourceTaxYear],
    [TaxYear], [RecordsLastUpdated], [DateCaptured],
    [Owner], [MailingAddress], [Acres], [LandValue], [Improvements],
    [TotalValue], [AssessedValue], [Parcel], [PropertyAddress],
    [CountyTaxDue], [CountyPaid], [CountyBalance],
    [CityTaxDue], [CityPaid], [CityBalance],
    [SchoolTaxDue], [SchoolPaid], [SchoolBalance],
    [TotalTaxDue], [TotalPaid], [TotalBalance], [LastPaymentDate],
    [ExemptCode], [HomesteadCode], [TaxDistrict], [PPIN], [Section],
    [Township], [Range], [Book], [Page], [LegalDescription], [TaxSaleHistory]
)
VALUES
(
    @TaxLienID, @APN, @AdvNum, @ParcelID, @AuctionYear, @SourceTaxYear,
    @TaxYear, @RecordsLastUpdated, @DateCaptured,
    @Owner, @MailingAddress, @Acres, @LandValue, @Improvements,
    @TotalValue, @AssessedValue, @Parcel, @PropertyAddress,
    @CountyTaxDue, @CountyPaid, @CountyBalance,
    @CityTaxDue, @CityPaid, @CityBalance,
    @SchoolTaxDue, @SchoolPaid, @SchoolBalance,
    @TotalTaxDue, @TotalPaid, @TotalBalance, @LastPaymentDate,
    @ExemptCode, @HomesteadCode, @TaxDistrict, @PPIN, @Section,
    @Township, @Range, @Book, @Page, @LegalDescription, @TaxSaleHistory
);";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using SqlTransaction transaction = connection.BeginTransaction();

                try
                {
                    using (SqlCommand createTableCommand =
                        new SqlCommand(createTableQuery, connection, transaction))
                    {
                        createTableCommand.ExecuteNonQuery();
                    }

                    foreach (LienRecord record in lienRecords)
                    {
                        using (SqlCommand insertCommand =
                            new SqlCommand(insertQuery, connection, transaction))
                        {
                            void AddStringParameter(string name, string value, int size = 1000)
                            {
                                insertCommand.Parameters.Add(name, SqlDbType.NVarChar, size).Value =
                                    value ?? string.Empty;
                            }

                            AddStringParameter("@TaxLienID", record.SourceLien.TaxLienID);
                            AddStringParameter("@APN", record.SourceLien.APN);
                            AddStringParameter("@AdvNum", record.SourceLien.AdvNum);
                            AddStringParameter("@ParcelID", record.SourceLien.ParcelID);
                            AddStringParameter("@AuctionYear", record.SourceLien.AuctionYear);
                            AddStringParameter("@SourceTaxYear", record.SourceLien.TaxYear);
                            AddStringParameter("@TaxYear", record.TaxYear);
                            AddStringParameter("@RecordsLastUpdated", record.RecordsLastUpdated);
                            insertCommand.Parameters.Add("@DateCaptured", SqlDbType.DateTime2).Value =
                                record.DateCaptured;
                            AddStringParameter("@Owner", record.Owner);
                            AddStringParameter("@MailingAddress", record.MailingAddress, -1);
                            AddStringParameter("@Acres", record.Acres);
                            AddStringParameter("@LandValue", record.LandValue);
                            AddStringParameter("@Improvements", record.Improvements);
                            AddStringParameter("@TotalValue", record.TotalValue);
                            AddStringParameter("@AssessedValue", record.AssessedValue);
                            AddStringParameter("@Parcel", record.Parcel);
                            AddStringParameter("@PropertyAddress", record.PropertyAddress);
                            AddStringParameter("@CountyTaxDue", record.CountyTaxDue);
                            AddStringParameter("@CountyPaid", record.CountyPaid);
                            AddStringParameter("@CountyBalance", record.CountyBalance);
                            AddStringParameter("@CityTaxDue", record.CityTaxDue);
                            AddStringParameter("@CityPaid", record.CityPaid);
                            AddStringParameter("@CityBalance", record.CityBalance);
                            AddStringParameter("@SchoolTaxDue", record.SchoolTaxDue);
                            AddStringParameter("@SchoolPaid", record.SchoolPaid);
                            AddStringParameter("@SchoolBalance", record.SchoolBalance);
                            AddStringParameter("@TotalTaxDue", record.TotalTaxDue);
                            AddStringParameter("@TotalPaid", record.TotalPaid);
                            AddStringParameter("@TotalBalance", record.TotalBalance);
                            AddStringParameter("@LastPaymentDate", record.LastPaymentDate);
                            AddStringParameter("@ExemptCode", record.ExemptCode);
                            AddStringParameter("@HomesteadCode", record.HomesteadCode);
                            AddStringParameter("@TaxDistrict", record.TaxDistrict);
                            AddStringParameter("@PPIN", record.PPIN);
                            AddStringParameter("@Section", record.Section);
                            AddStringParameter("@Township", record.Township);
                            AddStringParameter("@Range", record.Range);
                            AddStringParameter("@Book", record.Book);
                            AddStringParameter("@Page", record.Page);
                            AddStringParameter("@LegalDescription", record.LegalDescription, -1);
                            AddStringParameter(
                                "@TaxSaleHistory",
                                JsonSerializer.Serialize(record.TaxSaleHistory),
                                -1
                            );

                            insertCommand.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
                catch
                {
                    try
                    {
                        transaction.Rollback();
                    }
                    catch
                    {
                    }

                    throw;
                }
            }
        }

        private static LienRecord Scrape(IWebDriver driver, Lien lien)
        {
            IWebElement ppin =
                driver.FindElement(By.Name("HTMPPIN"));

            ppin.SendKeys(lien.AdvNum);

            IWebElement submitButton =
                driver.FindElement(By.Name("HTMSUBMIT"));

            submitButton.Click();

            Thread.Sleep(3000);

            IReadOnlyCollection<IWebElement> ppinLinks =
                driver.FindElements(
                    By.XPath("//table[.//td//*[normalize-space()='PPIN']]//tr[position() > 1]/td[1]/a")
                );

            if (ppinLinks.Count == 0)
            {
                throw new Exception(
                    $"No property result found for PPIN {lien.AdvNum}."
                );
            }

            ppinLinks.First().Click();

            Thread.Sleep(2000);

            HtmlDocument document = new HtmlDocument();
            document.LoadHtml(driver.PageSource);

            const string lowercaseLetters = "abcdefghijklmnopqrstuvwxyz";
            const string uppercaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            HtmlNode propertyDetailTable = document.DocumentNode.SelectSingleNode(
                $"//*[translate(normalize-space(.), '{lowercaseLetters}', '{uppercaseLetters}')='PROPERTY DETAIL']/ancestor-or-self::table[1]"
            ) ?? throw new Exception("PROPERTY DETAIL table was not found on the parcel detail page.");

            string GetPropertyDetailValue(string label)
            {
                HtmlNode labelCell = propertyDetailTable.SelectSingleNode(
                    $".//td[normalize-space(translate(., '{lowercaseLetters}:', '{uppercaseLetters}'))='{label}']"
                ) ?? throw new Exception($"{label} was not found in the PROPERTY DETAIL table.");

                HtmlNode valueCell = labelCell.SelectSingleNode("following-sibling::td[1]")
                    ?? throw new Exception($"No value was found for {label} in the PROPERTY DETAIL table.");

                string decodedValue = HtmlEntity.DeEntitize(valueCell.InnerText)
                    .Replace('\u00A0', ' ');

                return string.Join(
                    " ",
                    decodedValue.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                ).Trim();
            }

            HtmlNode ownerLabelCell = propertyDetailTable.SelectSingleNode(
                $".//td[normalize-space(translate(., '{lowercaseLetters}:', '{uppercaseLetters}'))='OWNER']"
            ) ?? throw new Exception("OWNER was not found in the PROPERTY DETAIL table.");

            HtmlNode ownerRow = ownerLabelCell.SelectSingleNode("ancestor::tr[1]")
                ?? throw new Exception("The OWNER row was not found in the PROPERTY DETAIL table.");

            List<string> mailingAddressFragments = new List<string>();
            IEnumerable<HtmlNode> rowsAfterOwner =
                ownerRow.SelectNodes("following-sibling::tr")?.Cast<HtmlNode>()
                ?? Enumerable.Empty<HtmlNode>();

            foreach (HtmlNode row in rowsAfterOwner)
            {
                HtmlNode? parcelLabelCell = row.SelectSingleNode(
                    $"./td[normalize-space(translate(., '{lowercaseLetters}:', '{uppercaseLetters}'))='PARCEL']"
                );

                if (parcelLabelCell != null)
                {
                    break;
                }

                HtmlNode? addressCell = row.SelectSingleNode("./td[2]");
                if (addressCell == null)
                {
                    continue;
                }

                string addressFragment = HtmlEntity.DeEntitize(addressCell.InnerText)
                    .Replace('\u00A0', ' ');
                addressFragment = string.Join(
                    " ",
                    addressFragment.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                ).Trim();

                if (addressFragment.Length > 0)
                {
                    mailingAddressFragments.Add(addressFragment);
                }
            }

            string mailingAddress = string.Join(" ", mailingAddressFragments);

            HtmlNode taxInformationHeading = document.DocumentNode.SelectSingleNode(
                $"//*[normalize-space(translate(., '{lowercaseLetters}:', '{uppercaseLetters}'))='TAX INFORMATION']"
            ) ?? throw new Exception("TAX INFORMATION section was not found on the parcel detail page.");

            HtmlNode taxInformationTable = taxInformationHeading.SelectSingleNode("following::table[1]")
                ?? throw new Exception("TAX INFORMATION table was not found on the parcel detail page.");

            string CleanTaxInformationValue(HtmlNode node)
            {
                string decodedValue = HtmlEntity.DeEntitize(node.InnerText)
                    .Replace('\u00A0', ' ');

                return string.Join(
                    " ",
                    decodedValue.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                ).Trim();
            }

            (string TaxDue, string Paid, string Balance) GetTaxInformationValues(string label)
            {
                HtmlNode row = taxInformationTable.SelectSingleNode(
                    $".//tr[td[1][normalize-space(translate(., '{lowercaseLetters}:', '{uppercaseLetters}'))='{label}']]"
                ) ?? throw new Exception($"{label} was not found in the TAX INFORMATION table.");

                HtmlNodeCollection cells = row.SelectNodes("./td")
                    ?? throw new Exception($"No values were found for {label} in the TAX INFORMATION table.");

                if (cells.Count < 4)
                {
                    throw new Exception($"The TAX INFORMATION row for {label} is incomplete.");
                }

                return (
                    CleanTaxInformationValue(cells[1]),
                    CleanTaxInformationValue(cells[2]),
                    CleanTaxInformationValue(cells[3])
                );
            }

            string GetTaxInformationValue(string label)
            {
                HtmlNode labelCell = taxInformationTable.SelectSingleNode(
                    $".//td[.//b[normalize-space(translate(., '{lowercaseLetters}', '{uppercaseLetters}'))='{label}']]"
                ) ?? throw new Exception($"{label} was not found in the TAX INFORMATION table.");

                HtmlNode valueCell = labelCell.SelectSingleNode("following-sibling::td[1]")
                    ?? throw new Exception($"No value was found for {label} in the TAX INFORMATION table.");

                return CleanTaxInformationValue(valueCell);
            }

            (string TaxDue, string Paid, string Balance) countyTax =
                GetTaxInformationValues("COUNTY");
            (string TaxDue, string Paid, string Balance) cityTax =
                GetTaxInformationValues("CITY");
            (string TaxDue, string Paid, string Balance) schoolTax =
                GetTaxInformationValues("SCHOOL");
            (string TaxDue, string Paid, string Balance) totalTax =
                GetTaxInformationValues("TOTAL");

            System.Text.RegularExpressions.Match totalBalanceMatch =
                System.Text.RegularExpressions.Regex.Match(
                    totalTax.Balance,
                    @"-?[\d,]+(?:\.\d+)?"
                );
            string totalBalance = totalBalanceMatch.Success
                ? totalBalanceMatch.Value
                : totalTax.Balance;

            string lastPaymentDate = GetTaxInformationValue("LAST PAYMENT DATE");

            HtmlNode miscellaneousInformationHeading = document.DocumentNode.SelectSingleNode(
                $"//*[normalize-space(translate(., '{lowercaseLetters}:', '{uppercaseLetters}'))='MISCELLANEOUS INFORMATION']"
            ) ?? throw new Exception("MISCELLANEOUS INFORMATION section was not found on the parcel detail page.");

            HtmlNode miscellaneousInformationTable =
                miscellaneousInformationHeading.SelectSingleNode("following::table[1]")
                ?? throw new Exception("MISCELLANEOUS INFORMATION table was not found on the parcel detail page.");

            string GetMiscellaneousInformationValue(string label)
            {
                HtmlNode labelCell = miscellaneousInformationTable.SelectSingleNode(
                    $".//td[.//b[normalize-space(translate(., '{lowercaseLetters}', '{uppercaseLetters}'))='{label}']]"
                ) ?? throw new Exception($"{label} was not found in the MISCELLANEOUS INFORMATION table.");

                HtmlNode? valueCell = labelCell.SelectSingleNode("following-sibling::td[1]");
                if (valueCell == null)
                {
                    return string.Empty;
                }

                string decodedValue = HtmlEntity.DeEntitize(valueCell.InnerText)
                    .Replace('\u00A0', ' ');

                return string.Join(
                    " ",
                    decodedValue.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                ).Trim();
            }

            HtmlNode legalLabelCell = miscellaneousInformationTable.SelectSingleNode(
                $".//td[.//b[normalize-space(translate(., '{lowercaseLetters}', '{uppercaseLetters}'))='LEGAL']]"
            ) ?? throw new Exception("LEGAL was not found in the MISCELLANEOUS INFORMATION table.");

            HtmlNode legalRow = legalLabelCell.SelectSingleNode("ancestor::tr[1]")
                ?? throw new Exception("The LEGAL row was not found in the MISCELLANEOUS INFORMATION table.");

            List<string> legalDescriptionFragments = new List<string>();
            HtmlNode? firstLegalValueCell = legalLabelCell.SelectSingleNode("following-sibling::td[1]");
            if (firstLegalValueCell != null)
            {
                string firstLegalFragment = HtmlEntity.DeEntitize(firstLegalValueCell.InnerText)
                    .Replace('\u00A0', ' ');
                firstLegalFragment = string.Join(
                    " ",
                    firstLegalFragment.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                ).Trim();

                if (firstLegalFragment.Length > 0)
                {
                    legalDescriptionFragments.Add(firstLegalFragment);
                }
            }

            IEnumerable<HtmlNode> rowsAfterLegal =
                legalRow.SelectNodes("following-sibling::tr")?.Cast<HtmlNode>()
                ?? Enumerable.Empty<HtmlNode>();

            foreach (HtmlNode row in rowsAfterLegal)
            {
                HtmlNode? sectionLabelCell = row.SelectSingleNode(
                    $"./td[normalize-space(translate(., '{lowercaseLetters}:', '{uppercaseLetters}'))='SECTION']"
                );

                if (sectionLabelCell != null)
                {
                    break;
                }

                HtmlNode? legalFragmentCell = row.SelectSingleNode("./td[4]");
                if (legalFragmentCell == null)
                {
                    continue;
                }

                string legalFragment = HtmlEntity.DeEntitize(legalFragmentCell.InnerText)
                    .Replace('\u00A0', ' ');
                legalFragment = string.Join(
                    " ",
                    legalFragment.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                ).Trim();

                if (legalFragment.Length > 0)
                {
                    legalDescriptionFragments.Add(legalFragment);
                }
            }

            string legalDescription = string.Join(" ", legalDescriptionFragments);

            string GetInlineMiscellaneousInformationValue(string label)
            {
                HtmlNode cell = miscellaneousInformationTable.SelectSingleNode(
                    $".//td[not(.//td) and starts-with(normalize-space(translate(., '{lowercaseLetters}', '{uppercaseLetters}')), '{label.ToUpperInvariant()}')]"
                ) ?? throw new Exception($"{label} was not found in the MISCELLANEOUS INFORMATION table.");

                string cellText = HtmlEntity.DeEntitize(cell.InnerText)
                    .Replace('\u00A0', ' ');
                cellText = string.Join(
                    " ",
                    cellText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                ).Trim();

                return cellText
                    .Substring(label.Length)
                    .Trim()
                    .TrimStart(':')
                    .Trim();
            }

            string book = GetInlineMiscellaneousInformationValue("Book");
            string page = GetInlineMiscellaneousInformationValue("Page");

            HtmlNode taxYearNode = document.DocumentNode.SelectSingleNode(
                "//text()[contains(normalize-space(.), 'Tax Year')]/parent::*"
            ) ?? throw new Exception("Tax Year was not found on the parcel detail page.");

            string taxYearText = HtmlEntity.DeEntitize(taxYearNode.InnerText).Trim();
            int taxYearLabelIndex = taxYearText.IndexOf("Tax Year", StringComparison.OrdinalIgnoreCase);
            string taxYear = taxYearText
                .Substring(taxYearLabelIndex + "Tax Year".Length)
                .Trim()
                .TrimStart(':')
                .Trim();

            HtmlNode recordsLastUpdatedNode = document.DocumentNode.SelectSingleNode(
                "//text()[contains(normalize-space(.), 'Records Last Updated')]/parent::*"
            ) ?? throw new Exception("Records Last Updated was not found on the parcel detail page.");

            string recordsLastUpdatedText = HtmlEntity.DeEntitize(recordsLastUpdatedNode.InnerText).Trim();
            int recordsLastUpdatedLabelIndex = recordsLastUpdatedText.IndexOf(
                "Records Last Updated",
                StringComparison.OrdinalIgnoreCase
            );
            string recordsLastUpdated = recordsLastUpdatedText
                .Substring(recordsLastUpdatedLabelIndex + "Records Last Updated".Length)
                .Trim()
                .TrimStart(':')
                .Trim();

            LienRecord record = new LienRecord
            {
                SourceLien = lien,
                DateCaptured = DateTime.Now,
                TaxYear = taxYear,
                RecordsLastUpdated = recordsLastUpdated,
                Owner = GetPropertyDetailValue("OWNER"),
                MailingAddress = mailingAddress,
                Acres = GetPropertyDetailValue("ACRES"),
                LandValue = GetPropertyDetailValue("LAND VALUE"),
                Improvements = GetPropertyDetailValue("IMPROVEMENTS"),
                TotalValue = GetPropertyDetailValue("TOTAL VALUE"),
                AssessedValue = GetPropertyDetailValue("ASSESSED"),
                Parcel = GetPropertyDetailValue("PARCEL"),
                PropertyAddress = GetPropertyDetailValue("ADDRESS"),
                CountyTaxDue = countyTax.TaxDue,
                CountyPaid = countyTax.Paid,
                CountyBalance = countyTax.Balance,
                CityTaxDue = cityTax.TaxDue,
                CityPaid = cityTax.Paid,
                CityBalance = cityTax.Balance,
                SchoolTaxDue = schoolTax.TaxDue,
                SchoolPaid = schoolTax.Paid,
                SchoolBalance = schoolTax.Balance,
                TotalTaxDue = totalTax.TaxDue,
                TotalPaid = totalTax.Paid,
                TotalBalance = totalBalance,
                LastPaymentDate = lastPaymentDate,
                ExemptCode = GetMiscellaneousInformationValue("EXEMPT CODE"),
                HomesteadCode = GetMiscellaneousInformationValue("HOMESTEAD CODE"),
                TaxDistrict = GetMiscellaneousInformationValue("TAX DISTRICT"),
                PPIN = GetMiscellaneousInformationValue("PPIN"),
                Section = GetMiscellaneousInformationValue("SECTION"),
                Township = GetMiscellaneousInformationValue("TOWNSHIP"),
                Range = GetMiscellaneousInformationValue("RANGE"),
                Book = book,
                Page = page,
                LegalDescription = legalDescription
            };

            HtmlNode? taxSalesHistoryHeadingCell = miscellaneousInformationTable.SelectSingleNode(
                $".//td[normalize-space(translate(., '{lowercaseLetters}', '{uppercaseLetters}'))='TAX SALES HISTORY, FOR UNPAID TAXES']"
            );

            if (taxSalesHistoryHeadingCell != null)
            {
                HtmlNode? taxSalesHistoryHeadingRow =
                    taxSalesHistoryHeadingCell.SelectSingleNode("ancestor::tr[1]");

                IEnumerable<HtmlNode> taxSalesHistoryRows =
                    taxSalesHistoryHeadingRow?.SelectNodes("following-sibling::tr")?.Cast<HtmlNode>()
                    ?? Enumerable.Empty<HtmlNode>();

                foreach (HtmlNode historyRow in taxSalesHistoryRows)
                {
                    HtmlNodeCollection? cells = historyRow.SelectNodes("./td");
                    if (cells == null || cells.Count < 4)
                    {
                        continue;
                    }

                    string year = HtmlEntity.DeEntitize(cells[0].InnerText)
                        .Replace('\u00A0', ' ');
                    year = string.Join(
                        " ",
                        year.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    ).Trim();

                    string soldTo = HtmlEntity.DeEntitize(cells[1].InnerText)
                        .Replace('\u00A0', ' ');
                    soldTo = string.Join(
                        " ",
                        soldTo.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    ).Trim();

                    string redeemedDateBy = HtmlEntity.DeEntitize(cells[3].InnerText)
                        .Replace('\u00A0', ' ');
                    redeemedDateBy = string.Join(
                        " ",
                        redeemedDateBy.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    ).Trim();

                    if (year.Equals("Year", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (year.Length == 0 && soldTo.Length == 0 && redeemedDateBy.Length == 0)
                    {
                        continue;
                    }

                    TaxSaleHistory history = new TaxSaleHistory
                    {
                        Year = year,
                        SoldTo = soldTo,
                        RedeemedDateBy = redeemedDateBy
                    };

                    record.TaxSaleHistory.Add(history);
                }
            }

            return record;
        }

        private static List<Lien> LoadLiens()
        {
            string connectionString =
                "Server=DataServer;Database=ContentGrabber;Trusted_Connection=True;TrustServerCertificate=True;";

            const string query = @"
SELECT
    TaxLienID,
    TaxLienAPN,
    AdvertisementNumber,
    ParcelID,
    AuctionYear,
    DelinquentYear
FROM staging.dbo.TaxLiens
WHERE TaxLienStatus = 'OPEN'
  AND AuctionYear = YEAR(GETDATE())
  AND FIPCode = '28001'";

            List<Lien> liens = new List<Lien>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Lien lien = new Lien
                            {
                                TaxLienID = Convert.ToString(reader["TaxLienID"]) ?? string.Empty,
                                APN = Convert.ToString(reader["TaxLienAPN"]) ?? string.Empty,
                                AdvNum = Convert.ToString(reader["AdvertisementNumber"]) ?? string.Empty,
                                ParcelID = Convert.ToString(reader["ParcelID"]) ?? string.Empty,
                                AuctionYear = Convert.ToString(reader["AuctionYear"]) ?? string.Empty,
                                TaxYear = Convert.ToString(reader["DelinquentYear"]) ?? string.Empty
                            };

                            liens.Add(lien);
                        }
                    }
                }
            }

            return liens;
        }

        private static void SendEmailNotification(string subject, string body)
        {
            MailMessage message = new MailMessage();
            SmtpClient smtp = new SmtpClient();

            message.From = new MailAddress("abshir.saleem@lumentumllc.com");
            message.To.Add(new MailAddress("abshir.saleem@lumentumllc.com"));
            message.To.Add(new MailAddress("alejandro.henriquez@lumentumllc.com"));

            message.Subject = subject;
            message.IsBodyHtml = true;
            message.Body = body;

            smtp.Port = 587;
            smtp.Host = "smtp-mail.outlook.com";
            smtp.EnableSsl = true;
            smtp.UseDefaultCredentials = false;
            smtp.Credentials = new NetworkCredential(
                "abshir.saleem@lumentumllc.com",
                Environment.GetEnvironmentVariable("SMTP_PASSWORD")
                    ?? throw new InvalidOperationException("SMTP_PASSWORD environment variable is not set.")
            );
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;

            smtp.Send(message);
        }
    }
}
