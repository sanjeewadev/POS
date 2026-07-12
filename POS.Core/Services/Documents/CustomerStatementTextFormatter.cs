using System;
using System.Linq;
using System.Text;
using POS.Core.Models.DTOs;

namespace POS.Core.Services.Documents
{
    public sealed class CustomerStatementTextFormatter
    {
        public string Format(CustomerAccountSummaryDto summary, DateTime? fromDate, DateTime? toDate)
        {
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));

            var builder = new StringBuilder();
            builder.AppendLine("CUSTOMER ACCOUNT STATEMENT");
            builder.AppendLine(new string('=', 76));
            builder.AppendLine($"Customer : {summary.CustomerCode} - {summary.CustomerName}");
            builder.AppendLine($"Period   : {(fromDate?.ToString("yyyy-MM-dd") ?? "Beginning")} to {(toDate?.ToString("yyyy-MM-dd") ?? "Today")}");
            builder.AppendLine($"Printed  : {DateTime.Now:yyyy-MM-dd HH:mm}");
            builder.AppendLine();
            builder.AppendLine($"Credit Limit      Rs. {summary.CreditLimit,14:N2}");
            builder.AppendLine($"Outstanding       Rs. {summary.CurrentBalance,14:N2}");
            builder.AppendLine($"Available Credit  Rs. {summary.AvailableCredit,14:N2}");
            builder.AppendLine($"Overdue           Rs. {summary.OverdueAmount,14:N2}");
            builder.AppendLine();
            builder.AppendLine("DATE              DOCUMENT        TYPE                 DEBIT       CREDIT      BALANCE");
            builder.AppendLine(new string('-', 94));

            foreach (CustomerLedgerStatementRowDto row in summary.StatementRows.OrderBy(row => row.TransactionDate).ThenBy(row => row.LedgerId))
            {
                builder.AppendLine(
                    $"{row.TransactionDate:yyyy-MM-dd HH:mm}  " +
                    $"{Trim(row.DocumentRef, 14),-14}  " +
                    $"{Trim(row.TransactionType, 18),-18}  " +
                    $"{row.DebitAmount,10:N2}  " +
                    $"{row.CreditAmount,10:N2}  " +
                    $"{row.RunningBalance,11:N2}");
            }

            builder.AppendLine(new string('-', 94));
            builder.AppendLine($"Closing Balance: Rs. {summary.CurrentBalance:N2}");
            builder.AppendLine();
            builder.AppendLine("AGING");
            builder.AppendLine($"Current   Rs. {summary.AgingCurrent:N2}");
            builder.AppendLine($"1-30      Rs. {summary.Aging1To30:N2}");
            builder.AppendLine($"31-60     Rs. {summary.Aging31To60:N2}");
            builder.AppendLine($"61-90     Rs. {summary.Aging61To90:N2}");
            builder.AppendLine($"90+       Rs. {summary.AgingOver90:N2}");
            return builder.ToString();
        }

        private static string Trim(string value, int maxLength)
        {
            string safe = value ?? string.Empty;
            return safe.Length <= maxLength ? safe : safe[..maxLength];
        }
    }
}
