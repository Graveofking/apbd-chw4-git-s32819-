using System;

namespace LegacyRenewalApp
{
    public class SubscriptionRenewalService
    {
        private readonly ICustomerRepository _customers;
        private readonly ISubscriptionPlanRepository _plans;
        private readonly IBillingGateway _billing;
        private readonly RenewalCalculator _calculator;
        
        public SubscriptionRenewalService(
            ICustomerRepository customers,
            ISubscriptionPlanRepository plans,
            IBillingGateway billing,
            RenewalCalculator calculator)
        {
            _customers  = customers;
            _billing    = billing;
            _plans      = plans;
            _calculator = calculator;
        }
        
        public SubscriptionRenewalService()
            : this(
                new CustomerRepository(),
                new SubscriptionPlanRepository(),
                new LegacyBillingGatewayAdapter(),
                new RenewalCalculator())
        { }
        public RenewalInvoice CreateRenewalInvoice(
            int customerId,
            string planCode,
            int seatCount,
            string paymentMethod,
            bool includePremiumSupport,
            bool useLoyaltyPoints)
        {
            ValidateInputs(customerId, planCode, seatCount, paymentMethod);

            string normalizedPlanCode      = planCode.Trim().ToUpperInvariant();
            string normalizedPaymentMethod = paymentMethod.Trim().ToUpperInvariant();

            var customer = _customers.GetById(customerId);
            var plan     = _plans.GetByCode(normalizedPlanCode);

            if (!customer.IsActive)
                throw new InvalidOperationException("Inactive customers cannot renew subscriptions");

            var result = _calculator.Calculate(
                customer, plan, seatCount,
                normalizedPaymentMethod,
                includePremiumSupport,
                useLoyaltyPoints);

            var invoice = BuildInvoice(customerId, customer, plan, seatCount,
                                       normalizedPlanCode, normalizedPaymentMethod, result);

            _billing.SaveInvoice(invoice);
            SendRenewalEmail(customer, normalizedPlanCode, invoice);

            return invoice;
        }
        private static void ValidateInputs(int customerId, string planCode, int seatCount, string paymentMethod)
        {
            if (customerId <= 0)              throw new ArgumentException("Customer id must be positive");
            if (string.IsNullOrWhiteSpace(planCode))     throw new ArgumentException("Plan code is required");
            if (seatCount <= 0)              throw new ArgumentException("Seat count must be positive");
            if (string.IsNullOrWhiteSpace(paymentMethod)) throw new ArgumentException("Payment method is required");
        }

        private static RenewalInvoice BuildInvoice(
            int customerId,
            Customer customer,
            SubscriptionPlan plan,
            int seatCount,
            string planCode,
            string paymentMethod,
            RenewalResult result)
        {
            return new RenewalInvoice
            {
                InvoiceNumber  = $"INV-{DateTime.UtcNow:yyyyMMdd}-{customerId}-{planCode}",
                CustomerName   = customer.FullName,
                PlanCode       = planCode,
                PaymentMethod  = paymentMethod,
                SeatCount      = seatCount,
                BaseAmount     = Round(result.BaseAmount),
                DiscountAmount = Round(result.DiscountAmount),
                SupportFee     = Round(result.SupportFee),
                PaymentFee     = Round(result.PaymentFee),
                TaxAmount      = Round(result.TaxAmount),
                FinalAmount    = Round(result.FinalAmount),
                Notes          = result.Notes,
                GeneratedAt    = DateTime.UtcNow
            };
        }

        private void SendRenewalEmail(Customer customer, string planCode, RenewalInvoice invoice)
        {
            if (string.IsNullOrWhiteSpace(customer.Email)) return;

            _billing.SendEmail(
                customer.Email,
                subject: "Subscription renewal invoice",
                body: $"Hello {customer.FullName}, your renewal for plan {planCode} " +
                      $"has been prepared. Final amount: {invoice.FinalAmount:F2}.");
        }

        private static decimal Round(decimal value)
            => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
