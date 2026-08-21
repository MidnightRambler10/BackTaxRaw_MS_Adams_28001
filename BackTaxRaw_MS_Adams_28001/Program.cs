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
            List<LienResult> successfulLiens =
                new List<LienResult>();

            List<(Lien Lien, string ErrorMessage, DateTime ErrorDateTime)> errorList =
                new List<(Lien Lien, string ErrorMessage, DateTime ErrorDateTime)>();

            foreach (Lien lien in liens)
            {
                try
                {
                    LienResult result = Scrape(driver, lien);

                    successfulLiens.Add(result);

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

            SaveToDB(successfulLiens);

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

            driver.Quit();
        }



        private static LienResult Scrape(IWebDriver driver, Lien lien)
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
                    By.XPath(
                        "//table[.//td//*[normalize-space()='PPIN']]" +
                        "//tr[position() > 1]/td[1]/a"
                    )
                );

            if (ppinLinks.Count == 0)
            {
                throw new Exception(
                    $"No property result found for PPIN {lien.AdvNum}."
                );
            }

            ppinLinks.First().Click();

            Thread.Sleep(2000);

            string? GetValue(string label)
            {
                IReadOnlyCollection<IWebElement> elements =
                    driver.FindElements(
                        By.XPath(
                            $"//td[.//b[" +
                            $"normalize-space(translate(., ':', ''))='{label}'" +
                            $"]]/following-sibling::td[1]"
                        )
                    );

                if (elements.Count == 0)
                {
                    return null;
                }

                string value = elements
                    .First()
                    .Text
                    .Trim();

                return string.IsNullOrWhiteSpace(value)
                    ? null
                    : value;
            }

            string? taxYear = null;
            string? recordsLastUpdated = null;

            IReadOnlyCollection<IWebElement> taxYearElements =
                driver.FindElements(
                    By.XPath(
                        "//b[contains(normalize-space(.), 'Tax Year')]"
                    )
                );

            if (taxYearElements.Count > 0)
            {
                string value = taxYearElements
                    .First()
                    .Text
                    .Replace("Tax Year", string.Empty)
                    .Trim();

                taxYear =
                    string.IsNullOrWhiteSpace(value)
                        ? null
                        : value;
            }

            IReadOnlyCollection<IWebElement> recordsUpdatedElements =
                driver.FindElements(
                    By.XPath(
                        "//font[contains(normalize-space(.), 'Records Last Updated')]/b"
                    )
                );

            if (recordsUpdatedElements.Count > 0)
            {
                string value = recordsUpdatedElements
                    .First()
                    .Text
                    .Trim();

                recordsLastUpdated =
                    string.IsNullOrWhiteSpace(value)
                        ? null
                        : value;
            }

            IWebElement? taxInformationTable = null;

            IReadOnlyCollection<IWebElement> taxInformationTables =
                driver.FindElements(
                    By.XPath(
                        "//tr[td//b[normalize-space(.)='TAX INFORMATION']]" +
                        "/following-sibling::tr[1]//table"
                    )
                );

            if (taxInformationTables.Count > 0)
            {
                taxInformationTable =
                    taxInformationTables.First();
            }

            string? GetTaxValue(string rowLabel, int columnIndex)
            {
                if (taxInformationTable == null)
                {
                    return null;
                }

                IReadOnlyCollection<IWebElement> rows =
                    taxInformationTable.FindElements(
                        By.XPath(
                            $".//tr[" +
                            $"td[1]//b[normalize-space(.)='{rowLabel}']" +
                            $"]"
                        )
                    );

                if (rows.Count == 0)
                {
                    return null;
                }

                IReadOnlyCollection<IWebElement> cells =
                    rows
                        .First()
                        .FindElements(By.XPath("./td"));

                if (cells.Count <= columnIndex)
                {
                    return null;
                }

                string value = cells
                    .ElementAt(columnIndex)
                    .Text
                    .Trim();

                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                if (
                    rowLabel == "TOTAL" &&
                    columnIndex == 3
                )
                {
                    string[] pieces =
                        value.Split(
                            ' ',
                            StringSplitOptions.RemoveEmptyEntries
                        );

                    if (pieces.Length > 0)
                    {
                        value = pieces[0];
                    }
                }

                return string.IsNullOrWhiteSpace(value)
                    ? null
                    : value;
            }

            string? taxInformationYear = null;

            if (taxInformationTable != null)
            {
                IReadOnlyCollection<IWebElement> taxInformationYearElements =
                    taxInformationTable.FindElements(
                        By.XPath(
                            ".//tr[" +
                            "td[1]//b[starts-with(normalize-space(.), 'YEAR ')]" +
                            "]/td[1]//b"
                        )
                    );

                if (taxInformationYearElements.Count > 0)
                {
                    string value = taxInformationYearElements
                        .First()
                        .Text
                        .Replace("YEAR", string.Empty)
                        .Trim();

                    taxInformationYear =
                        string.IsNullOrWhiteSpace(value)
                            ? null
                            : value;
                }
            }

            string? lastPaymentDate = null;

            if (taxInformationTable != null)
            {
                IReadOnlyCollection<IWebElement> lastPaymentRows =
                    taxInformationTable.FindElements(
                        By.XPath(
                            ".//tr[" +
                            "td//b[normalize-space(.)='LAST PAYMENT DATE']" +
                            "]"
                        )
                    );

                if (lastPaymentRows.Count > 0)
                {
                    IReadOnlyCollection<IWebElement> cells =
                        lastPaymentRows
                            .First()
                            .FindElements(By.XPath("./td"));

                    if (cells.Count > 1)
                    {
                        string value = cells
                            .ElementAt(1)
                            .Text
                            .Trim();

                        lastPaymentDate =
                            string.IsNullOrWhiteSpace(value)
                                ? null
                                : value;
                    }
                }
            }

            IWebElement? miscellaneousInformationTable = null;

            IReadOnlyCollection<IWebElement> miscellaneousInformationTables =
                driver.FindElements(
                    By.XPath(
                        "//tr[td//b[normalize-space(.)='MISCELLANEOUS INFORMATION']]" +
                        "/following-sibling::tr[1]//table"
                    )
                );

            if (miscellaneousInformationTables.Count > 0)
            {
                miscellaneousInformationTable =
                    miscellaneousInformationTables.First();
            }

            string? GetMiscellaneousValue(string label)
            {
                if (miscellaneousInformationTable == null)
                {
                    return null;
                }

                IReadOnlyCollection<IWebElement> elements =
                    miscellaneousInformationTable.FindElements(
                        By.XPath(
                            $".//td[.//b[" +
                            $"normalize-space(translate(., ':', ''))='{label}'" +
                            $"]]/following-sibling::td[1]"
                        )
                    );

                if (elements.Count == 0)
                {
                    return null;
                }

                string value = elements
                    .First()
                    .Text
                    .Trim();

                return string.IsNullOrWhiteSpace(value)
                    ? null
                    : value;
            }

            string? GetBookPageValue(string label)
            {
                if (miscellaneousInformationTable == null)
                {
                    return null;
                }

                IReadOnlyCollection<IWebElement> elements =
                    miscellaneousInformationTable.FindElements(
                        By.XPath(
                            $".//td[font/b[normalize-space(.)='{label}']]"
                        )
                    );

                if (elements.Count == 0)
                {
                    return null;
                }

                string value = elements
                    .First()
                    .Text
                    .Replace(label, string.Empty)
                    .Trim();

                return string.IsNullOrWhiteSpace(value)
                    ? null
                    : value;
            }

            string? GetLegalValue()
            {
                if (miscellaneousInformationTable == null)
                {
                    return null;
                }

                IReadOnlyCollection<IWebElement> legalRows =
                    miscellaneousInformationTable.FindElements(
                        By.XPath(
                            ".//tr[td[3]//b[normalize-space(.)='LEGAL']]"
                        )
                    );

                if (legalRows.Count == 0)
                {
                    return null;
                }

                IWebElement legalRow =
                    legalRows.First();

                IReadOnlyCollection<IWebElement> allRows =
                    miscellaneousInformationTable.FindElements(
                        By.XPath(".//tr")
                    );

                List<string> legalParts =
                    new List<string>();

                bool collectLegal = false;

                foreach (IWebElement row in allRows)
                {
                    if (row.Equals(legalRow))
                    {
                        collectLegal = true;
                    }

                    if (!collectLegal)
                    {
                        continue;
                    }

                    IReadOnlyCollection<IWebElement> bookElements =
                        row.FindElements(
                            By.XPath(
                                "./td[font/b[normalize-space(.)='Book']]"
                            )
                        );

                    if (bookElements.Count > 0)
                    {
                        break;
                    }

                    IReadOnlyCollection<IWebElement> cells =
                        row.FindElements(By.XPath("./td"));

                    if (cells.Count < 4)
                    {
                        continue;
                    }

                    string value = cells
                        .ElementAt(3)
                        .Text
                        .Trim();

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        legalParts.Add(value);
                    }
                }

                if (legalParts.Count == 0)
                {
                    return null;
                }

                return string.Join(
                    " ",
                    legalParts
                );
            }

            string? GetTaxSaleHistoryJson()
            {
                IReadOnlyCollection<IWebElement> historyHeaderRows =
                    driver.FindElements(
                        By.XPath(
                            "//tr[" +
                            "count(td)=1 and " +
                            "td[@colspan='4']//b[" +
                            "normalize-space(.)='TAX SALES HISTORY, FOR UNPAID TAXES'" +
                            "]" +
                            "]"
                        )
                    );

                if (historyHeaderRows.Count == 0)
                {
                    return null;
                }

                IWebElement historyHeaderRow =
                    historyHeaderRows.First();

                IReadOnlyCollection<IWebElement> historyRows =
                    historyHeaderRow.FindElements(
                        By.XPath("./following-sibling::tr")
                    );

                List<object> history =
                    new List<object>();

                foreach (IWebElement row in historyRows)
                {
                    IReadOnlyCollection<IWebElement> cells =
                        row.FindElements(
                            By.XPath("./td")
                        );

                    if (cells.Count < 4)
                    {
                        continue;
                    }

                    string year =
                        cells.ElementAt(0)
                            .Text
                            .Trim();

                    if (!int.TryParse(year, out _))
                    {
                        continue;
                    }

                    string soldTo =
                        cells.ElementAt(1)
                            .Text
                            .Trim();

                    string redeemedDateBy =
                        cells.ElementAt(3)
                            .Text
                            .Trim();

                    history.Add(
                        new
                        {
                            Year = year,

                            SoldTo =
                                string.IsNullOrWhiteSpace(soldTo)
                                    ? null
                                    : soldTo,

                            RedeemedDateBy =
                                string.IsNullOrWhiteSpace(redeemedDateBy)
                                    ? null
                                    : redeemedDateBy
                        }
                    );
                }

                if (history.Count == 0)
                {
                    return null;
                }

                return JsonSerializer.Serialize(history);
            }
            TaxInformation taxInformation =
                new TaxInformation
                {
                    YEAR =
                        taxInformationYear,

                    COUNTY_TAX_DUE =
                        GetTaxValue("COUNTY", 1),

                    COUNTY_PAID =
                        GetTaxValue("COUNTY", 2),

                    COUNTY_BALANCE =
                        GetTaxValue("COUNTY", 3),

                    CITY_TAX_DUE =
                        GetTaxValue("CITY", 1),

                    CITY_PAID =
                        GetTaxValue("CITY", 2),

                    CITY_BALANCE =
                        GetTaxValue("CITY", 3),

                    SCHOOL_TAX_DUE =
                        GetTaxValue("SCHOOL", 1),

                    SCHOOL_PAID =
                        GetTaxValue("SCHOOL", 2),

                    SCHOOL_BALANCE =
                        GetTaxValue("SCHOOL", 3),

                    TOTAL_TAX_DUE =
                        GetTaxValue("TOTAL", 1),

                    TOTAL_PAID =
                        GetTaxValue("TOTAL", 2),

                    TOTAL_BALANCE =
                        GetTaxValue("TOTAL", 3),

                    LAST_PAYMENT_DATE =
                        lastPaymentDate
                };

            return new LienResult
            {
                Lien =
                    lien,

                PARCEL =
                    GetValue("PARCEL"),

                ADDRESS =
                    GetValue("ADDRESS"),

                OWNER =
                    GetValue("OWNER"),

                ACRES =
                    GetValue("ACRES"),

                LAND_VALUE =
                    GetValue("LAND VALUE"),

                IMPROVEMENTS =
                    GetValue("IMPROVEMENTS"),

                TOTAL_VALUE =
                    GetValue("TOTAL VALUE"),

                ASSESSED =
                    GetValue("ASSESSED"),

                PPIN =
                    GetMiscellaneousValue("PPIN"),

                TOWNSHIP =
                    GetMiscellaneousValue("TOWNSHIP"),

                LEGAL =
                    GetLegalValue(),

                TAX_DISTRICT =
                    GetMiscellaneousValue("TAX DISTRICT"),

                SECTION =
                    GetMiscellaneousValue("SECTION"),

                RANGE =
                    GetMiscellaneousValue("RANGE"),

                TAX_YEAR =
                    taxYear,

                RECORDS_LAST_UPDATED =
                    recordsLastUpdated,

                EXEMPT_CODE =
                    GetMiscellaneousValue("EXEMPT CODE"),

                HOMESTEAD_CODE =
                    GetMiscellaneousValue("HOMESTEAD CODE"),

                BOOK =
                    GetBookPageValue("Book"),

                PAGE =
                    GetBookPageValue("Page"),

                TAX_INFORMATION =
                    taxInformation,

                TAX_SALE_HISTORY_JSON =
                    GetTaxSaleHistoryJson()
            };
        }




        private static void SaveToDB(List<LienResult> successfulLiens)
        {
            const string connectionString =
                "Server=DataServer;Database=ContentGrabber;Trusted_Connection=True;TrustServerCertificate=True;";

            const string recreateTableQuery = @"
                DROP TABLE IF EXISTS dbo.BackTaxRaw_MS_Adams_28001;

                CREATE TABLE dbo.BackTaxRaw_MS_Adams_28001
                (
                    TaxLienID           NVARCHAR(255) NULL,
                    APN                 NVARCHAR(255) NULL,
                    AdvertisementNumber NVARCHAR(255) NULL,
                    ParcelID            NVARCHAR(255) NULL,
                    AuctionYear         NVARCHAR(255) NULL,
                    DelinquentYear      NVARCHAR(255) NULL,
                    PARCEL              NVARCHAR(MAX) NULL,
                    ADDRESS             NVARCHAR(MAX) NULL,
                    OWNER               NVARCHAR(MAX) NULL,
                    ACRES               NVARCHAR(MAX) NULL,
                    LAND_VALUE          NVARCHAR(MAX) NULL,
                    IMPROVEMENTS        NVARCHAR(MAX) NULL,
                    TOTAL_VALUE         NVARCHAR(MAX) NULL,
                    ASSESSED            NVARCHAR(MAX) NULL,
                    PPIN                NVARCHAR(MAX) NULL,
                    TOWNSHIP            NVARCHAR(MAX) NULL,
                    LEGAL               NVARCHAR(MAX) NULL,
                    TAX_DISTRICT        NVARCHAR(MAX) NULL,
                    SECTION             NVARCHAR(MAX) NULL,
                    RANGE               NVARCHAR(MAX) NULL,
                    TAX_YEAR            NVARCHAR(MAX) NULL,
                    RECORDS_LAST_UPDATED NVARCHAR(MAX) NULL,
                    EXEMPT_CODE         NVARCHAR(MAX) NULL,
                    HOMESTEAD_CODE      NVARCHAR(MAX) NULL,
                    BOOK                NVARCHAR(MAX) NULL,
                    PAGE                NVARCHAR(MAX) NULL,
                    TAX_INFO_YEAR       NVARCHAR(MAX) NULL,
                    COUNTY_TAX_DUE      NVARCHAR(MAX) NULL,
                    COUNTY_PAID         NVARCHAR(MAX) NULL,
                    COUNTY_BALANCE      NVARCHAR(MAX) NULL,
                    CITY_TAX_DUE        NVARCHAR(MAX) NULL,
                    CITY_PAID           NVARCHAR(MAX) NULL,
                    CITY_BALANCE        NVARCHAR(MAX) NULL,
                    SCHOOL_TAX_DUE      NVARCHAR(MAX) NULL,
                    SCHOOL_PAID         NVARCHAR(MAX) NULL,
                    SCHOOL_BALANCE      NVARCHAR(MAX) NULL,
                    TOTAL_TAX_DUE       NVARCHAR(MAX) NULL,
                    TOTAL_PAID          NVARCHAR(MAX) NULL,
                    TOTAL_BALANCE       NVARCHAR(MAX) NULL,
                    LAST_PAYMENT_DATE   NVARCHAR(MAX) NULL,
                    TAX_SALE_HISTORY_JSON NVARCHAR(MAX) NULL
                );";

            const string insertQuery = @"
                INSERT INTO dbo.BackTaxRaw_MS_Adams_28001
                (
                    TaxLienID, APN, AdvertisementNumber, ParcelID, AuctionYear,
                    DelinquentYear, PARCEL, ADDRESS, OWNER, ACRES, LAND_VALUE,
                    IMPROVEMENTS, TOTAL_VALUE, ASSESSED, PPIN, TOWNSHIP, LEGAL,
                    TAX_DISTRICT, SECTION, RANGE, TAX_YEAR, RECORDS_LAST_UPDATED,
                    EXEMPT_CODE, HOMESTEAD_CODE, BOOK, PAGE, TAX_INFO_YEAR,
                    COUNTY_TAX_DUE, COUNTY_PAID, COUNTY_BALANCE, CITY_TAX_DUE,
                    CITY_PAID, CITY_BALANCE, SCHOOL_TAX_DUE, SCHOOL_PAID,
                    SCHOOL_BALANCE, TOTAL_TAX_DUE, TOTAL_PAID, TOTAL_BALANCE,
                    LAST_PAYMENT_DATE, TAX_SALE_HISTORY_JSON
                )
                VALUES
                (
                    @TaxLienID, @APN, @AdvertisementNumber, @ParcelID, @AuctionYear,
                    @DelinquentYear, @PARCEL, @ADDRESS, @OWNER, @ACRES, @LAND_VALUE,
                    @IMPROVEMENTS, @TOTAL_VALUE, @ASSESSED, @PPIN, @TOWNSHIP, @LEGAL,
                    @TAX_DISTRICT, @SECTION, @RANGE, @TAX_YEAR, @RECORDS_LAST_UPDATED,
                    @EXEMPT_CODE, @HOMESTEAD_CODE, @BOOK, @PAGE, @TAX_INFO_YEAR,
                    @COUNTY_TAX_DUE, @COUNTY_PAID, @COUNTY_BALANCE, @CITY_TAX_DUE,
                    @CITY_PAID, @CITY_BALANCE, @SCHOOL_TAX_DUE, @SCHOOL_PAID,
                    @SCHOOL_BALANCE, @TOTAL_TAX_DUE, @TOTAL_PAID, @TOTAL_BALANCE,
                    @LAST_PAYMENT_DATE, @TAX_SALE_HISTORY_JSON
                );";

            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();

            using SqlTransaction transaction = connection.BeginTransaction();

            try
            {
                using (SqlCommand recreateCommand =
                    new SqlCommand(recreateTableQuery, connection, transaction))
                {
                    recreateCommand.ExecuteNonQuery();
                }

                foreach (LienResult result in successfulLiens)
                {
                    using SqlCommand insertCommand =
                        new SqlCommand(insertQuery, connection, transaction);

                    void AddValue(string name, string? value)
                    {
                        insertCommand.Parameters.Add(name, SqlDbType.NVarChar, -1).Value =
                            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
                    }

                    AddValue("@TaxLienID", result.Lien.TaxLienID);
                    AddValue("@APN", result.Lien.APN);
                    AddValue("@AdvertisementNumber", result.Lien.AdvNum);
                    AddValue("@ParcelID", result.Lien.ParcelID);
                    AddValue("@AuctionYear", result.Lien.AuctionYear);
                    AddValue("@DelinquentYear", result.Lien.TaxYear);
                    AddValue("@PARCEL", result.PARCEL);
                    AddValue("@ADDRESS", result.ADDRESS);
                    AddValue("@OWNER", result.OWNER);
                    AddValue("@ACRES", result.ACRES);
                    AddValue("@LAND_VALUE", result.LAND_VALUE);
                    AddValue("@IMPROVEMENTS", result.IMPROVEMENTS);
                    AddValue("@TOTAL_VALUE", result.TOTAL_VALUE);
                    AddValue("@ASSESSED", result.ASSESSED);
                    AddValue("@PPIN", result.PPIN);
                    AddValue("@TOWNSHIP", result.TOWNSHIP);
                    AddValue("@LEGAL", result.LEGAL);
                    AddValue("@TAX_DISTRICT", result.TAX_DISTRICT);
                    AddValue("@SECTION", result.SECTION);
                    AddValue("@RANGE", result.RANGE);
                    AddValue("@TAX_YEAR", result.TAX_YEAR);
                    AddValue("@RECORDS_LAST_UPDATED", result.RECORDS_LAST_UPDATED);
                    AddValue("@EXEMPT_CODE", result.EXEMPT_CODE);
                    AddValue("@HOMESTEAD_CODE", result.HOMESTEAD_CODE);
                    AddValue("@BOOK", result.BOOK);
                    AddValue("@PAGE", result.PAGE);
                    AddValue("@TAX_INFO_YEAR", result.TAX_INFORMATION?.YEAR);
                    AddValue("@COUNTY_TAX_DUE", result.TAX_INFORMATION?.COUNTY_TAX_DUE);
                    AddValue("@COUNTY_PAID", result.TAX_INFORMATION?.COUNTY_PAID);
                    AddValue("@COUNTY_BALANCE", result.TAX_INFORMATION?.COUNTY_BALANCE);
                    AddValue("@CITY_TAX_DUE", result.TAX_INFORMATION?.CITY_TAX_DUE);
                    AddValue("@CITY_PAID", result.TAX_INFORMATION?.CITY_PAID);
                    AddValue("@CITY_BALANCE", result.TAX_INFORMATION?.CITY_BALANCE);
                    AddValue("@SCHOOL_TAX_DUE", result.TAX_INFORMATION?.SCHOOL_TAX_DUE);
                    AddValue("@SCHOOL_PAID", result.TAX_INFORMATION?.SCHOOL_PAID);
                    AddValue("@SCHOOL_BALANCE", result.TAX_INFORMATION?.SCHOOL_BALANCE);
                    AddValue("@TOTAL_TAX_DUE", result.TAX_INFORMATION?.TOTAL_TAX_DUE);
                    AddValue("@TOTAL_PAID", result.TAX_INFORMATION?.TOTAL_PAID);
                    AddValue("@TOTAL_BALANCE", result.TAX_INFORMATION?.TOTAL_BALANCE);
                    AddValue("@LAST_PAYMENT_DATE", result.TAX_INFORMATION?.LAST_PAYMENT_DATE);
                    AddValue("@TAX_SALE_HISTORY_JSON", result.TAX_SALE_HISTORY_JSON);

                    insertCommand.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
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
                "@B10tx2025"
                   

            );
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;

            smtp.Send(message);
        }

        
    }
}
