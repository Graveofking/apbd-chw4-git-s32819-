using System;
using System.Collections.Generic;
using System.Text;

namespace LegacyRenewalApp
{
    /// <summary>
    /// Encapsulates all pricing logic for a subscription renewal:
    /// base amount, discounts, support fee, payment fee, tax.
    ///
    /// All if-else chains are replaced with dictionary lookups (DRY, OCP).
    /// Adding a new country, segment, or payment method means adding one entry
    /// to a dictionary — no modification of existing logic.
    ///
    /// Single responsibility: knows how to price a renewal.
    /// Does not know about persistence or notifications.
    /// </summary>
    public class RenewalCalculator
    {
        // --- Lookup tables replace if-else chains (DRY + OCP) ---

        private static readonly Dictionary<string, decimal> SegmentDiscounts = new()
        {
            ["Silver"]   = 0.05m,
            ["Gold"]     = 0.10m,
            ["Platinum"] = 0.15m,
        };

        private static readonly Dictionary<string, decimal> PremiumSupportFees = new()
        {
            ["START"]      = 250m,
            ["PRO"]        = 400m,
            ["ENTERPRISE"] = 700m,
        };

        private static readonly Dictionary<string, decimal> PaymentFeeRates = new()
        {
            ["CARD"]          = 0.020m,
            ["BANK_TRANSFER"] = 0.010m,
            ["PAYPAL"]        = 0.035m,
            ["INVOICE"]       = 0.000m,
        };

        private static readonly Dictionary<string, decimal> TaxRates = new()
        {
            ["Poland"]         = 0.23m,
            ["Germany"]        = 0.19m,
            ["Czech Republic"] = 0.21m,
            ["Norway"]         = 0.25m,
        };

        private const decimal DefaultTaxRate           = 0.20m;
        private const decimal MinDiscountedSubtotal    = 300m;
        private const decimal MinFinalAmount           = 500m;
        private const int     MaxRedeemableLoyaltyPts  = 200;

        // ----------------------------------------------------------------

        public RenewalResult Calculate(
            Customer customer,
            SubscriptionPlan plan,
            int seatCount,
            string normalizedPaymentMethod,
            bool includePremiumSupport,
            bool useLoyaltyPoints)
        {
            if (!PaymentFeeRates.ContainsKey(normalizedPaymentMethod))
                throw new ArgumentException($"Unsupported payment method: {normalizedPaymentMethod}");

            var notes = new StringBuilder();

            decimal baseAmount     = (plan.MonthlyPricePerSeat * seatCount * 12m) + plan.SetupFee;
            decimal discountAmount = CalculateDiscount(customer, plan, seatCount, baseAmount, useLoyaltyPoints, notes);

            decimal subtotal = EnforceMinimum(baseAmount - discountAmount, MinDiscountedSubtotal, notes,
                                              "minimum discounted subtotal applied");

            decimal supportFee = CalculateSupportFee(plan.Code, includePremiumSupport, notes);
            decimal paymentFee = CalculatePaymentFee(normalizedPaymentMethod, subtotal + supportFee, notes);

            decimal taxBase   = subtotal + supportFee + paymentFee;
            decimal taxRate   = TaxRates.GetValueOrDefault(customer.Country, DefaultTaxRate);
            decimal taxAmount = taxBase * taxRate;

            decimal finalAmount = EnforceMinimum(taxBase + taxAmount, MinFinalAmount, notes,
                                                 "minimum invoice amount applied");

            return new RenewalResult(baseAmount, discountAmount, supportFee, paymentFee,
                                     taxAmount, finalAmount, notes.ToString().TrimEnd());
        }

        // ----------------------------------------------------------------
        // Private helpers — each rule is one focused method, not a class
        // ----------------------------------------------------------------

        private static decimal CalculateDiscount(
            Customer customer,
            SubscriptionPlan plan,
            int seatCount,
            decimal baseAmount,
            bool useLoyaltyPoints,
            StringBuilder notes)
        {
            decimal total = 0m;

            // Segment discount
            if (SegmentDiscounts.TryGetValue(customer.Segment, out decimal segmentRate))
            {
                total += baseAmount * segmentRate;
                notes.Append($"{customer.Segment.ToLower()} discount; ");
            }
            else if (customer.Segment == "Education" && plan.IsEducationEligible)
            {
                total += baseAmount * 0.20m;
                notes.Append("education discount; ");
            }

            // Loyalty years discount
            if (customer.YearsWithCompany >= 5)
            {
                total += baseAmount * 0.07m;
                notes.Append("long-term loyalty discount; ");
            }
            else if (customer.YearsWithCompany >= 2)
            {
                total += baseAmount * 0.03m;
                notes.Append("basic loyalty discount; ");
            }

            // Team size discount
            decimal teamRate = seatCount switch
            {
                >= 50 => 0.12m,
                >= 20 => 0.08m,
                >= 10 => 0.04m,
                _     => 0m
            };
            if (teamRate > 0)
            {
                total += baseAmount * teamRate;
                string label = seatCount >= 50 ? "large" : seatCount >= 20 ? "medium" : "small";
                notes.Append($"{label} team discount; ");
            }

            // Loyalty points redemption
            if (useLoyaltyPoints && customer.LoyaltyPoints > 0)
            {
                int pts = Math.Min(customer.LoyaltyPoints, MaxRedeemableLoyaltyPts);
                total += pts;
                notes.Append($"loyalty points used: {pts}; ");
            }

            return total;
        }

        private static decimal CalculateSupportFee(string planCode, bool include, StringBuilder notes)
        {
            if (!include) return 0m;
            decimal fee = PremiumSupportFees.GetValueOrDefault(planCode, 0m);
            notes.Append("premium support included; ");
            return fee;
        }

        private static decimal CalculatePaymentFee(string method, decimal base_, StringBuilder notes)
        {
            decimal rate = PaymentFeeRates[method];
            if (rate > 0) notes.Append($"{method.ToLower().Replace('_', ' ')} fee; ");
            else          notes.Append("invoice payment; ");
            return base_ * rate;
        }

        private static decimal EnforceMinimum(decimal value, decimal floor, StringBuilder notes, string label)
        {
            if (value < floor)
            {
                notes.Append($"{label}; ");
                return floor;
            }
            return value;
        }
    }

    /// <summary>Plain data carrier for the result of a pricing calculation.</summary>
    public record RenewalResult(
        decimal BaseAmount,
        decimal DiscountAmount,
        decimal SupportFee,
        decimal PaymentFee,
        decimal TaxAmount,
        decimal FinalAmount,
        string  Notes);
}
