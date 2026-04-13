namespace LegacyRenewalApp
{
    /// <summary>
    /// Wraps the unmodifiable static LegacyBillingGateway behind IBillingGateway,
    /// removing the direct static dependency from all business code.
    /// </summary>
    public class LegacyBillingGatewayAdapter : IBillingGateway
    {
        public void SaveInvoice(RenewalInvoice invoice)
            => LegacyBillingGateway.SaveInvoice(invoice);

        public void SendEmail(string email, string subject, string body)
            => LegacyBillingGateway.SendEmail(email, subject, body);
    }
}
